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

internal static class PlacementEnhancementService
{
	private const int EnhancementSettleDelayMilliseconds = 350;

	private static readonly SemaphoreSlim EnhancementPromptGate = new SemaphoreSlim(1, 1);

	public static async Task ApplyPostPlacementEnhancementsAsync(IReadOnlyList<PlacedFeatureContext> createdFeatures)
	{
		if (createdFeatures == null || createdFeatures.Count == 0)
		{
			return;
		}
		if (!await EnhancementPromptGate.WaitAsync(TimeSpan.FromSeconds(30)))
		{
			return;
		}
		try
		{
			List<MapPoint> processedSplitPoints = new List<MapPoint>();
			foreach (PlacedFeatureContext createdFeature in createdFeatures)
			{
				if (createdFeature?.Layer == null || createdFeature.Geometry == null || createdFeature.ObjectID <= 0 || !createdFeature.AllowPlacementEnhancements)
				{
					continue;
				}
				await RunEnhancementStepAsync("line split", () => TryPromptForLineSplitAsync(createdFeature, createdFeatures, processedSplitPoints));
				await WaitForEnhancementSettleAsync();
				await RunEnhancementStepAsync("association", () => TryPromptForAssociationsAsync(createdFeature, createdFeatures));
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
		await Task.Delay(EnhancementSettleDelayMilliseconds);
	}

	private static async Task TryPromptForLineSplitAsync(PlacedFeatureContext createdFeature, IReadOnlyList<PlacedFeatureContext> featuresCreatedByOperation, List<MapPoint> processedSplitPoints)
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
			await TryPromptForSingleSplitPointAsync(createdFeature, featuresCreatedByOperation, processedSplitPoints, (MapPoint)createdFeature.Geometry, "Split underlying line at the placement point?");
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
			bool hasStartCandidate = settings.EnableSplitAtLineStartPoint && (!settings.SuppressDuplicateSplitPrompts || !WasSplitPointProcessed(processedSplitPoints, startPoint)) && (await FindSplitCandidatesAsync(createdFeature, featuresCreatedByOperation, startPoint)).Count > 0;
			bool hasEndCandidate = settings.EnableSplitAtLineEndPoint && (!settings.SuppressDuplicateSplitPrompts || !WasSplitPointProcessed(processedSplitPoints, endPoint)) && (await FindSplitCandidatesAsync(createdFeature, featuresCreatedByOperation, endPoint)).Count > 0;
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
					await TryPromptForSingleSplitPointAsync(createdFeature, featuresCreatedByOperation, processedSplitPoints, startPoint, "Split underlying line at the insert/start point?");
				}
				else
				{
					await TryPromptForSingleSplitPointAsync(createdFeature, featuresCreatedByOperation, processedSplitPoints, endPoint, "Split underlying line at this line endpoint?");
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
				await TryPromptForSingleSplitPointAsync(createdFeature, featuresCreatedByOperation, processedSplitPoints, startPoint, "Split underlying line at the insert/start point?");
			}
			if (choiceDialog.SplitAtEnd)
			{
				await TryPromptForSingleSplitPointAsync(createdFeature, featuresCreatedByOperation, processedSplitPoints, endPoint, "Split underlying line at this line endpoint?");
			}
		}
	}

	private static async Task TryPromptForSingleSplitPointAsync(PlacedFeatureContext createdFeature, IReadOnlyList<PlacedFeatureContext> featuresCreatedByOperation, List<MapPoint> processedSplitPoints, MapPoint splitPoint, string message)
	{
		TemplateEditorSettings settings = AddinConfiguration.Settings;
		if (settings?.SuppressDuplicateSplitPrompts == true && WasSplitPointProcessed(processedSplitPoints, splitPoint))
		{
			return;
		}
		List<FeatureCandidate> splitCandidates = await FindSplitCandidatesAsync(createdFeature, featuresCreatedByOperation, splitPoint);
		if (splitCandidates.Count == 0)
		{
			return;
		}
		if (settings?.SuppressDuplicateSplitPrompts == true)
		{
			TrackProcessedSplitPoint(processedSplitPoints, splitPoint);
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
				if (!await ShowConfirmationAsync(message, "Template Editor"))
				{
					return;
				}
			}
		}
		await ExecuteSplitAsync(splitCandidate.Layer, splitCandidate.ObjectID, splitPoint);
	}

	private static void TrackProcessedSplitPoint(List<MapPoint> processedSplitPoints, MapPoint splitPoint)
	{
		if (processedSplitPoints == null || splitPoint == null || WasSplitPointProcessed(processedSplitPoints, splitPoint))
		{
			return;
		}
		processedSplitPoints.Add(splitPoint);
	}

	private static bool WasSplitPointProcessed(IEnumerable<MapPoint> processedSplitPoints, MapPoint splitPoint)
	{
		return splitPoint != null && processedSplitPoints != null && processedSplitPoints.Any((MapPoint processedPoint) => AreSameSplitPoint(processedPoint, splitPoint));
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
				string.Equals(firstPoint.SpatialReference.Wkid.ToString(), secondPoint.SpatialReference.Wkid.ToString(), StringComparison.OrdinalIgnoreCase));
	}

	private static async Task TryPromptForAssociationsAsync(PlacedFeatureContext createdFeature, IReadOnlyList<PlacedFeatureContext> featuresCreatedByOperation)
	{
		TemplateEditorSettings settings = AddinConfiguration.Settings;
		string groupName = createdFeature.Template?.GroupLayer?.ToUpperInvariant();
		if (settings == null || !settings.EnableAssociationPrompts)
		{
			return;
		}
		GeometryType createdShapeType = await QueuedTask.Run(() => createdFeature.Layer.GetFeatureClass().GetDefinition().GetShapeType());
		bool createdFeatureIsLine = GeometryTypeHelper.IsPolyline(createdShapeType);
		if (string.Equals(settings.AssociationPromptMode, "Never", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		if (!settings.AssociationPlacementGroups.Contains(groupName))
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
				await PromptForMultipleAssociationsAsync(
					createdFeature,
					reverseAttachmentCandidates,
					"Create structural attachments?",
					"Create structural attachment associations for the nearby eligible features?");
			}
			return;
		}
		if (settings.EnableStructuralAttachmentPrompts && (!createdFeatureIsLine || settings.EnableLineAssociationPrompts || settings.EnableLineStructuralAttachmentPrompts))
		{
			List<FeatureCandidate> attachmentCandidates = await FindAssociationCandidatesAsync(createdFeature, featuresCreatedByOperation, settings.StructuralAttachmentTargetGroups, settings.StructuralAttachmentTargetLayerNames, settings.StructuralAttachmentSearchDistance, AssociationType.Attachment, "Structural attachment");
			AssociationPromptResult attachmentResult = await PromptForAssociationAsync(createdFeature, attachmentCandidates, "Create structural attachment?", "Review the highlighted structural attachment candidate.");
			if (attachmentResult.WasCreated && settings.StopAfterFirstSuccessfulAssociation)
			{
				return;
			}
		}
		if (settings.EnableJunctionJunctionConnectivityPrompts && !createdFeatureIsLine)
		{
			List<FeatureCandidate> connectivityCandidates = await FindAssociationCandidatesAsync(createdFeature, featuresCreatedByOperation, settings.JunctionJunctionConnectivityTargetGroups, settings.JunctionJunctionConnectivityTargetLayerNames, settings.JunctionJunctionConnectivitySearchDistance, (AssociationType)1, "Junction-junction connectivity");
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
				containmentCandidates.AddRange(await FindAssociationCandidatesAsync(createdFeature, featuresCreatedByOperation, settings.ContainmentPointTargetGroups, settings.ContainmentPointTargetLayerNames, settings.ContainmentPointSearchDistance, AssociationType.Containment, "Containment in structure point"));
			}
			if (settings.EnableContainmentBoundaryPrompts && (!createdFeatureIsLine || settings.EnableLineContainmentBoundaryPrompts))
			{
				containmentCandidates.AddRange(await FindAssociationCandidatesAsync(createdFeature, featuresCreatedByOperation, settings.ContainmentBoundaryTargetGroups, settings.ContainmentBoundaryTargetLayerNames, settings.ContainmentBoundarySearchDistance, AssociationType.Containment, "Containment in structure boundary"));
			}
			await PromptForAssociationAsync(createdFeature, containmentCandidates, "Create containment association?", "Review the highlighted containment candidate.");
		}
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
				if (!await ShowConfirmationAsync(singlePrompt + "\n\n" + chosenCandidate.Label, "Template Editor"))
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
			if (!await ShowConfirmationAsync($"{prompt}\n\n{candidates.Count} candidate(s):\n{candidateSummary}{additionalText}", title))
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
		TemplateEditorSettings settings = AddinConfiguration.Settings;
		Geometry searchGeometry = await CreateSearchGeometryAsync(point, settings.SplitSearchDistance);
		List<FeatureCandidate> candidates = await FindFeatureCandidatesAsync(settings.SplitTargetLineGroups, settings.SplitTargetLayerNames, searchGeometry, createdFeature.Geometry, delegate(FeatureLayer layer, long objectId, Geometry geometry)
		{
			return !WasCreatedByOperation(featuresCreatedByOperation, layer, objectId) &&
				(!settings.SplitOnlyInteriorCandidates || IsInteriorSplitCandidate(geometry, point));
		}, "Line");
		return candidates.Take(settings.MaxSplitCandidatesToReview).ToList();
	}

	private static bool WasCreatedByOperation(IReadOnlyList<PlacedFeatureContext> featuresCreatedByOperation, FeatureLayer layer, long objectId)
	{
		return featuresCreatedByOperation != null && featuresCreatedByOperation.Any((PlacedFeatureContext feature) => feature?.Layer == layer && feature.ObjectID == objectId);
	}

	private static async Task<List<FeatureCandidate>> FindAssociationCandidatesAsync(PlacedFeatureContext createdFeature, IReadOnlyList<PlacedFeatureContext> featuresCreatedByOperation, IEnumerable<string> targetGroups, IEnumerable<string> targetLayerNames, double searchDistance, AssociationType associationType, string labelPrefix, bool createdFeatureIsAssociationSource = false)
	{
		Geometry searchGeometry = await CreateSearchGeometryAsync(createdFeature.Geometry, searchDistance);
		FeatureLayerInfo createdFeatureInfo = await GetPlacedFeatureInfoAsync(createdFeature);
		List<FeatureCandidate> candidates = await FindFeatureCandidatesAsync(targetGroups, targetLayerNames, searchGeometry, createdFeature.Geometry, delegate(FeatureLayer layer, long objectId, Geometry geometry)
		{
			return !WasCreatedByOperation(featuresCreatedByOperation, layer, objectId) &&
				IsAllowedAssociationCandidate(associationType, layer, objectId, createdFeatureInfo, createdFeatureIsAssociationSource);
		}, labelPrefix);
		foreach (FeatureCandidate candidate in candidates)
		{
			candidate.AssociationType = associationType;
			candidate.CreatedFeatureIsAssociationSource = createdFeatureIsAssociationSource;
		}
		return candidates;
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

	private static bool IsAllowedAssociationCandidate(AssociationType associationType, FeatureLayer candidateLayer, long candidateObjectId, FeatureLayerInfo createdFeatureInfo, bool createdFeatureIsAssociationSource)
	{
		AssociationRuleCatalog catalog = AssociationRuleCatalog.Current;
		if (!catalog.HasRules || createdFeatureInfo == null)
		{
			return true;
		}
		QueryFilter queryFilter = new QueryFilter
		{
			ObjectIDs = new List<long> { candidateObjectId }
		};
		using RowCursor rowCursor = candidateLayer.Search(queryFilter);
		if (!rowCursor.MoveNext())
		{
			return false;
		}
		using Feature candidateFeature = (Feature)rowCursor.Current;
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
		TableDefinition definition = layer.GetFeatureClass().GetDefinition() as TableDefinition;
		List<Field> fields = definition?.GetFields()?.ToList() ?? new List<Field>();
		string owningGroupName = CommonFunctions.GetOwningGroupName(layer);
		string assetGroup = GetLayerAssetGroupName(layer, owningGroupName, feature, fields, definition);
		string assetType = GetResolvedFieldText(feature, fields, definition, "ASSETTYPE");
		return new FeatureLayerInfo
		{
			TableName = ResolveUtilityNetworkTableName(owningGroupName, layer.Name),
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
		DataSubtype subtype = GetFeatureSubtype(feature, definition);
		string domainDescription = GetCodedDomainDescription(field.GetDomain(subtype), value) ?? GetCodedDomainDescription(field.GetDomain((DataSubtype)null), value);
		return string.IsNullOrWhiteSpace(domainDescription) ? Convert.ToString(value) : domainDescription;
	}

	private static DataSubtype GetFeatureSubtype(Feature feature, TableDefinition definition)
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
		return definition.GetSubtypes().FirstOrDefault((DataSubtype subtype) =>
			string.Equals(Convert.ToString(subtype.GetCode()), subtypeValueText, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(subtype.GetName(), subtypeValueText, StringComparison.OrdinalIgnoreCase));
	}

	private static string GetCodedDomainDescription(DataDomain domain, object value)
	{
		if (domain is not CodedValueDomain codedValueDomain)
		{
			return null;
		}
		string valueText = Convert.ToString(value);
		foreach (KeyValuePair<object, string> pair in codedValueDomain.GetCodedValuePairs())
		{
			if (string.Equals(Convert.ToString(pair.Key), valueText, StringComparison.OrdinalIgnoreCase))
			{
				return pair.Value;
			}
		}
		return null;
	}

	private static string ResolveUtilityNetworkTableName(string owningGroupName, string layerName)
	{
		string normalizedName = NormalizeAssociationName(owningGroupName) ?? NormalizeAssociationName(layerName);
		if (string.IsNullOrWhiteSpace(normalizedName))
		{
			return owningGroupName ?? layerName;
		}
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

	private static async Task<List<FeatureCandidate>> FindFeatureCandidatesAsync(IEnumerable<string> targetGroupNames, IEnumerable<string> targetLayerNames, Geometry searchGeometry, Geometry sourceGeometry, Func<FeatureLayer, long, Geometry, bool> includePredicate, string labelPrefix)
	{
		if (MapView.Active == null || searchGeometry == null)
		{
			return new List<FeatureCandidate>();
		}
		List<string> targetGroups = (targetGroupNames ?? Enumerable.Empty<string>()).Select((string name) => name?.ToUpperInvariant()).Where((string name) => !string.IsNullOrWhiteSpace(name)).Distinct().ToList();
		List<string> targetLayers = (targetLayerNames ?? Enumerable.Empty<string>()).Select((string name) => name?.ToUpperInvariant()).Where((string name) => !string.IsNullOrWhiteSpace(name)).Distinct().ToList();
		if (targetGroups.Count == 0)
		{
			return new List<FeatureCandidate>();
		}
		List<LayerSearchContext> layerContexts = CommonFunctions.GetFeatureLayersForGroups(targetGroups)
			.Where((FeatureLayer layer) => targetLayers.Count == 0 || targetLayers.Contains(layer.Name.ToUpperInvariant()))
			.Select((FeatureLayer layer) => new LayerSearchContext
			{
				Layer = layer,
				OwningGroupName = CommonFunctions.GetOwningGroupName(layer)
			})
			.ToList();
		return await QueuedTask.Run(delegate
		{
			List<FeatureCandidate> candidates = new List<FeatureCandidate>();
			foreach (LayerSearchContext layerContext in layerContexts)
			{
				FeatureLayer layer = layerContext.Layer;
				SpatialQueryFilter spatialQueryFilter = new SpatialQueryFilter
				{
					FilterGeometry = searchGeometry,
					SpatialRelationship = SpatialRelationship.Intersects
				};
				using RowCursor rowCursor = layer.Search(spatialQueryFilter);
				while (rowCursor.MoveNext())
				{
					using Feature feature = (Feature)rowCursor.Current;
					Geometry geometry = feature.GetShape();
					if (!includePredicate(layer, feature.GetObjectID(), geometry))
					{
						continue;
					}
					double distance = sourceGeometry == null ? 0.0 : GeometryEngine.Instance.Distance(sourceGeometry, geometry);
					string featureIdentifier = GetFeatureIdentifier(feature, layer);
					candidates.Add(new FeatureCandidate
					{
						Layer = layer,
						ObjectID = feature.GetObjectID(),
						Geometry = geometry,
						Label = $"{labelPrefix}: {layerContext.OwningGroupName}/{layer.Name} ({featureIdentifier})",
						Distance = distance
					});
				}
			}
			return candidates.OrderBy((FeatureCandidate candidate) => candidate.Distance).ThenBy((FeatureCandidate candidate) => candidate.Label).ToList();
		});
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
		string facilityIdFieldName = layer.GetFeatureClass().GetDefinition().GetFields()
			.Select((Field field) => field.Name)
			.FirstOrDefault((string fieldName) => string.Equals(fieldName, "FACILITYID", StringComparison.OrdinalIgnoreCase));
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
				Name = "Split underlying line"
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
				Name = "Create association"
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
				if (createdFeature?.Geometry != null)
				{
					overlays.Add(MapView.Active.AddOverlay(createdFeature.Geometry, CreatePlacedFeatureSymbol(createdFeature.Geometry)));
				}
				foreach (FeatureCandidate candidate in candidateList)
				{
					overlays.Add(MapView.Active.AddOverlay(candidate.Geometry, CreateCandidateSymbol(candidate.Geometry, isSplitCandidate)));
				}
			}
			overlay = new OverlayGroup(overlays);
		});
		return overlay;
	}

	private static CIMSymbolReference CreatePlacedFeatureSymbol(Geometry geometry)
	{
		CIMColor color = ColorFactory.Instance.CreateRGBColor(0.0, 255.0, 80.0, 75.0);
		CIMColor outlineColor = ColorFactory.Instance.CreateRGBColor(0.0, 255.0, 80.0, 100.0);
		if (geometry is Polyline)
		{
			return SymbolFactory.Instance.ConstructLineSymbol(outlineColor, 4.0, SimpleLineStyle.Solid).MakeSymbolReference();
		}
		if (geometry is Polygon)
		{
			return SymbolFactory.Instance.ConstructPolygonSymbol(color, SimpleFillStyle.Solid, SymbolFactory.Instance.ConstructStroke(outlineColor, 2.0, SimpleLineStyle.Solid)).MakeSymbolReference();
		}
		return SymbolFactory.Instance.ConstructPointSymbol(outlineColor, 10.0, SimpleMarkerStyle.Circle).MakeSymbolReference();
	}

	private static CIMSymbolReference CreateCandidateSymbol(Geometry geometry, bool isSplitCandidate)
	{
		CIMColor color = ColorFactory.Instance.CreateRGBColor(255.0, 0.0, 0.0, 75.0);
		CIMColor outlineColor = ColorFactory.Instance.CreateRGBColor(255.0, 0.0, 0.0, 100.0);
		if (geometry is Polyline)
		{
			return SymbolFactory.Instance.ConstructLineSymbol(outlineColor, 5.0, SimpleLineStyle.Solid).MakeSymbolReference();
		}
		if (geometry is Polygon)
		{
			return SymbolFactory.Instance.ConstructPolygonSymbol(color, SimpleFillStyle.Solid, SymbolFactory.Instance.ConstructStroke(outlineColor, 3.0, SimpleLineStyle.Solid)).MakeSymbolReference();
		}
		return SymbolFactory.Instance.ConstructPointSymbol(outlineColor, 12.0, SimpleMarkerStyle.Circle).MakeSymbolReference();
	}

	private static async Task<MessageBoxResult> ShowMessageBoxAsync(string message, string title, MessageBoxButton buttons)
	{
		return await Application.Current.Dispatcher.InvokeAsync(delegate
		{
			return DialogService.Show(message, title, buttons);
		});
	}

	private static async Task<bool> ShowConfirmationAsync(string message, string title)
	{
		EnhancementConfirmationDialog dialog = await ShowDialogAsync(() => new EnhancementConfirmationDialog(title, message));
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
