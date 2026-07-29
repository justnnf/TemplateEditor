using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.UtilityNetwork;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using DataDomain = ArcGIS.Core.Data.Domain;
using DataSubtype = ArcGIS.Core.Data.Subtype;

namespace TemplateEditor;

/// <summary>
/// Cache for feature info lookups (domain descriptions, subtypes) to avoid redundant field/domain queries.
/// Cache key format: "{LayerName}:{FieldName}:{Value}:{SubtypeCode}"
/// </summary>
internal static class FeatureInfoCache
{
	private static readonly Dictionary<string, string> DomainDescriptionCache = new(StringComparer.Ordinal);
	private static readonly Dictionary<string, DataSubtype> SubtypeCache = new(StringComparer.Ordinal);
	private static readonly object LockObject = new();

	/// <summary>
	/// Gets a cached domain description or retrieves and caches it if not found.
	/// </summary>
	public static string GetDomainDescription(DataDomain domain, object value, string cacheKeyPrefix = null)
	{
		if (domain is not CodedValueDomain codedValueDomain || value == null)
		{
			return null;
		}

		string valueText = Convert.ToString(value);
		string cacheKey = $"{cacheKeyPrefix ?? ""}Domain:{domain?.GetName() ?? ""}:{valueText}";

		lock (LockObject)
		{
			if (DomainDescriptionCache.TryGetValue(cacheKey, out var cachedDescription))
			{
				return cachedDescription;
			}
		}

		// Perform lookup
		foreach (KeyValuePair<object, string> pair in codedValueDomain.GetCodedValuePairs())
		{
			if (string.Equals(Convert.ToString(pair.Key), valueText, StringComparison.OrdinalIgnoreCase))
			{
				lock (LockObject)
				{
					DomainDescriptionCache[cacheKey] = pair.Value;
				}
				return pair.Value;
			}
		}

		// Cache negative result
		lock (LockObject)
		{
			DomainDescriptionCache[cacheKey] = null;
		}
		return null;
	}

	/// <summary>
	/// Gets a cached subtype or retrieves and caches it if not found.
	/// </summary>
	public static DataSubtype GetSubtype(TableDefinition definition, Feature feature, string cacheKeyPrefix = null)
	{
		string subtypeField = definition?.GetSubtypeField();
		if (string.IsNullOrWhiteSpace(subtypeField))
		{
			return null;
		}

		object subtypeValue = feature[subtypeField];
		if (subtypeValue == null || subtypeValue == DBNull.Value)
		{
			return null;
		}

		string subtypeValueText = Convert.ToString(subtypeValue);
		string cacheKey = $"{cacheKeyPrefix ?? ""}Subtype:{definition?.GetName() ?? ""}:{subtypeValueText}";

		lock (LockObject)
		{
			if (SubtypeCache.TryGetValue(cacheKey, out var cachedSubtype))
			{
				return cachedSubtype;
			}
		}

		// Perform lookup
		var subtype = definition.GetSubtypes().FirstOrDefault((DataSubtype st) =>
			string.Equals(Convert.ToString(st.GetCode()), subtypeValueText, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(st.GetName(), subtypeValueText, StringComparison.OrdinalIgnoreCase));

		lock (LockObject)
		{
			SubtypeCache[cacheKey] = subtype;
		}
		return subtype;
	}

	/// <summary>
	/// Clears the cache. Call this between placement operations if needed.
	/// </summary>
	public static void Clear()
	{
		lock (LockObject)
		{
			DomainDescriptionCache.Clear();
			SubtypeCache.Clear();
		}
	}
}

internal static class PlacementEnhancementService
{
	private static readonly SemaphoreSlim EnhancementPromptGate = new SemaphoreSlim(1, 1);
	// Cache for FACILITYID field names per layer to avoid repeated field lookups
	private static readonly Dictionary<string, string> FacilityIdFieldCache = new Dictionary<string, string>(StringComparer.Ordinal);
	// Cache for layer metadata (definition, fields, owning group) to avoid repeated queries per feature
	private static readonly Dictionary<string, LayerMetadata> LayerMetadataCache = new Dictionary<string, LayerMetadata>(StringComparer.Ordinal);
	private static readonly object CacheLock = new object();

	public static async Task ApplyPostPlacementEnhancementsAsync(IReadOnlyList<PlacedFeatureContext> createdFeatures, IReadOnlyList<ExistingAssociationPair> existingAssociations = null)
	{
		if (createdFeatures == null || createdFeatures.Count == 0)
		{
			return;
		}
		if (!await EnhancementPromptGate.WaitAsync(TimeSpan.FromSeconds(30)))
		{
			await ShowMessageBoxAsync(
				"Automatic split and association prompts were skipped because another Template Editor placement prompt is still open. Finish the open prompt, then review the placed feature's associations before continuing.",
				"Template Editor",
				MessageBoxButton.OK);
			return;
		}
		try
		{
			HashSet<string> processedSplitPointKeys = new(StringComparer.Ordinal);
			foreach (PlacedFeatureContext createdFeature in createdFeatures)
			{
				if (createdFeature?.Layer == null || createdFeature.Geometry == null || createdFeature.ObjectID <= 0 || !createdFeature.AllowPlacementEnhancements)
				{
					continue;
				}
				await RunEnhancementStepAsync("line split", () => TryPromptForLineSplitAsync(createdFeature, createdFeatures, processedSplitPointKeys));
				await WaitForEnhancementSettleAsync();
				await RunEnhancementStepAsync("association", () => TryPromptForAssociationsAsync(createdFeature, createdFeatures, existingAssociations));
				await WaitForEnhancementSettleAsync();
			}
		}
		finally
		{
			EnhancementPromptGate.Release();
		}
	}

	private static async Task RunEnhancementStepAsync(string stepName, Func<Task> action)
	{
		try
		{
			await action();
		}
		catch (Exception ex)
		{
			if (AddinConfiguration.Settings?.ShowAutomaticStepDiagnostics == false)
			{
				return;
			}
			await ShowMessageBoxAsync($"The automatic {stepName} step could not be completed.\n\n{ex.Message}\n\nTemplate Editor will continue with the next automatic placement step.", "Template Editor", MessageBoxButton.OK);
		}
	}

	private static async Task WaitForEnhancementSettleAsync()
	{
		await Application.Current.Dispatcher.InvokeAsync(() => { });
	}

	private static async Task TryPromptForLineSplitAsync(PlacedFeatureContext createdFeature, IReadOnlyList<PlacedFeatureContext> featuresCreatedByOperation, HashSet<string> processedSplitPointKeys)
	{
		TemplateEditorSettings settings = AddinConfiguration.Settings;
		if (settings == null || !settings.EnableLineSplitPrompts || string.Equals(settings.SplitPromptMode, "Never", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		string groupName = createdFeature.Template?.GroupLayer?.ToUpperInvariant();
		GeometryType createdShapeType = await QueuedTask.Run(() => createdFeature.Layer.GetFeatureClass().GetDefinition().GetShapeType());
		if (GeometryTypeHelper.IsPoint(createdShapeType))
		{
			if (!settings.EnablePointPlacementSplitPrompt || !settings.SplitPointPlacementGroups.Contains(groupName))
			{
				return;
			}
			await TryPromptForSingleSplitPointAsync(createdFeature, featuresCreatedByOperation, processedSplitPointKeys, (MapPoint)createdFeature.Geometry, "Split underlying line at the placement point?");
			return;
		}
		if (GeometryTypeHelper.IsPolyline(createdShapeType))
		{
			if (!settings.EnableLineEndpointSplitPrompt || !settings.SplitLinePlacementGroups.Contains(groupName))
			{
				return;
			}
			Polyline polyline = createdFeature.Geometry as Polyline;
			if (polyline == null || polyline.PointCount < 2)
			{
				return;
			}
			List<string> availableOptions = new List<string>();
			MapPoint startPoint = polyline.Points.First();
			MapPoint endPoint = polyline.Points.Last();
			List<FeatureCandidate> startCandidates = settings.EnableSplitAtLineStartPoint && (!settings.SuppressDuplicateSplitPrompts || !WasSplitPointProcessed(processedSplitPointKeys, startPoint))
				? await FindSplitCandidatesAsync(createdFeature, featuresCreatedByOperation, startPoint)
				: new List<FeatureCandidate>();
			List<FeatureCandidate> endCandidates = settings.EnableSplitAtLineEndPoint && (!settings.SuppressDuplicateSplitPrompts || !WasSplitPointProcessed(processedSplitPointKeys, endPoint))
				? await FindSplitCandidatesAsync(createdFeature, featuresCreatedByOperation, endPoint)
				: new List<FeatureCandidate>();
			bool hasStartCandidate = startCandidates.Count > 0;
			bool hasEndCandidate = endCandidates.Count > 0;
			if (hasStartCandidate)
			{
				availableOptions.Add("Start");
			}
			if (hasEndCandidate)
			{
				availableOptions.Add("End");
			}
			if (availableOptions.Count == 0)
			{
				return;
			}
			if (availableOptions.Count == 1)
			{
				if (hasStartCandidate)
				{
					await TryPromptForSingleSplitPointAsync(createdFeature, featuresCreatedByOperation, processedSplitPointKeys, startPoint, "Split underlying line at the insert/start point?", startCandidates);
				}
				else
				{
					await TryPromptForSingleSplitPointAsync(createdFeature, featuresCreatedByOperation, processedSplitPointKeys, endPoint, "Split underlying line at this line endpoint?", endCandidates);
				}
				return;
			}
			LineSplitChoiceDialog choiceDialog = await ShowDialogAsync(() => new LineSplitChoiceDialog(availableOptions));
			if (choiceDialog == null)
			{
				return;
			}
			if (choiceDialog.SplitAtStart)
			{
				await TryPromptForSingleSplitPointAsync(createdFeature, featuresCreatedByOperation, processedSplitPointKeys, startPoint, "Split underlying line at the insert/start point?", startCandidates);
			}
			if (choiceDialog.SplitAtEnd)
			{
				await TryPromptForSingleSplitPointAsync(createdFeature, featuresCreatedByOperation, processedSplitPointKeys, endPoint, "Split underlying line at this line endpoint?", endCandidates);
			}
		}
	}

	private static async Task TryPromptForSingleSplitPointAsync(PlacedFeatureContext createdFeature, IReadOnlyList<PlacedFeatureContext> featuresCreatedByOperation, HashSet<string> processedSplitPointKeys, MapPoint splitPoint, string message, List<FeatureCandidate> splitCandidates = null)
	{
		TemplateEditorSettings settings = AddinConfiguration.Settings;
		if (settings?.SuppressDuplicateSplitPrompts == true && WasSplitPointProcessed(processedSplitPointKeys, splitPoint))
		{
			return;
		}
		splitCandidates ??= await FindSplitCandidatesAsync(createdFeature, featuresCreatedByOperation, splitPoint);
		if (splitCandidates.Count == 0)
		{
			return;
		}
		if (settings?.SuppressDuplicateSplitPrompts == true)
		{
			TrackProcessedSplitPoint(processedSplitPointKeys, splitPoint);
		}
		FeatureCandidate splitCandidate = splitCandidates.Count == 1 ? splitCandidates[0] : await ChooseCandidateAsync(createdFeature, "Choose Line To Split", "Review the highlighted line and choose which one to split.", splitCandidates, isSplitCandidate: true);
		if (splitCandidate == null)
		{
			return;
		}
		bool autoSplit = splitCandidates.Count == 1 && string.Equals(settings?.SplitPromptMode, "AutoWhenOne", StringComparison.OrdinalIgnoreCase);
		if (!autoSplit)
		{
			using (await ShowCandidateContextAsync(createdFeature, splitCandidate, isSplitCandidate: true))
			{
				if (!await ShowConfirmationAsync(message, "Template Editor", "Split Line", "Skip"))
				{
					return;
				}
			}
		}
		await ExecuteSplitAsync(splitCandidate.Layer, splitCandidate.ObjectID, splitPoint);
	}


	/// <summary>
	/// Generates a string key for a MapPoint to enable O(1) split point deduplication.
	/// Uses rounded coordinates to account for floating-point precision.
	/// </summary>
	private static string GetSplitPointKey(MapPoint point)
	{
		if (point == null)
			return null;

		int wkid = point.SpatialReference?.Wkid ?? 0;
		// Round coordinates to avoid floating-point precision issues
		// Using scale of 1e6 means precision to 0.000001 units
		long x = (long)Math.Round(point.X * 1e6);
		long y = (long)Math.Round(point.Y * 1e6);
		return $"{x}|{y}|{wkid}";
	}

	private static void TrackProcessedSplitPoint(HashSet<string> processedSplitPointKeys, MapPoint splitPoint)
	{
		if (processedSplitPointKeys == null || splitPoint == null)
		{
			return;
		}
		string key = GetSplitPointKey(splitPoint);
		if (key != null)
		{
			processedSplitPointKeys.Add(key);
		}
	}

	private static bool WasSplitPointProcessed(HashSet<string> processedSplitPointKeys, MapPoint splitPoint)
	{
		if (splitPoint == null || processedSplitPointKeys == null)
		{
			return false;
		}
		string key = GetSplitPointKey(splitPoint);
		return key != null && processedSplitPointKeys.Contains(key);
	}

	private static bool AreSameSplitPoint(MapPoint firstPoint, MapPoint secondPoint)
	{
		if (firstPoint == null || secondPoint == null)
		{
			return false;
		}
		const double coordinateTolerance = 1e-6;
		return Math.Abs(firstPoint.X - secondPoint.X) <= coordinateTolerance &&
			Math.Abs(firstPoint.Y - secondPoint.Y) <= coordinateTolerance &&
			(firstPoint.SpatialReference == null ||
				secondPoint.SpatialReference == null ||
				firstPoint.SpatialReference.Wkid == secondPoint.SpatialReference.Wkid);
	}

	private static async Task TryPromptForAssociationsAsync(PlacedFeatureContext createdFeature, IReadOnlyList<PlacedFeatureContext> featuresCreatedByOperation, IReadOnlyList<ExistingAssociationPair> existingAssociations)
	{
		TemplateEditorSettings settings = AddinConfiguration.Settings;
		string groupName = createdFeature.Template?.GroupLayer?.ToUpperInvariant();
		if (settings == null || !settings.EnableAssociationPrompts)
		{
			return;
		}
		bool useRuleCatalogSearchScope = AssociationRuleCatalog.Current.HasRules;
		GeometryType createdShapeType = await QueuedTask.Run(() => createdFeature.Layer.GetFeatureClass().GetDefinition().GetShapeType());
		bool createdFeatureIsLine = GeometryTypeHelper.IsPolyline(createdShapeType);
		if (string.Equals(settings.AssociationPromptMode, "Never", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		if (useRuleCatalogSearchScope)
		{
			if (settings.EnableStructuralAttachmentPrompts)
			{
				List<FeatureCandidate> reverseAttachmentCandidates = await FindAssociationCandidatesAsync(
					createdFeature,
					featuresCreatedByOperation,
					settings.AssociationPlacementGroups,
					Enumerable.Empty<string>(),
					settings.StructuralAttachmentSearchDistance,
					AssociationType.Attachment,
					"Structural attachment",
					createdFeatureIsAssociationSource: true);
				reverseAttachmentCandidates = ExcludeExistingAssociationCandidates(createdFeature, reverseAttachmentCandidates, existingAssociations);
				await PromptForMultipleAssociationsAsync(
					createdFeature,
					reverseAttachmentCandidates,
					"Create structural attachments?",
					"Create structural attachment associations for the nearby eligible features?");
			}
			if (settings.EnableContainmentBoundaryPrompts && IsLineOrPolygonGeometry(createdFeature.Geometry))
			{
				List<FeatureCandidate> reverseContainmentCandidates = await FindAssociationCandidatesAsync(
					createdFeature,
					featuresCreatedByOperation,
					settings.AssociationPlacementGroups,
					Enumerable.Empty<string>(),
					settings.ContainmentBoundarySearchDistance,
					AssociationType.Containment,
					"Contain in this structure container",
					createdFeatureIsAssociationSource: true,
					geometryPredicate: IsLineOrPointGeometry);
				reverseContainmentCandidates = ExcludeExistingAssociationCandidates(createdFeature, reverseContainmentCandidates, existingAssociations);
				await PromptForMultipleAssociationsAsync(
					createdFeature,
					reverseContainmentCandidates,
					"Create containment associations?",
					"Create containment associations for the nearby eligible features?");
			}
		}
		if (!useRuleCatalogSearchScope && !settings.AssociationPlacementGroups.Contains(groupName))
		{
			if (settings.EnableStructuralAttachmentPrompts && settings.StructuralAttachmentTargetGroups.Contains(groupName))
			{
				List<FeatureCandidate> reverseAttachmentCandidates = await FindAssociationCandidatesAsync(
					createdFeature,
					featuresCreatedByOperation,
					settings.AssociationPlacementGroups,
					Enumerable.Empty<string>(),
					settings.StructuralAttachmentSearchDistance,
					AssociationType.Attachment,
					"Structural attachment",
					createdFeatureIsAssociationSource: true);
				reverseAttachmentCandidates = ExcludeExistingAssociationCandidates(createdFeature, reverseAttachmentCandidates, existingAssociations);
				await PromptForMultipleAssociationsAsync(
					createdFeature,
					reverseAttachmentCandidates,
					"Create structural attachments?",
					"Create structural attachment associations for the nearby eligible features?");
			}
			if (settings.EnableContainmentBoundaryPrompts && IsContainmentContainerTarget(createdFeature, settings))
			{
				List<FeatureCandidate> reverseContainmentCandidates = await FindAssociationCandidatesAsync(
					createdFeature,
					featuresCreatedByOperation,
					settings.AssociationPlacementGroups,
					Enumerable.Empty<string>(),
					settings.ContainmentBoundarySearchDistance,
					AssociationType.Containment,
					"Contain in this structure container",
					createdFeatureIsAssociationSource: true,
					geometryPredicate: IsLineOrPointGeometry);
				reverseContainmentCandidates = ExcludeExistingAssociationCandidates(createdFeature, reverseContainmentCandidates, existingAssociations);
				await PromptForMultipleAssociationsAsync(
					createdFeature,
					reverseContainmentCandidates,
					"Create containment associations?",
					"Create containment associations for the nearby eligible features?");
			}
			return;
		}
		if (settings.EnableStructuralAttachmentPrompts && (!createdFeatureIsLine || settings.EnableLineAssociationPrompts || settings.EnableLineStructuralAttachmentPrompts))
		{
			List<FeatureCandidate> attachmentCandidates = await FindAssociationCandidatesAsync(createdFeature, featuresCreatedByOperation, settings.StructuralAttachmentTargetGroups, settings.StructuralAttachmentTargetLayerNames, settings.StructuralAttachmentSearchDistance, AssociationType.Attachment, "Structural attachment");
			attachmentCandidates = ExcludeExistingAssociationCandidates(createdFeature, attachmentCandidates, existingAssociations);
			AssociationPromptResult attachmentResult = await PromptForAssociationAsync(createdFeature, attachmentCandidates, "Create structural attachment?", "Review the highlighted structural attachment candidate.");
			if (attachmentResult.WasCreated && settings.StopAfterFirstSuccessfulAssociation)
			{
				return;
			}
		}
		if (settings.EnableJunctionJunctionConnectivityPrompts && !createdFeatureIsLine)
		{
			List<FeatureCandidate> connectivityCandidates = await FindAssociationCandidatesAsync(createdFeature, featuresCreatedByOperation, settings.JunctionJunctionConnectivityTargetGroups, settings.JunctionJunctionConnectivityTargetLayerNames, settings.JunctionJunctionConnectivitySearchDistance, UtilityNetworkAssociationTypes.JunctionJunctionConnectivity, "Junction-junction connectivity");
			connectivityCandidates = ExcludeExistingAssociationCandidates(createdFeature, connectivityCandidates, existingAssociations);
			AssociationPromptResult connectivityResult = await PromptForAssociationAsync(createdFeature, connectivityCandidates, "Create junction-junction connectivity association?", "Review the highlighted junction-junction connectivity candidate.");
			if (connectivityResult.WasCreated && settings.StopAfterFirstSuccessfulAssociation)
			{
				return;
			}
		}
		if (settings.EnableContainmentPointPrompts || settings.EnableContainmentBoundaryPrompts)
		{
			List<FeatureCandidate> containmentCandidates = new List<FeatureCandidate>();
			if (settings.EnableContainmentPointPrompts && (!createdFeatureIsLine || settings.EnableLineAssociationPrompts || settings.EnableLineContainmentPointPrompts))
			{
				containmentCandidates.AddRange(await FindAssociationCandidatesAsync(createdFeature, featuresCreatedByOperation, settings.ContainmentPointTargetGroups, settings.ContainmentPointTargetLayerNames, settings.ContainmentPointSearchDistance, AssociationType.Containment, "Containment in structure point", geometryPredicate: IsPointGeometry));
			}
			if (settings.EnableContainmentBoundaryPrompts && (!createdFeatureIsLine || settings.EnableLineContainmentBoundaryPrompts))
			{
				containmentCandidates.AddRange(await FindAssociationCandidatesAsync(createdFeature, featuresCreatedByOperation, settings.ContainmentBoundaryTargetGroups, settings.ContainmentBoundaryTargetLayerNames, settings.ContainmentBoundarySearchDistance, AssociationType.Containment, "Containment in structure container", geometryPredicate: IsLineOrPolygonGeometry));
			}
			containmentCandidates = ExcludeExistingAssociationCandidates(createdFeature, containmentCandidates, existingAssociations);
			await PromptForAssociationAsync(createdFeature, containmentCandidates, "Create containment association?", "Review the highlighted containment candidate.");
		}
	}

	private static List<FeatureCandidate> ExcludeExistingAssociationCandidates(PlacedFeatureContext createdFeature, List<FeatureCandidate> candidates, IReadOnlyList<ExistingAssociationPair> existingAssociations)
	{
		if (candidates == null || candidates.Count == 0 || existingAssociations == null || existingAssociations.Count == 0)
		{
			return candidates ?? new List<FeatureCandidate>();
		}
		return candidates
			.Where((FeatureCandidate candidate) => !existingAssociations.Any((ExistingAssociationPair existingAssociation) =>
				existingAssociation.Matches(candidate.AssociationType, createdFeature.Layer, createdFeature.ObjectID, candidate.Layer, candidate.ObjectID)))
			.ToList();
	}

	private static bool IsContainmentContainerTarget(PlacedFeatureContext createdFeature, TemplateEditorSettings settings)
	{
		string groupName = createdFeature?.Template?.GroupLayer?.ToUpperInvariant();
		if (settings?.ContainmentBoundaryTargetGroups?.Contains(groupName) != true)
		{
			return false;
		}
		List<string> targetLayerNames = settings.ContainmentBoundaryTargetLayerNames ?? new List<string>();
		return targetLayerNames.Count == 0 ||
			targetLayerNames.Contains(createdFeature.Layer?.Name?.ToUpperInvariant()) ||
			targetLayerNames.Contains(createdFeature.Template?.SubtypeLayer?.ToUpperInvariant());
	}

	private static async Task<AssociationPromptResult> PromptForAssociationAsync(PlacedFeatureContext createdFeature, List<FeatureCandidate> candidates, string singlePrompt, string chooserPrompt)
	{
		if (candidates == null || candidates.Count == 0)
		{
			return AssociationPromptResult.NotAttempted;
		}
		TemplateEditorSettings settings = AddinConfiguration.Settings;
		FeatureCandidate chosenCandidate = candidates.Count == 1 ? candidates[0] : await ChooseCandidateAsync(createdFeature, "Choose Association Target", chooserPrompt, candidates, isSplitCandidate: false);
		if (chosenCandidate == null)
		{
			return AssociationPromptResult.NotAttempted;
		}
		bool autoCreate = candidates.Count == 1 &&
			(string.Equals(settings?.AssociationPromptMode, "AutoWhenOne", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(settings?.AssociationPromptMode, "ReviewMultipleOnly", StringComparison.OrdinalIgnoreCase));
		if (!autoCreate)
		{
			using (await ShowCandidateContextAsync(createdFeature, chosenCandidate, isSplitCandidate: false))
			{
				if (!await ShowConfirmationAsync(singlePrompt + "\n\n" + chosenCandidate.Label, "Template Editor", "Create Association", "Skip"))
				{
					return AssociationPromptResult.NotAttempted;
				}
			}
		}
		try
		{
			await ExecuteAssociationAsync(createdFeature, chosenCandidate);
			return AssociationPromptResult.Created(chosenCandidate);
		}
		catch (Exception ex)
		{
			await ShowMessageBoxAsync("The association could not be created.\n\n" + ex.Message, "Template Editor", MessageBoxButton.OK);
			return AssociationPromptResult.Failed(chosenCandidate);
		}
	}

	private static async Task<int> PromptForMultipleAssociationsAsync(PlacedFeatureContext createdFeature, List<FeatureCandidate> candidates, string title, string prompt)
	{
		if (candidates == null || candidates.Count == 0)
		{
			return 0;
		}
		if (candidates.Count == 1)
		{
			AssociationPromptResult result = await PromptForAssociationAsync(createdFeature, candidates, title, prompt);
			return result.WasCreated ? 1 : 0;
		}
		string candidateSummary = string.Join("\n", candidates.Take(12).Select((FeatureCandidate candidate) => "  - " + candidate.Label));
		string additionalText = candidates.Count > 12 ? $"\n  - {candidates.Count - 12} more..." : string.Empty;
		using (await ShowCandidateContextAsync(createdFeature, candidates, isSplitCandidate: false))
		{
			if (!await ShowConfirmationAsync($"{prompt}\n\n{candidates.Count} candidate(s):\n{candidateSummary}{additionalText}", title, "Create Associations", "Skip"))
			{
				return 0;
			}
		}
		int createdCount = 0;
		List<string> failures = new List<string>();
		foreach (FeatureCandidate candidate in candidates)
		{
			try
			{
				await ExecuteAssociationAsync(createdFeature, candidate);
				createdCount++;
			}
			catch (Exception ex)
			{
				failures.Add($"{candidate.Label}: {ex.Message}");
			}
		}
		if (failures.Count > 0)
		{
			string displayedFailures = string.Join("\n", failures.Take(8));
			string additionalFailureText = failures.Count > 8 ? $"\n\n{failures.Count - 8} more association failure(s) were not shown." : string.Empty;
			await ShowMessageBoxAsync(
				$"{createdCount} association(s) were created, but {failures.Count} failed.\n\n{displayedFailures}{additionalFailureText}",
				"Template Editor",
				MessageBoxButton.OK);
		}
		return createdCount;
	}

	private static async Task<List<FeatureCandidate>> FindSplitCandidatesAsync(PlacedFeatureContext createdFeature, IReadOnlyList<PlacedFeatureContext> featuresCreatedByOperation, MapPoint point)
	{
		if (!IsSplitAllowedForPlacedFeature(createdFeature))
		{
			return new List<FeatureCandidate>();
		}
		TemplateEditorSettings settings = AddinConfiguration.Settings;
		Geometry searchGeometry = await CreateSearchGeometryAsync(point, settings.SplitSearchDistance);
		if (!await HasNearbyFeatureCandidatesAsync(settings.SplitTargetLineGroups, settings.SplitTargetLayerNames, searchGeometry, layerContextPredicate: IsElectricLineLayerContext))
		{
			return new List<FeatureCandidate>();
		}
		HashSet<string> createdFeatureKeys = BuildCreatedFeatureKeySet(featuresCreatedByOperation);
		List<FeatureCandidate> candidates = await FindFeatureCandidatesAsync(settings.SplitTargetLineGroups, settings.SplitTargetLayerNames, searchGeometry, createdFeature.Geometry, delegate(FeatureLayer layer, Feature feature, Geometry geometry)
		{
			return !IsFeatureInSet(createdFeatureKeys, layer, feature.GetObjectID()) &&
				(!settings.SplitOnlyInteriorCandidates || IsInteriorSplitCandidate(geometry, point));
		}, "Line", false, settings.MaxSplitCandidatesToReview, layerContextPredicate: IsElectricLineLayerContext);
		return candidates;
	}

	private static HashSet<string> BuildCreatedFeatureKeySet(IReadOnlyList<PlacedFeatureContext> featuresCreatedByOperation)
	{
		HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
		if (featuresCreatedByOperation != null)
		{
			foreach (PlacedFeatureContext feature in featuresCreatedByOperation)
			{
				if (feature?.Layer != null && feature.ObjectID > 0)
				{
					keys.Add($"{feature.Layer.URI}|{feature.ObjectID}");
				}
			}
		}
		return keys;
	}

	private static bool IsFeatureInSet(HashSet<string> createdFeatureKeys, FeatureLayer layer, long objectId)
	{
		if (createdFeatureKeys == null || layer == null || objectId <= 0)
		{
			return false;
		}
		string key = $"{layer.URI}|{objectId}";
		return createdFeatureKeys.Contains(key);
	}

	private static bool WasCreatedByOperation(IReadOnlyList<PlacedFeatureContext> featuresCreatedByOperation, FeatureLayer layer, long objectId)
	{
		return featuresCreatedByOperation != null && featuresCreatedByOperation.Any((PlacedFeatureContext feature) => feature?.Layer == layer && feature.ObjectID == objectId);
	}

	private static async Task<List<FeatureCandidate>> FindAssociationCandidatesAsync(PlacedFeatureContext createdFeature, IReadOnlyList<PlacedFeatureContext> featuresCreatedByOperation, IEnumerable<string> targetGroups, IEnumerable<string> targetLayerNames, double searchDistance, AssociationType associationType, string labelPrefix, bool createdFeatureIsAssociationSource = false, Func<Geometry, bool> geometryPredicate = null)
	{
		Geometry searchGeometry = await CreateSearchGeometryAsync(createdFeature.Geometry, searchDistance);
		bool useRuleCatalogSearchScope = AssociationRuleCatalog.Current.HasRules;
		if (!await HasNearbyFeatureCandidatesAsync(targetGroups, targetLayerNames, searchGeometry, useRuleCatalogSearchScope))
		{
			return new List<FeatureCandidate>();
		}
		FeatureLayerInfo createdFeatureInfo = await GetPlacedFeatureInfoAsync(createdFeature);
		Func<LayerSearchContext, bool> layerContextPredicate = null;
		AssociationRuleCatalog catalog = AssociationRuleCatalog.Current;
		if (catalog.HasRules && createdFeatureInfo != null)
		{
			HashSet<string> allowedCounterpartTables = catalog.GetAllowedCounterpartTables(associationType, createdFeatureInfo, createdFeatureIsAssociationSource);
			if (allowedCounterpartTables != null)
			{
				if (allowedCounterpartTables.Count == 0)
				{
					return new List<FeatureCandidate>();
				}
				layerContextPredicate = (LayerSearchContext context) => allowedCounterpartTables.Contains(NormalizeAssociationName(ResolveUtilityNetworkTableName(context.OwningGroupName, context.Layer?.Name)));
			}
		}
		HashSet<string> createdFeatureKeys = BuildCreatedFeatureKeySet(featuresCreatedByOperation);
		List<FeatureCandidate> candidates = await FindFeatureCandidatesAsync(targetGroups, targetLayerNames, searchGeometry, createdFeature.Geometry, delegate(FeatureLayer layer, Feature feature, Geometry geometry)
		{
			return (geometryPredicate == null || geometryPredicate(geometry)) &&
				!IsFeatureInSet(createdFeatureKeys, layer, feature.GetObjectID()) &&
				IsAllowedAssociationCandidate(associationType, layer, feature, createdFeatureInfo, createdFeatureIsAssociationSource);
		}, labelPrefix, useRuleCatalogSearchScope, int.MaxValue, layerContextPredicate);
		foreach (FeatureCandidate candidate in candidates)
		{
			candidate.AssociationType = associationType;
			candidate.CreatedFeatureIsAssociationSource = createdFeatureIsAssociationSource;
		}
		return candidates;
	}

	private static bool IsPointGeometry(Geometry geometry)
	{
		return geometry is MapPoint || geometry is Multipoint;
	}

	private static bool IsLineOrPolygonGeometry(Geometry geometry)
	{
		return geometry is Polyline || geometry is Polygon;
	}

	private static bool IsLineOrPointGeometry(Geometry geometry)
	{
		return geometry is Polyline || geometry is MapPoint || geometry is Multipoint;
	}

	private static async Task<FeatureLayerInfo> GetPlacedFeatureInfoAsync(PlacedFeatureContext createdFeature)
	{
		if (createdFeature?.Layer == null || createdFeature.ObjectID <= 0)
		{
			return null;
		}
		return await QueuedTask.Run(delegate
		{
			QueryFilter queryFilter = new QueryFilter
			{
				ObjectIDs = new List<long> { createdFeature.ObjectID }
			};
			using RowCursor rowCursor = createdFeature.Layer.Search(queryFilter);
			if (!rowCursor.MoveNext())
			{
				return null;
			}
			using Feature feature = (Feature)rowCursor.Current;
			return GetFeatureLayerInfo(createdFeature.Layer, feature);
		});
	}

	private static bool IsAllowedAssociationCandidate(AssociationType associationType, FeatureLayer candidateLayer, Feature candidateFeature, FeatureLayerInfo createdFeatureInfo, bool createdFeatureIsAssociationSource)
	{
		AssociationRuleCatalog catalog = AssociationRuleCatalog.Current;
		if (!catalog.HasRules || createdFeatureInfo == null || candidateFeature == null)
		{
			return true;
		}
		FeatureLayerInfo candidateInfo = GetFeatureLayerInfo(candidateLayer, candidateFeature);
		return createdFeatureIsAssociationSource
			? catalog.Allows(associationType, createdFeatureInfo, candidateInfo)
			: catalog.Allows(associationType, candidateInfo, createdFeatureInfo);
	}

	private static FeatureLayerInfo GetFeatureLayerInfo(FeatureLayer layer, Feature feature)
	{
		if (layer == null || feature == null)
		{
			return null;
		}

		// Optimize: Cache layer metadata (definition, fields, owning group) per layer to avoid repeated queries
		string layerUri = layer.URI;
		LayerMetadata metadata;

		lock (CacheLock)
		{
			if (!LayerMetadataCache.TryGetValue(layerUri, out metadata))
			{
				// First time for this layer - query and cache the metadata
				TableDefinition definition = layer.GetFeatureClass().GetDefinition() as TableDefinition;
				metadata = new LayerMetadata
				{
					Definition = definition,
					Fields = definition?.GetFields()?.ToList() ?? new List<Field>(),
					OwningGroupName = MapMemberLookupService.GetOwningGroupName(layer)
				};
				LayerMetadataCache[layerUri] = metadata;
			}
		}

		string assetGroup = GetLayerAssetGroupName(layer, metadata.OwningGroupName, feature, metadata.Fields, metadata.Definition);
		string assetType = GetResolvedFieldText(feature, metadata.Fields, metadata.Definition, "ASSETTYPE");
		return new FeatureLayerInfo
		{
			TableName = ResolveUtilityNetworkTableName(metadata.OwningGroupName, layer.Name),
			AssetGroup = assetGroup,
			AssetType = assetType
		};
	}

	private static string GetLayerAssetGroupName(FeatureLayer layer, string owningGroupName, Feature feature, IReadOnlyList<Field> fields, TableDefinition definition)
	{
		if (((Layer)layer).Parent is SubtypeGroupLayer && !string.Equals(layer.Name, owningGroupName, StringComparison.OrdinalIgnoreCase))
		{
			return layer.Name;
		}
		string assetGroup = GetResolvedFieldText(feature, fields, definition, "ASSETGROUP");
		return string.IsNullOrWhiteSpace(assetGroup) ? layer.Name : assetGroup;
	}

	private static string GetResolvedFieldText(Feature feature, IReadOnlyList<Field> fields, TableDefinition definition, string requestedFieldName)
	{
		Field field = fields.FirstOrDefault((Field candidate) => string.Equals(candidate.Name, requestedFieldName, StringComparison.OrdinalIgnoreCase));
		if (field == null)
		{
			return null;
		}
		object value = feature[field.Name];
		if (value == null || value == DBNull.Value)
		{
			return null;
		}
		DataSubtype subtype = FeatureInfoCache.GetSubtype(definition, feature, definition?.GetName());
		string cacheKeyPrefix = $"{definition?.GetName()}:{field.Name}:";
		string domainDescription = FeatureInfoCache.GetDomainDescription(field.GetDomain(subtype), value, cacheKeyPrefix) ?? FeatureInfoCache.GetDomainDescription(field.GetDomain((DataSubtype)null), value, cacheKeyPrefix);
		return string.IsNullOrWhiteSpace(domainDescription) ? Convert.ToString(value) : domainDescription;
	}

	private static string ResolveUtilityNetworkTableName(string owningGroupName, string layerName)
	{
		string normalizedName = NormalizeAssociationName(owningGroupName) ?? NormalizeAssociationName(layerName);
		if (string.IsNullOrWhiteSpace(normalizedName))
		{
			return owningGroupName ?? layerName;
		}
		// These names normalize common Esri electric utility network table/layer labels used by the rule JSON.
		if (normalizedName.Contains("ELECTRICASSEMBLY"))
		{
			return "ElectricAssembly";
		}
		if (normalizedName.Contains("ELECTRICDEVICE"))
		{
			return "ElectricDevice";
		}
		if (normalizedName.Contains("ELECTRICJUNCTIONOBJECT"))
		{
			return "ElectricJunctionObject";
		}
		if (normalizedName.Contains("ELECTRICEDGEOBJECT"))
		{
			return "ElectricEdgeObject";
		}
		if (normalizedName.Contains("ELECTRICJUNCTION"))
		{
			return "ElectricJunction";
		}
		if (normalizedName.Contains("ELECTRICLINE"))
		{
			return "ElectricLine";
		}
		if (normalizedName.Contains("STRUCTUREJUNCTIONOBJECT"))
		{
			return "StructureJunctionObject";
		}
		if (normalizedName.Contains("STRUCTUREEDGEOBJECT"))
		{
			return "StructureEdgeObject";
		}
		if (normalizedName.Contains("STRUCTUREBOUNDARY"))
		{
			return "StructureBoundary";
		}
		if (normalizedName.Contains("STRUCTUREJUNCTION"))
		{
			return "StructureJunction";
		}
		if (normalizedName.Contains("STRUCTURELINE"))
		{
			return "StructureLine";
		}
		return owningGroupName ?? layerName;
	}

	private static string NormalizeAssociationName(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}
		return value.Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty).ToUpperInvariant();
	}

	private static bool IsSplitAllowedForPlacedFeature(PlacedFeatureContext createdFeature)
	{
		string sourceTableName = ResolveUtilityNetworkTableName(createdFeature?.Template?.GroupLayer, createdFeature?.Layer?.Name);
		return string.Equals(sourceTableName, "ElectricDevice", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsElectricLineLayerContext(LayerSearchContext context)
	{
		if (context?.Layer == null)
		{
			return false;
		}
		string tableName = ResolveUtilityNetworkTableName(context.OwningGroupName, context.Layer.Name);
		return string.Equals(tableName, "ElectricLine", StringComparison.OrdinalIgnoreCase);
	}

	private static async Task<bool> HasNearbyFeatureCandidatesAsync(IEnumerable<string> targetGroupNames, IEnumerable<string> targetLayerNames, Geometry searchGeometry, bool useRuleCatalogSearchScope = false, Func<LayerSearchContext, bool> layerContextPredicate = null)
	{
		if (MapView.Active == null || searchGeometry == null)
		{
			return false;
		}
		List<LayerSearchContext> layerContexts = BuildLayerSearchContexts(targetGroupNames, targetLayerNames, useRuleCatalogSearchScope, layerContextPredicate);
		if (layerContexts.Count == 0)
		{
			return false;
		}
		return await QueuedTask.Run(delegate
		{
			foreach (LayerSearchContext layerContext in layerContexts)
			{
				FeatureLayer layer = layerContext.Layer;
				SpatialReference layerSpatialReference = GetLayerSpatialReference(layer);
				Geometry layerSearchGeometry;
				try
				{
					layerSearchGeometry = ProjectGeometry(searchGeometry, layerSpatialReference);
				}
				catch
				{
					continue;
				}
				SpatialQueryFilter filter = new SpatialQueryFilter
				{
					FilterGeometry = layerSearchGeometry,
					SpatialRelationship = SpatialRelationship.Intersects,
					SubFields = "OBJECTID"
				};
				using RowCursor cursor = layer.Search(filter);
				if (cursor.MoveNext())
				{
					return true;
				}
			}
			return false;
		});
	}

	private static List<LayerSearchContext> BuildLayerSearchContexts(IEnumerable<string> targetGroupNames, IEnumerable<string> targetLayerNames, bool useRuleCatalogSearchScope, Func<LayerSearchContext, bool> layerContextPredicate)
	{
		HashSet<string> targetGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (targetGroupNames != null)
		{
			foreach (string name in targetGroupNames)
			{
				if (!string.IsNullOrWhiteSpace(name))
				{
					targetGroups.Add(name.ToUpperInvariant());
				}
			}
		}
		HashSet<string> targetLayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (targetLayerNames != null)
		{
			foreach (string name in targetLayerNames)
			{
				if (!string.IsNullOrWhiteSpace(name))
				{
					targetLayers.Add(name.ToUpperInvariant());
				}
			}
		}
		if (!useRuleCatalogSearchScope && targetGroups.Count == 0)
		{
			return new List<LayerSearchContext>();
		}
		IEnumerable<FeatureLayer> searchLayers = useRuleCatalogSearchScope
			? MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>()
			: MapMemberLookupService.GetFeatureLayersForGroups(targetGroups);
		List<LayerSearchContext> layerContexts = new List<LayerSearchContext>();
		foreach (FeatureLayer layer in searchLayers)
		{
			if (!useRuleCatalogSearchScope && targetLayers.Count > 0 && !targetLayers.Contains(layer.Name.ToUpperInvariant()))
			{
				continue;
			}
			LayerSearchContext context = new LayerSearchContext
			{
				Layer = layer,
				OwningGroupName = MapMemberLookupService.GetOwningGroupName(layer)
			};
			if (layerContextPredicate != null && !layerContextPredicate(context))
			{
				continue;
			}
			layerContexts.Add(context);
		}
		return layerContexts;
	}

	private static async Task<List<FeatureCandidate>> FindFeatureCandidatesAsync(IEnumerable<string> targetGroupNames, IEnumerable<string> targetLayerNames, Geometry searchGeometry, Geometry sourceGeometry, Func<FeatureLayer, Feature, Geometry, bool> includePredicate, string labelPrefix, bool useRuleCatalogSearchScope = false, int maxCandidates = int.MaxValue, Func<LayerSearchContext, bool> layerContextPredicate = null)
	{
		if (MapView.Active == null || searchGeometry == null)
		{
			return new List<FeatureCandidate>();
		}
		List<LayerSearchContext> layerContexts = BuildLayerSearchContexts(targetGroupNames, targetLayerNames, useRuleCatalogSearchScope, layerContextPredicate);
		if (layerContexts.Count == 0)
		{
			return new List<FeatureCandidate>();
		}
		return await QueuedTask.Run(delegate
		{
			// Optimize: Pre-allocate capacity based on maxCandidates to reduce reallocations
			int estimatedCapacity = Math.Min(maxCandidates, 100);
			List<FeatureCandidate> candidates = new List<FeatureCandidate>(estimatedCapacity);

			foreach (LayerSearchContext layerContext in layerContexts)
			{
				// Early termination: stop searching if we have enough candidates
				if (candidates.Count >= maxCandidates)
				{
					break;
				}

				FeatureLayer layer = layerContext.Layer;
				// Optimize: Cache layer spatial reference to avoid repeated calls
				SpatialReference layerSpatialReference = GetLayerSpatialReference(layer);
				Geometry layerSearchGeometry;
				Geometry projectedSourceGeometry;
				try
				{
					layerSearchGeometry = ProjectGeometry(searchGeometry, layerSpatialReference);
					// Project sourceGeometry once per layer instead of for each feature
					projectedSourceGeometry = ProjectGeometry(sourceGeometry, layerSpatialReference);
				}
				catch (Exception ex)
				{
					LogService.LogException($"Could not project automatic search geometry for layer '{layer?.Name}'.", ex);
					continue;
				}
				SpatialQueryFilter spatialQueryFilter = new SpatialQueryFilter
				{
					FilterGeometry = layerSearchGeometry,
					SpatialRelationship = SpatialRelationship.Intersects
				};
				RowCursor rowCursor;
				try
				{
					rowCursor = layer.Search(spatialQueryFilter);
				}
				catch (Exception ex)
				{
					LogService.LogException($"Automatic candidate search failed for layer '{layer?.Name}'.", ex);
					continue;
				}
				// Optimize: Cache layer name and owning group to avoid repeated property accesses
				string layerName = layer.Name;
				string owningGroupName = layerContext.OwningGroupName;

				using (rowCursor)
			while (rowCursor.MoveNext())
			{
				// Early termination within layer: stop if we have enough candidates
				if (candidates.Count >= maxCandidates)
				{
					break;
				}

				using Feature feature = (Feature)rowCursor.Current;
				Geometry geometry = feature.GetShape();
				if (!includePredicate(layer, feature, geometry))
				{
					continue;
				}
				double distance = GetDistance(projectedSourceGeometry, geometry);
				string label = labelPrefix + ": " + owningGroupName + "/" + layerName + " (OID " + feature.GetObjectID() + ")";
				candidates.Add(new FeatureCandidate
				{
					Layer = layer,
					ObjectID = feature.GetObjectID(),
					Geometry = geometry,
					Label = label,
					Distance = distance
				});
			}
			}
			// Optimize: Use Array.Sort with custom comparer instead of LINQ OrderBy for better performance
			if (candidates.Count > 1)
			{
				candidates.Sort((a, b) =>
				{
					int distanceComparison = a.Distance.CompareTo(b.Distance);
					return distanceComparison != 0 ? distanceComparison : a.ObjectID.CompareTo(b.ObjectID);
				});
			}
			return candidates;
		});
	}

	private static double GetCompatibleDistance(Geometry sourceGeometry, Geometry candidateGeometry)
	{
		if (sourceGeometry == null || candidateGeometry == null)
		{
			return 0.0;
		}
		try
		{
			Geometry comparableSourceGeometry = ProjectGeometry(sourceGeometry, candidateGeometry.SpatialReference);
			return GeometryEngine.Instance.Distance(comparableSourceGeometry, candidateGeometry);
		}
		catch (Exception ex)
		{
			LogService.LogException("Automatic candidate distance could not be calculated with compatible spatial references.", ex);
			return double.MaxValue;
		}
	}

	/// <summary>
	/// Calculates distance between two geometries that are already in the same spatial reference.
	/// Use this instead of GetCompatibleDistance when geometries have already been projected.
	/// </summary>
	private static double GetDistance(Geometry sourceGeometry, Geometry candidateGeometry)
	{
		if (sourceGeometry == null || candidateGeometry == null)
		{
			return 0.0;
		}
		try
		{
			return GeometryEngine.Instance.Distance(sourceGeometry, candidateGeometry);
		}
		catch (Exception ex)
		{
			LogService.LogException("Automatic candidate distance could not be calculated.", ex);
			return double.MaxValue;
		}
	}

	private static SpatialReference GetLayerSpatialReference(FeatureLayer layer)
	{
		return layer?.GetFeatureClass()?.GetDefinition() is FeatureClassDefinition definition
			? definition.GetSpatialReference()
			: null;
	}

	private static Geometry ProjectGeometry(Geometry geometry, SpatialReference outputSpatialReference)
	{
		if (geometry == null || outputSpatialReference == null)
		{
			return geometry;
		}
		SpatialReference inputSpatialReference = geometry.SpatialReference;
		if (inputSpatialReference == null ||
			SpatialReference.AreEqual(inputSpatialReference, outputSpatialReference, ignoreUnknown: true, checkResolution: false))
		{
			return geometry;
		}
		return GeometryEngine.Instance.Project(geometry, outputSpatialReference);
	}

	private static bool IsInteriorSplitCandidate(Geometry candidateGeometry, MapPoint splitPoint)
	{
		if (candidateGeometry is not Polyline candidateLine || splitPoint == null || candidateLine.PointCount < 2)
		{
			return true;
		}
		return !AreSameSplitPoint(candidateLine.Points.First(), splitPoint) && !AreSameSplitPoint(candidateLine.Points.Last(), splitPoint);
	}

	private static string GetFeatureIdentifier(Feature feature, FeatureLayer layer)
	{
		// Optimize: Cache FACILITYID field name per layer to avoid repeated field queries
		string layerUri = layer.URI;
		string facilityIdFieldName;

		lock (CacheLock)
		{
			if (!FacilityIdFieldCache.TryGetValue(layerUri, out facilityIdFieldName))
			{
				// First time for this layer - query and cache the field name
				facilityIdFieldName = layer.GetFeatureClass().GetDefinition().GetFields()
					.Select((Field field) => field.Name)
					.FirstOrDefault((string fieldName) => string.Equals(fieldName, "FACILITYID", StringComparison.OrdinalIgnoreCase));
				FacilityIdFieldCache[layerUri] = facilityIdFieldName; // Cache even if null
			}
		}

		if (!string.IsNullOrWhiteSpace(facilityIdFieldName))
		{
			object facilityId = feature[facilityIdFieldName];
			string facilityIdText = Convert.ToString(facilityId);
			if (!string.IsNullOrWhiteSpace(facilityIdText))
			{
				return "Facility ID " + facilityIdText;
			}
		}
		return "OID " + feature.GetObjectID();
	}

	private static async Task<Geometry> CreateSearchGeometryAsync(Geometry geometry, double searchDistance)
	{
		return await QueuedTask.Run(delegate
		{
			if (geometry == null)
			{
				return null;
			}
			if (searchDistance <= 0.0)
			{
				return geometry;
			}
			return GeometryEngine.Instance.Buffer(geometry, searchDistance);
		});
	}

	private static async Task ExecuteSplitAsync(FeatureLayer targetLayer, long targetObjectId, MapPoint splitPoint)
	{
		await QueuedTask.Run(delegate
		{
			EditOperation editOperation = new EditOperation
			{
				Name = "Split underlying line",
				ProgressMessage = "Splitting underlying line...",
				ShowProgressor = true
			};
			editOperation.Split(targetLayer, targetObjectId, splitPoint);
			if (!editOperation.IsEmpty && !editOperation.Execute())
			{
				throw new InvalidOperationException(string.IsNullOrWhiteSpace(editOperation.ErrorMessage) ? "The line split did not complete." : editOperation.ErrorMessage);
			}
		});
	}

	private static async Task ExecuteAssociationAsync(PlacedFeatureContext createdFeature, FeatureCandidate candidate)
	{
		await QueuedTask.Run(delegate
		{
			EditOperation editOperation = new EditOperation
			{
				Name = "Create association",
				ProgressMessage = "Creating association...",
				ShowProgressor = true
			};
			RowHandle targetHandle = new RowHandle((MapMember)candidate.Layer, candidate.ObjectID);
			RowHandle createdHandle = new RowHandle((MapMember)createdFeature.Layer, createdFeature.ObjectID);
			RowHandle fromHandle = candidate.CreatedFeatureIsAssociationSource ? createdHandle : targetHandle;
			RowHandle toHandle = candidate.CreatedFeatureIsAssociationSource ? targetHandle : createdHandle;
			AssociationDescription associationDescription = (candidate.AssociationType != AssociationType.Containment) ? new AssociationDescription(candidate.AssociationType, fromHandle, toHandle) : new AssociationDescription(AssociationType.Containment, fromHandle, toHandle, !candidate.CreatedFeatureIsAssociationSource && createdFeature.Layer != null);
			editOperation.Create(associationDescription);
			if (!editOperation.IsEmpty && !editOperation.Execute())
			{
				throw new InvalidOperationException(string.IsNullOrWhiteSpace(editOperation.ErrorMessage) ? "The association could not be created." : editOperation.ErrorMessage);
			}
		});
	}

	private static async Task<FeatureCandidate> ChooseCandidateAsync(PlacedFeatureContext createdFeature, string title, string prompt, IReadOnlyList<FeatureCandidate> candidates, bool isSplitCandidate)
	{
		int i = 0;
		while (i >= 0 && i < candidates.Count)
		{
			FeatureCandidate candidate = candidates[i];
			using (await ShowCandidateContextAsync(createdFeature, candidate, isSplitCandidate))
			{
				string candidateLabel = $"{candidate.Label}\n\nCandidate {i + 1} of {candidates.Count}";
				CandidateChoiceDialog dialog = await ShowDialogAsync(() => new CandidateChoiceDialog(title, prompt, candidateLabel, i > 0, i < candidates.Count - 1));
				if (dialog == null)
				{
					return null;
				}
				if (dialog.Result == CandidateChoiceResult.UseCandidate)
				{
					return candidate;
				}
				if (dialog.Result == CandidateChoiceResult.PreviousCandidate)
				{
					i = Math.Max(0, i - 1);
					continue;
				}
				if (dialog.Result == CandidateChoiceResult.NextCandidate)
				{
					i++;
					continue;
				}
				if (dialog.Result == CandidateChoiceResult.Skip)
				{
					return null;
				}
			}
			i++;
		}
		return null;
	}

	private static async Task<IDisposable> ShowCandidateContextAsync(PlacedFeatureContext createdFeature, FeatureCandidate candidate, bool isSplitCandidate)
	{
		if (candidate == null)
		{
			return null;
		}
		return await ShowCandidateContextAsync(createdFeature, new[] { candidate }, isSplitCandidate);
	}

	private static async Task<IDisposable> ShowCandidateContextAsync(PlacedFeatureContext createdFeature, IEnumerable<FeatureCandidate> candidates, bool isSplitCandidate)
	{
		List<FeatureCandidate> candidateList = (candidates ?? Enumerable.Empty<FeatureCandidate>())
			.Where((FeatureCandidate candidate) => candidate?.Layer != null && candidate.ObjectID > 0 && candidate.Geometry != null)
			.ToList();
		if (candidateList.Count == 0)
		{
			return null;
		}
		TemplateEditorSettings settings = AddinConfiguration.Settings;
		if (isSplitCandidate ? settings?.HighlightSplitCandidates != true : settings?.HighlightAssociationCandidates != true)
		{
			return null;
		}
		IDisposable overlay = null;
		await QueuedTask.Run(delegate
		{
			List<IDisposable> overlays = new List<IDisposable>();
			if (MapView.Active != null)
			{
				foreach (FeatureCandidate candidate in candidateList)
				{
					overlays.Add(MapView.Active.AddOverlay(candidate.Geometry, isSplitCandidate ? CreateSplitCandidateSymbol(candidate.Geometry, settings) : CreateAssociationTargetSymbol(candidate.Geometry, settings)));
				}
				if (createdFeature?.Geometry != null)
				{
					overlays.Add(MapView.Active.AddOverlay(createdFeature.Geometry, CreateSourceHintSymbol(createdFeature.Geometry, settings)));
				}
			}
			overlay = new OverlayGroup(overlays);
		});
		return overlay;
	}

	private static CIMSymbolReference CreateSourceHintSymbol(Geometry geometry, TemplateEditorSettings settings)
	{
		CIMColor color = CreateHintColor(settings?.HintSourceColorHex, "#00FF50", 75.0);
		CIMColor outlineColor = CreateHintColor(settings?.HintSourceColorHex, "#00FF50", 100.0);
		if (geometry is Polyline)
		{
			return SymbolFactory.Instance.ConstructLineSymbol(outlineColor, 4.0, SimpleLineStyle.Solid).MakeSymbolReference();
		}
		if (geometry is Polygon)
		{
			return SymbolFactory.Instance.ConstructPolygonSymbol(color, SimpleFillStyle.Solid, SymbolFactory.Instance.ConstructStroke(outlineColor, 2.0, SimpleLineStyle.Solid)).MakeSymbolReference();
		}
		return CreatePointHintSymbol(color, outlineColor, 10.0, 1.5);
	}

	private static CIMSymbolReference CreateSplitCandidateSymbol(Geometry geometry, TemplateEditorSettings settings)
	{
		CIMColor color = CreateHintColor(settings?.HintSplitCandidateColorHex, "#FF0000", 60.0);
		CIMColor outlineColor = CreateHintColor(settings?.HintSplitCandidateColorHex, "#FF0000", 100.0);
		if (geometry is Polyline)
		{
			return SymbolFactory.Instance.ConstructLineSymbol(outlineColor, 5.0, SimpleLineStyle.Solid).MakeSymbolReference();
		}
		if (geometry is Polygon)
		{
			return SymbolFactory.Instance.ConstructPolygonSymbol(color, SimpleFillStyle.Solid, SymbolFactory.Instance.ConstructStroke(outlineColor, 3.0, SimpleLineStyle.Solid)).MakeSymbolReference();
		}
		return CreatePointHintSymbol(color, outlineColor, 12.0, 1.75);
	}

	private static CIMSymbolReference CreateAssociationTargetSymbol(Geometry geometry, TemplateEditorSettings settings)
	{
		CIMColor color = CreateHintColor(settings?.HintAssociationTargetColorHex, "#FF0000", 35.0);
		CIMColor outlineColor = CreateHintColor(settings?.HintAssociationTargetColorHex, "#FF0000", 100.0);
		if (geometry is Polyline)
		{
			return SymbolFactory.Instance.ConstructLineSymbol(outlineColor, 5.0, SimpleLineStyle.Solid).MakeSymbolReference();
		}
		if (geometry is Polygon)
		{
			return SymbolFactory.Instance.ConstructPolygonSymbol(color, SimpleFillStyle.Solid, SymbolFactory.Instance.ConstructStroke(outlineColor, 3.0, SimpleLineStyle.Solid)).MakeSymbolReference();
		}
		return CreatePointHintSymbol(color, outlineColor, 19.0, 2.25);
	}

	private static CIMSymbolReference CreatePointHintSymbol(CIMColor fillColor, CIMColor outlineColor, double size, double outlineWidth)
	{
		CIMPolygonSymbol markerSymbol = SymbolFactory.Instance.ConstructPolygonSymbol(fillColor, SimpleFillStyle.Solid, SymbolFactory.Instance.ConstructStroke(outlineColor, outlineWidth, SimpleLineStyle.Solid));
		CIMPointSymbol pointSymbol = SymbolFactory.Instance.ConstructPointSymbol(fillColor, size, SimpleMarkerStyle.Circle);
		foreach (CIMSymbolLayer layer in pointSymbol.SymbolLayers ?? Array.Empty<CIMSymbolLayer>())
		{
			if (layer is CIMVectorMarker vectorMarker && vectorMarker.MarkerGraphics != null)
			{
				foreach (CIMMarkerGraphic markerGraphic in vectorMarker.MarkerGraphics)
				{
					markerGraphic.Symbol = markerSymbol;
				}
			}
		}
		return pointSymbol.MakeSymbolReference();
	}

	private static CIMColor CreateHintColor(string hexColor, string fallbackHexColor, double alpha)
	{
		string normalized = NormalizeHintColor(hexColor, fallbackHexColor);
		int red = Convert.ToInt32(normalized.Substring(1, 2), 16);
		int green = Convert.ToInt32(normalized.Substring(3, 2), 16);
		int blue = Convert.ToInt32(normalized.Substring(5, 2), 16);
		return ColorFactory.Instance.CreateRGBColor(red, green, blue, alpha);
	}

	private static string NormalizeHintColor(string hexColor, string fallbackHexColor)
	{
		string normalized = (hexColor ?? string.Empty).Trim();
		if (normalized.StartsWith("#", StringComparison.Ordinal))
		{
			normalized = normalized.Substring(1);
		}
		if (normalized.Length != 6 || normalized.Any((char c) => !Uri.IsHexDigit(c)))
		{
			return fallbackHexColor;
		}
		return "#" + normalized;
	}

	private static async Task<MessageBoxResult> ShowMessageBoxAsync(string message, string title, MessageBoxButton buttons)
	{
		return await Application.Current.Dispatcher.InvokeAsync(delegate
		{
			return DialogService.Show(message, title, buttons);
		});
	}

	private static async Task<bool> ShowConfirmationAsync(string message, string title, string confirmLabel = "Yes", string cancelLabel = "No")
	{
		EnhancementConfirmationDialog dialog = await ShowDialogAsync(() => new EnhancementConfirmationDialog(title, message, confirmLabel, cancelLabel));
		return dialog != null;
	}

	private static async Task<TDialog> ShowDialogAsync<TDialog>(Func<TDialog> createDialog) where TDialog : Window
	{
		return await Application.Current.Dispatcher.InvokeAsync(delegate
		{
			TDialog val = createDialog();
			Window mainWindow = Application.Current?.MainWindow;
			if (mainWindow != null && val != mainWindow)
			{
				val.Owner = mainWindow;
			}
			return val.ShowDialog() == true ? val : null;
		});
	}
}

internal sealed class PlacedFeatureContext
{
	public SimpleTemplate Template { get; set; }

	public Geometry Geometry { get; set; }

	public RowToken Token { get; set; }

	public FeatureLayer Layer { get; set; }

	public long ObjectID { get; set; }

	public bool AllowPlacementEnhancements { get; set; } = true;
}

internal sealed class ExistingAssociationPair
{
	public AssociationType AssociationType { get; set; }

	public MapMember FirstMember { get; set; }

	public long FirstObjectID { get; set; }

	public MapMember SecondMember { get; set; }

	public long SecondObjectID { get; set; }

	public bool Matches(AssociationType associationType, MapMember firstMember, long firstObjectID, MapMember secondMember, long secondObjectID)
	{
		if (AssociationType != associationType || firstMember == null || secondMember == null || firstObjectID <= 0 || secondObjectID <= 0)
		{
			return false;
		}
		return MatchesEndpoint(FirstMember, FirstObjectID, firstMember, firstObjectID) &&
			MatchesEndpoint(SecondMember, SecondObjectID, secondMember, secondObjectID) ||
			MatchesEndpoint(FirstMember, FirstObjectID, secondMember, secondObjectID) &&
			MatchesEndpoint(SecondMember, SecondObjectID, firstMember, firstObjectID);
	}

	private static bool MatchesEndpoint(MapMember expectedMember, long expectedObjectID, MapMember actualMember, long actualObjectID)
	{
		return expectedMember == actualMember && expectedObjectID == actualObjectID;
	}
}

internal sealed class FeatureCandidate
{
	public FeatureLayer Layer { get; set; }

	public long ObjectID { get; set; }

	public Geometry Geometry { get; set; }

	public string Label { get; set; }

	public double Distance { get; set; }

	public AssociationType AssociationType { get; set; }

	public bool CreatedFeatureIsAssociationSource { get; set; }
}

internal sealed class LayerSearchContext
{
	public FeatureLayer Layer { get; set; }

	public string OwningGroupName { get; set; }
}

internal sealed class LayerMetadata
{
	public TableDefinition Definition { get; set; }
	public List<Field> Fields { get; set; }
	public string OwningGroupName { get; set; }
}

internal sealed class OverlayGroup : IDisposable
{
	private readonly IReadOnlyList<IDisposable> _overlays;

	public OverlayGroup(IReadOnlyList<IDisposable> overlays)
	{
		_overlays = overlays ?? new List<IDisposable>();
	}

	public void Dispose()
	{
		foreach (IDisposable overlay in _overlays)
		{
			overlay?.Dispose();
		}
	}
}

internal sealed class AssociationPromptResult
{
	public static AssociationPromptResult NotAttempted { get; } = new AssociationPromptResult();

	public FeatureCandidate Candidate { get; private set; }

	public bool WasAttempted { get; private set; }

	public bool WasCreated { get; private set; }

	public static AssociationPromptResult Created(FeatureCandidate candidate)
	{
		return new AssociationPromptResult
		{
			Candidate = candidate,
			WasAttempted = true,
			WasCreated = true
		};
	}

	public static AssociationPromptResult Failed(FeatureCandidate candidate)
	{
		return new AssociationPromptResult
		{
			Candidate = candidate,
			WasAttempted = true
		};
	}
}
