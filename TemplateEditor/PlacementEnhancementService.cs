using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.UtilityNetwork;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace TemplateEditor;

internal static class PlacementEnhancementService
{
	private sealed class CandidateSelectionOverlay : IDisposable
	{
		private readonly PlacedFeatureContext _createdFeature;

		private readonly bool _isSplitCandidate;

		private IDisposable _overlay;

		private int _updateVersion;

		public CandidateSelectionOverlay(PlacedFeatureContext createdFeature, bool isSplitCandidate)
		{
			_createdFeature = createdFeature;
			_isSplitCandidate = isSplitCandidate;
		}

		public async Task UpdateAsync(FeatureCandidate candidate)
		{
			int updateVersion = Interlocked.Increment(ref _updateVersion);
			IDisposable nextOverlay = await ShowCandidateSelectionOverlayAsync(_createdFeature, candidate, _isSplitCandidate);
			if (updateVersion != _updateVersion)
			{
				nextOverlay?.Dispose();
				return;
			}
			IDisposable previousOverlay = _overlay;
			_overlay = nextOverlay;
			previousOverlay?.Dispose();
		}

		public void Dispose()
		{
			Interlocked.Increment(ref _updateVersion);
			_overlay?.Dispose();
			_overlay = null;
		}
	}

	private static readonly SemaphoreSlim EnhancementPromptGate = new SemaphoreSlim(1, 1);

	private static readonly Dictionary<string, string> FacilityIdFieldCache = new Dictionary<string, string>(StringComparer.Ordinal);

	private static readonly Dictionary<string, LayerMetadata> LayerMetadataCache = new Dictionary<string, LayerMetadata>(StringComparer.Ordinal);

	private static readonly object CacheLock = new object();

	private static GeometryType GetFeatureLayerShapeType(FeatureLayer layer)
	{
		FeatureClass featureClass = layer?.GetFeatureClass();
		try
		{
			FeatureClassDefinition definition = featureClass?.GetDefinition();
			try
			{
				return (definition != null) ? definition.GetShapeType() : (GeometryType)0;
			}
			finally
			{
				((IDisposable)definition)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)featureClass)?.Dispose();
		}
	}

	internal static Task ClearMetadataCacheAsync()
	{
		if (QueuedTask.OnWorker)
		{
			ClearMetadataCache();
			return Task.CompletedTask;
		}
		return QueuedTask.Run((Action)ClearMetadataCache, TaskCreationOptions.None);
	}

	private static void ClearMetadataCache()
	{
		lock (CacheLock)
		{
			foreach (LayerMetadata metadata in LayerMetadataCache.Values)
			{
				foreach (Field field in metadata.Fields ?? Enumerable.Empty<Field>())
				{
					((IDisposable)field)?.Dispose();
				}
				((IDisposable)metadata.Definition)?.Dispose();
			}
			LayerMetadataCache.Clear();
			FacilityIdFieldCache.Clear();
		}
	}

	public static async Task ApplyPostPlacementEnhancementsAsync(IReadOnlyList<PlacedFeatureContext> createdFeatures, IReadOnlyList<ExistingAssociationPair> existingAssociations = null)
	{
		if (createdFeatures == null || createdFeatures.Count == 0)
		{
			return;
		}
		if (await EnhancementPromptGate.WaitAsync(TimeSpan.FromSeconds(30.0)))
		{
			try
			{
				HashSet<string> processedSplitPointKeys = new HashSet<string>(StringComparer.Ordinal);
				foreach (PlacedFeatureContext createdFeature in createdFeatures)
				{
					if (createdFeature?.Layer != null && createdFeature.Geometry != null && createdFeature.ObjectID > 0 && createdFeature.AllowPlacementEnhancements)
					{
						await RunEnhancementStepAsync("line split", () => TryPromptForLineSplitAsync(createdFeature, createdFeatures, processedSplitPointKeys));
						await WaitForEnhancementSettleAsync();
						await RunEnhancementStepAsync("association", () => TryPromptForAssociationsAsync(createdFeature, createdFeatures, existingAssociations));
						await WaitForEnhancementSettleAsync();
					}
				}
				return;
			}
			finally
			{
				EnhancementPromptGate.Release();
			}
		}
		await ShowMessageBoxAsync("Automatic split and association prompts were skipped because another Template Editor placement prompt is still open. Finish the open prompt, then review the placed feature's associations before continuing.", "Template Editor", MessageBoxButton.OK);
	}

	private static async Task RunEnhancementStepAsync(string stepName, Func<Task> action)
	{
		try
		{
			await action();
		}
		catch (Exception ex)
		{
			TemplateEditorSettings settings = AddinConfiguration.Settings;
			if (settings != null && !settings.ShowAutomaticStepDiagnostics)
			{
				return;
			}
			await ShowMessageBoxAsync($"The automatic {stepName} step could not be completed.\n\n{ex.Message}\n\nTemplate Editor will continue with the next automatic placement step.", "Template Editor", MessageBoxButton.OK);
		}
	}

	private static async Task WaitForEnhancementSettleAsync()
	{
		await ((DispatcherObject)Application.Current).Dispatcher.InvokeAsync((Action)delegate
		{
		});
	}

	private static async Task TryPromptForLineSplitAsync(PlacedFeatureContext createdFeature, IReadOnlyList<PlacedFeatureContext> featuresCreatedByOperation, HashSet<string> processedSplitPointKeys)
	{
		TemplateEditorSettings settings = AddinConfiguration.Settings;
		if (settings == null || !settings.EnableLineSplitPrompts || string.Equals(settings.SplitPromptMode, "Never", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		string groupName = createdFeature.Template?.GroupLayer?.ToUpperInvariant();
		GeometryType createdShapeType = await QueuedTask.Run<GeometryType>((Func<GeometryType>)delegate
		{
			return GetFeatureLayerShapeType(createdFeature.Layer);
		}, TaskCreationOptions.None);
		if (GeometryTypeHelper.IsPoint(createdShapeType))
		{
			if (settings.EnablePointPlacementSplitPrompt && settings.SplitPointPlacementGroups.Contains(groupName))
			{
				MapPoint splitAnchor = createdFeature.SplitPointOverride ?? createdFeature.Geometry as MapPoint;
				if (splitAnchor != null)
				{
					await TryPromptForSingleSplitPointAsync(createdFeature, featuresCreatedByOperation, processedSplitPointKeys, splitAnchor, "Split underlying line at the original insert point?");
				}
			}
		}
		else
		{
			if (!GeometryTypeHelper.IsPolyline(createdShapeType))
			{
				return;
			}
			if (!settings.EnableLineEndpointSplitPrompt || !settings.SplitLinePlacementGroups.Contains(groupName))
			{
				return;
			}
			Geometry geometry = createdFeature.Geometry;
			Polyline polyline = (Polyline)(object)((geometry is Polyline) ? geometry : null);
			if (polyline == null || ((Geometry)polyline).PointCount < 2)
			{
				return;
			}
			List<string> availableOptions = new List<string>();
			MapPoint startPoint = ((IEnumerable<MapPoint>)((Multipart)polyline).Points).First();
			MapPoint endPoint = ((IEnumerable<MapPoint>)((Multipart)polyline).Points).Last();
			List<FeatureCandidate> list = ((!settings.EnableSplitAtLineStartPoint || (settings.SuppressDuplicateSplitPrompts && WasSplitPointProcessed(processedSplitPointKeys, startPoint))) ? new List<FeatureCandidate>() : (await FindSplitCandidatesAsync(createdFeature, featuresCreatedByOperation, startPoint)));
			List<FeatureCandidate> startCandidates = list;
			List<FeatureCandidate> list2 = ((!settings.EnableSplitAtLineEndPoint || (settings.SuppressDuplicateSplitPrompts && WasSplitPointProcessed(processedSplitPointKeys, endPoint))) ? new List<FeatureCandidate>() : (await FindSplitCandidatesAsync(createdFeature, featuresCreatedByOperation, endPoint)));
			List<FeatureCandidate> endCandidates = list2;
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
			if (choiceDialog != null)
			{
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
	}

	private static async Task TryPromptForSingleSplitPointAsync(PlacedFeatureContext createdFeature, IReadOnlyList<PlacedFeatureContext> featuresCreatedByOperation, HashSet<string> processedSplitPointKeys, MapPoint splitPoint, string message, List<FeatureCandidate> splitCandidates = null)
	{
		TemplateEditorSettings settings = AddinConfiguration.Settings;
		if ((settings?.SuppressDuplicateSplitPrompts ?? false) && WasSplitPointProcessed(processedSplitPointKeys, splitPoint))
		{
			return;
		}
		List<FeatureCandidate> list = splitCandidates;
		List<FeatureCandidate> list2 = list;
		if (list2 == null)
		{
			List<FeatureCandidate> list3;
			splitCandidates = (list3 = await FindSplitCandidatesAsync(createdFeature, featuresCreatedByOperation, splitPoint));
			list2 = list3;
		}
		_ = list2;
		if (splitCandidates.Count == 0)
		{
			return;
		}
		if (settings?.SuppressDuplicateSplitPrompts ?? false)
		{
			TrackProcessedSplitPoint(processedSplitPointKeys, splitPoint);
		}
		FeatureCandidate featureCandidate = ((splitCandidates.Count != 1) ? (await ChooseCandidateAsync(createdFeature, "Choose Line To Split", "Review the highlighted line and choose which one to split.", splitCandidates, isSplitCandidate: true)) : splitCandidates[0]);
		FeatureCandidate splitCandidate = featureCandidate;
		if (splitCandidate == null)
		{
			return;
		}
		if (splitCandidates.Count != 1 || !string.Equals(settings?.SplitPromptMode, "AutoWhenOne", StringComparison.OrdinalIgnoreCase))
		{
			using (await ShowCandidateContextAsync(createdFeature, splitCandidate, isSplitCandidate: true))
			{
				if (!(await ShowConfirmationAsync(message, "Template Editor", "Split Line", "Skip")))
				{
					return;
				}
			}
		}
		await ExecuteSplitAsync(splitCandidate.Layer, splitCandidate.ObjectID, splitPoint);
	}

	private static string GetSplitPointKey(MapPoint point)
	{
		if (point == null)
		{
			return null;
		}
		SpatialReference spatialReference = ((Geometry)point).SpatialReference;
		int value = ((spatialReference != null) ? spatialReference.Wkid : 0);
		long value2 = (long)Math.Round(point.X * 1000000.0);
		long value3 = (long)Math.Round(point.Y * 1000000.0);
		return $"{value2}|{value3}|{value}";
	}

	private static void TrackProcessedSplitPoint(HashSet<string> processedSplitPointKeys, MapPoint splitPoint)
	{
		if (processedSplitPointKeys != null && splitPoint != null)
		{
			string splitPointKey = GetSplitPointKey(splitPoint);
			if (splitPointKey != null)
			{
				processedSplitPointKeys.Add(splitPointKey);
			}
		}
	}

	private static bool WasSplitPointProcessed(HashSet<string> processedSplitPointKeys, MapPoint splitPoint)
	{
		if (splitPoint == null || processedSplitPointKeys == null)
		{
			return false;
		}
		string splitPointKey = GetSplitPointKey(splitPoint);
		return splitPointKey != null && processedSplitPointKeys.Contains(splitPointKey);
	}

	private static bool AreSameSplitPoint(MapPoint firstPoint, MapPoint secondPoint)
	{
		if (firstPoint == null || secondPoint == null)
		{
			return false;
		}
		return Math.Abs(firstPoint.X - secondPoint.X) <= 1E-06 && Math.Abs(firstPoint.Y - secondPoint.Y) <= 1E-06 && (((Geometry)firstPoint).SpatialReference == null || ((Geometry)secondPoint).SpatialReference == null || ((Geometry)firstPoint).SpatialReference.Wkid == ((Geometry)secondPoint).SpatialReference.Wkid);
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
		bool createdFeatureIsLine = GeometryTypeHelper.IsPolyline(await QueuedTask.Run<GeometryType>((Func<GeometryType>)delegate
		{
			return GetFeatureLayerShapeType(createdFeature.Layer);
		}, TaskCreationOptions.None));
		if (string.Equals(settings.AssociationPromptMode, "Never", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		if (useRuleCatalogSearchScope)
		{
			if (settings.EnableStructuralAttachmentPrompts)
			{
				await PromptForMultipleAssociationsAsync(candidates: ExcludeExistingAssociationCandidates(candidates: await FindAssociationCandidatesAsync(createdFeature, featuresCreatedByOperation, settings.AssociationPlacementGroups, Enumerable.Empty<string>(), settings.StructuralAttachmentSearchDistance, (AssociationType)3, "Structural attachment", createdFeatureIsAssociationSource: true), createdFeature: createdFeature, existingAssociations: existingAssociations), createdFeature: createdFeature, title: "Create structural attachments?", prompt: "Create structural attachment associations for the nearby eligible features?");
			}
			if (settings.EnableContainmentBoundaryPrompts && IsLineOrPolygonGeometry(createdFeature.Geometry))
			{
				await PromptForMultipleAssociationsAsync(candidates: ExcludeExistingAssociationCandidates(candidates: await FindAssociationCandidatesAsync(createdFeature, featuresCreatedByOperation, settings.AssociationPlacementGroups, Enumerable.Empty<string>(), settings.ContainmentBoundarySearchDistance, (AssociationType)2, "Contain in this structure container", createdFeatureIsAssociationSource: true, IsLineOrPointGeometry), createdFeature: createdFeature, existingAssociations: existingAssociations), createdFeature: createdFeature, title: "Create containment associations?", prompt: "Create containment associations for the nearby eligible features?");
			}
		}
		if (!useRuleCatalogSearchScope && !settings.AssociationPlacementGroups.Contains(groupName))
		{
			if (settings.EnableStructuralAttachmentPrompts && settings.StructuralAttachmentTargetGroups.Contains(groupName))
			{
				await PromptForMultipleAssociationsAsync(candidates: ExcludeExistingAssociationCandidates(candidates: await FindAssociationCandidatesAsync(createdFeature, featuresCreatedByOperation, settings.AssociationPlacementGroups, Enumerable.Empty<string>(), settings.StructuralAttachmentSearchDistance, (AssociationType)3, "Structural attachment", createdFeatureIsAssociationSource: true), createdFeature: createdFeature, existingAssociations: existingAssociations), createdFeature: createdFeature, title: "Create structural attachments?", prompt: "Create structural attachment associations for the nearby eligible features?");
			}
			if (settings.EnableContainmentBoundaryPrompts && IsContainmentContainerTarget(createdFeature, settings))
			{
				await PromptForMultipleAssociationsAsync(candidates: ExcludeExistingAssociationCandidates(candidates: await FindAssociationCandidatesAsync(createdFeature, featuresCreatedByOperation, settings.AssociationPlacementGroups, Enumerable.Empty<string>(), settings.ContainmentBoundarySearchDistance, (AssociationType)2, "Contain in this structure container", createdFeatureIsAssociationSource: true, IsLineOrPointGeometry), createdFeature: createdFeature, existingAssociations: existingAssociations), createdFeature: createdFeature, title: "Create containment associations?", prompt: "Create containment associations for the nearby eligible features?");
			}
		}
		else if ((!settings.EnableStructuralAttachmentPrompts || (createdFeatureIsLine && !settings.EnableLineAssociationPrompts && !settings.EnableLineStructuralAttachmentPrompts) || !(await PromptForAssociationAsync(candidates: ExcludeExistingAssociationCandidates(candidates: MergeUniqueCandidates(await FindAssociationCandidatesAsync(createdFeature, featuresCreatedByOperation, settings.StructuralAttachmentTargetGroups, settings.StructuralAttachmentTargetLayerNames, settings.StructuralAttachmentSearchDistance, (AssociationType)3, "Structural attachment"), await FindSelectedAssociationCandidatesAsync(createdFeature, featuresCreatedByOperation, (AssociationType)3, "Selected structural attachment")), createdFeature: createdFeature, existingAssociations: existingAssociations), createdFeature: createdFeature, singlePrompt: "Create structural attachment?", chooserPrompt: "Review the highlighted structural attachment candidate.")).WasCreated || !settings.StopAfterFirstSuccessfulAssociation) && (!settings.EnableJunctionJunctionConnectivityPrompts || createdFeatureIsLine || !(await PromptForAssociationAsync(candidates: ExcludeExistingAssociationCandidates(candidates: MergeUniqueCandidates(await FindAssociationCandidatesAsync(createdFeature, featuresCreatedByOperation, settings.JunctionJunctionConnectivityTargetGroups, settings.JunctionJunctionConnectivityTargetLayerNames, settings.JunctionJunctionConnectivitySearchDistance, (AssociationType)1, "Junction-junction connectivity"), await FindSelectedAssociationCandidatesAsync(createdFeature, featuresCreatedByOperation, (AssociationType)1, "Selected junction")), createdFeature: createdFeature, existingAssociations: existingAssociations), createdFeature: createdFeature, singlePrompt: "Create junction-junction connectivity association?", chooserPrompt: "Review the highlighted junction-junction connectivity candidate.")).WasCreated || !settings.StopAfterFirstSuccessfulAssociation) && (settings.EnableContainmentPointPrompts || settings.EnableContainmentBoundaryPrompts))
		{
			List<FeatureCandidate> containmentCandidates = new List<FeatureCandidate>();
			if (settings.EnableContainmentPointPrompts && (!createdFeatureIsLine || settings.EnableLineAssociationPrompts || settings.EnableLineContainmentPointPrompts))
			{
				List<FeatureCandidate> list = containmentCandidates;
				list.AddRange(await FindAssociationCandidatesAsync(createdFeature, featuresCreatedByOperation, settings.ContainmentPointTargetGroups, settings.ContainmentPointTargetLayerNames, settings.ContainmentPointSearchDistance, (AssociationType)2, "Containment in structure point", createdFeatureIsAssociationSource: false, IsPointGeometry));
			}
			if (settings.EnableContainmentBoundaryPrompts && (!createdFeatureIsLine || settings.EnableLineContainmentBoundaryPrompts))
			{
				List<FeatureCandidate> list2 = containmentCandidates;
				list2.AddRange(await FindAssociationCandidatesAsync(createdFeature, featuresCreatedByOperation, settings.ContainmentBoundaryTargetGroups, settings.ContainmentBoundaryTargetLayerNames, settings.ContainmentBoundarySearchDistance, (AssociationType)2, "Containment in structure container", createdFeatureIsAssociationSource: false, IsLineOrPolygonGeometry));
			}
			IEnumerable<FeatureCandidate> first = containmentCandidates;
			await PromptForAssociationAsync(candidates: ExcludeExistingAssociationCandidates(candidates: MergeUniqueCandidates(first, await FindSelectedAssociationCandidatesAsync(createdFeature, featuresCreatedByOperation, (AssociationType)2, "Selected containment target")), createdFeature: createdFeature, existingAssociations: existingAssociations), createdFeature: createdFeature, singlePrompt: "Create containment association?", chooserPrompt: "Review the highlighted containment candidate.");
		}
	}

	private static async Task<List<FeatureCandidate>> FindSelectedAssociationCandidatesAsync(PlacedFeatureContext createdFeature, IReadOnlyList<PlacedFeatureContext> featuresCreatedByOperation, AssociationType associationType, string labelPrefix)
	{
		if (!AssociationRuleCatalog.Current.HasRules || createdFeature?.Layer == null)
		{
			return new List<FeatureCandidate>();
		}
		FeatureLayerInfo createdFeatureInfo = await GetPlacedFeatureInfoAsync(createdFeature);
		if (createdFeatureInfo == null)
		{
			return new List<FeatureCandidate>();
		}
		HashSet<string> createdFeatureKeys = BuildCreatedFeatureKeySet(featuresCreatedByOperation);
		return await QueuedTask.Run<List<FeatureCandidate>>((Func<List<FeatureCandidate>>)delegate
		{
			List<FeatureCandidate> list = new List<FeatureCandidate>();
			MapView active = MapView.Active;
			object obj;
			if (active == null)
			{
				obj = null;
			}
			else
			{
				Map map = active.Map;
				obj = ((map != null) ? map.GetLayersAsFlattenedList().OfType<FeatureLayer>() : null);
			}
			if (obj == null)
			{
				obj = Enumerable.Empty<FeatureLayer>();
			}
			foreach (FeatureLayer item in (IEnumerable<FeatureLayer>)obj)
			{
				List<long> list2 = ((BasicFeatureLayer)item).GetSelection().GetObjectIDs().ToList();
				if (list2.Count != 0)
				{
					RowCursor val = ((BasicFeatureLayer)item).Search(new QueryFilter
					{
						ObjectIDs = list2
					}, (TimeRange)null, (RangeExtent)null, (CIMFloorFilterSettings)null);
					try
					{
						while (val.MoveNext())
						{
							Feature val2 = (Feature)val.Current;
							try
							{
								if (!IsFeatureInSet(createdFeatureKeys, item, ((Row)val2).GetObjectID()) && IsAllowedAssociationCandidate(associationType, item, val2, createdFeatureInfo, createdFeatureIsAssociationSource: false))
								{
									Geometry shape = val2.GetShape();
									string owningGroupName = MapMemberLookupService.GetOwningGroupName(item);
									list.Add(new FeatureCandidate
									{
										Layer = item,
										ObjectID = ((Row)val2).GetObjectID(),
										Geometry = shape,
										Label = $"{labelPrefix}: {owningGroupName}/{((MapMember)item).Name} (OID {((Row)val2).GetObjectID()})",
										Distance = GetCompatibleDistance(createdFeature.Geometry, shape),
										AssociationType = associationType
									});
								}
							}
							finally
							{
								((IDisposable)val2)?.Dispose();
							}
						}
					}
					finally
					{
						((IDisposable)val)?.Dispose();
					}
				}
			}
			return list;
		}, TaskCreationOptions.None);
	}

	private static List<FeatureCandidate> MergeUniqueCandidates(IEnumerable<FeatureCandidate> first, IEnumerable<FeatureCandidate> second)
	{
		return (from @group in (first ?? Enumerable.Empty<FeatureCandidate>()).Concat(second ?? Enumerable.Empty<FeatureCandidate>()).GroupBy<FeatureCandidate, string>(delegate(FeatureCandidate candidate)
			{
				DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(2, 3);
				FeatureLayer layer = candidate.Layer;
				defaultInterpolatedStringHandler.AppendFormatted((layer != null) ? ((MapMember)layer).URI : null);
				defaultInterpolatedStringHandler.AppendLiteral("|");
				defaultInterpolatedStringHandler.AppendFormatted(candidate.ObjectID);
				defaultInterpolatedStringHandler.AppendLiteral("|");
				defaultInterpolatedStringHandler.AppendFormatted<AssociationType>(candidate.AssociationType);
				return defaultInterpolatedStringHandler.ToStringAndClear();
			}, StringComparer.Ordinal)
			select @group.OrderBy((FeatureCandidate candidate) => candidate.Distance).First() into candidate
			orderby candidate.Distance
			select candidate).ToList();
	}

	private static List<FeatureCandidate> ExcludeExistingAssociationCandidates(PlacedFeatureContext createdFeature, List<FeatureCandidate> candidates, IReadOnlyList<ExistingAssociationPair> existingAssociations)
	{
		if (candidates == null || candidates.Count == 0 || existingAssociations == null || existingAssociations.Count == 0)
		{
			return candidates ?? new List<FeatureCandidate>();
		}
		return candidates.Where((FeatureCandidate candidate) => !existingAssociations.Any(delegate(ExistingAssociationPair existingAssociation)
		{
			return existingAssociation.Matches(candidate.AssociationType, (MapMember)(object)createdFeature.Layer, createdFeature.ObjectID, (MapMember)(object)candidate.Layer, candidate.ObjectID);
		})).ToList();
	}

	private static bool IsContainmentContainerTarget(PlacedFeatureContext createdFeature, TemplateEditorSettings settings)
	{
		string item = createdFeature?.Template?.GroupLayer?.ToUpperInvariant();
		if (settings == null || settings.ContainmentBoundaryTargetGroups?.Contains(item) != true)
		{
			return false;
		}
		List<string> list = settings.ContainmentBoundaryTargetLayerNames ?? new List<string>();
		int result;
		if (list.Count != 0)
		{
			FeatureLayer layer = createdFeature.Layer;
			if (!list.Contains((layer == null) ? null : ((MapMember)layer).Name?.ToUpperInvariant()))
			{
				result = (list.Contains(createdFeature.Template?.SubtypeLayer?.ToUpperInvariant()) ? 1 : 0);
				goto IL_00c6;
			}
		}
		result = 1;
		goto IL_00c6;
		IL_00c6:
		return (byte)result != 0;
	}

	private static async Task<AssociationPromptResult> PromptForAssociationAsync(PlacedFeatureContext createdFeature, List<FeatureCandidate> candidates, string singlePrompt, string chooserPrompt)
	{
		if (candidates == null || candidates.Count == 0)
		{
			return AssociationPromptResult.NotAttempted;
		}
		TemplateEditorSettings settings = AddinConfiguration.Settings;
		FeatureCandidate featureCandidate = ((candidates.Count != 1) ? (await ChooseCandidateAsync(createdFeature, "Choose Association Target", chooserPrompt, candidates, isSplitCandidate: false)) : candidates[0]);
		FeatureCandidate chosenCandidate = featureCandidate;
		if (chosenCandidate == null)
		{
			return AssociationPromptResult.NotAttempted;
		}
		if (candidates.Count != 1 || (!string.Equals(settings?.AssociationPromptMode, "AutoWhenOne", StringComparison.OrdinalIgnoreCase) && !string.Equals(settings?.AssociationPromptMode, "ReviewMultipleOnly", StringComparison.OrdinalIgnoreCase)))
		{
			using (await ShowCandidateContextAsync(createdFeature, chosenCandidate, isSplitCandidate: false))
			{
				if (!(await ShowConfirmationAsync(singlePrompt + "\n\n" + chosenCandidate.Label, "Template Editor", "Create Association", "Skip")))
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
			return (await PromptForAssociationAsync(createdFeature, candidates, title, prompt)).WasCreated ? 1 : 0;
		}
		string candidateSummary = string.Join("\n", from featureCandidate in candidates.Take(12)
			select "  - " + featureCandidate.Label);
		string additionalText = ((candidates.Count > 12) ? $"\n  - {candidates.Count - 12} more..." : string.Empty);
		using (await ShowCandidateContextAsync(createdFeature, candidates, isSplitCandidate: false))
		{
			if (!(await ShowConfirmationAsync($"{prompt}\n\n{candidates.Count} candidate(s):\n{candidateSummary}{additionalText}", title, "Create Associations", "Skip")))
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
				failures.Add(candidate.Label + ": " + ex.Message);
			}
		}
		if (failures.Count > 0)
		{
			string displayedFailures = string.Join("\n", failures.Take(8));
			string additionalFailureText = ((failures.Count > 8) ? $"\n\n{failures.Count - 8} more association failure(s) were not shown." : string.Empty);
			await ShowMessageBoxAsync($"{createdCount} association(s) were created, but {failures.Count} failed.\n\n{displayedFailures}{additionalFailureText}", "Template Editor", MessageBoxButton.OK);
		}
		return createdCount;
	}

	private static async Task<List<FeatureCandidate>> FindSplitCandidatesAsync(PlacedFeatureContext createdFeature, IReadOnlyList<PlacedFeatureContext> featuresCreatedByOperation, MapPoint point)
	{
		if (createdFeature == null || point == null)
		{
			return new List<FeatureCandidate>();
		}
		TemplateEditorSettings settings = AddinConfiguration.Settings;
		Geometry searchGeometry = await CreateSearchGeometryAsync((Geometry)(object)point, settings.SplitSearchDistance);
		HashSet<string> createdFeatureKeys = BuildCreatedFeatureKeySet(featuresCreatedByOperation);
		return await FindFeatureCandidatesAsync(settings.SplitTargetLineGroups, settings.SplitTargetLayerNames, searchGeometry, point, (FeatureLayer layer, Feature feature, Geometry geometry) => !IsFeatureInSet(createdFeatureKeys, layer, ((Row)feature).GetObjectID()) && (!settings.SplitOnlyInteriorCandidates || IsInteriorSplitCandidate(geometry, point)), "Line", useRuleCatalogSearchScope: false, settings.MaxSplitCandidatesToReview);
	}

	private static HashSet<string> BuildCreatedFeatureKeySet(IReadOnlyList<PlacedFeatureContext> featuresCreatedByOperation)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		if (featuresCreatedByOperation != null)
		{
			foreach (PlacedFeatureContext item in featuresCreatedByOperation)
			{
				if (item?.Layer != null && item.ObjectID > 0)
				{
					hashSet.Add($"{((MapMember)item.Layer).URI}|{item.ObjectID}");
				}
			}
		}
		return hashSet;
	}

	private static bool IsFeatureInSet(HashSet<string> createdFeatureKeys, FeatureLayer layer, long objectId)
	{
		if (createdFeatureKeys == null || layer == null || objectId <= 0)
		{
			return false;
		}
		string item = $"{((MapMember)layer).URI}|{objectId}";
		return createdFeatureKeys.Contains(item);
	}

	private static bool WasCreatedByOperation(IReadOnlyList<PlacedFeatureContext> featuresCreatedByOperation, FeatureLayer layer, long objectId)
	{
		return featuresCreatedByOperation?.Any((PlacedFeatureContext feature) => feature?.Layer == layer && feature.ObjectID == objectId) ?? false;
	}

	private static async Task<List<FeatureCandidate>> FindAssociationCandidatesAsync(PlacedFeatureContext createdFeature, IReadOnlyList<PlacedFeatureContext> featuresCreatedByOperation, IEnumerable<string> targetGroups, IEnumerable<string> targetLayerNames, double searchDistance, AssociationType associationType, string labelPrefix, bool createdFeatureIsAssociationSource = false, Func<Geometry, bool> geometryPredicate = null)
	{
		Geometry searchGeometry = await CreateSearchGeometryAsync(createdFeature.Geometry, searchDistance);
		bool useRuleCatalogSearchScope = AssociationRuleCatalog.Current.HasRules;
		if (!AssociationRuleCatalog.Current.IsAvailable)
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
				layerContextPredicate = delegate(LayerSearchContext context)
				{
					HashSet<string> hashSet = allowedCounterpartTables;
					string owningGroupName = context.OwningGroupName;
					FeatureLayer layer = context.Layer;
					return hashSet.Contains(NormalizeAssociationName(ResolveUtilityNetworkTableName(owningGroupName, (layer != null) ? ((MapMember)layer).Name : null)));
				};
			}
		}
		HashSet<string> createdFeatureKeys = BuildCreatedFeatureKeySet(featuresCreatedByOperation);
		List<FeatureCandidate> candidates = await FindFeatureCandidatesAsync(targetGroups, targetLayerNames, searchGeometry, createdFeature.Geometry, delegate(FeatureLayer layer, Feature feature, Geometry geometry)
		{
			return (geometryPredicate == null || geometryPredicate(geometry)) && !IsFeatureInSet(createdFeatureKeys, layer, ((Row)feature).GetObjectID()) && IsAllowedAssociationCandidate(associationType, layer, feature, createdFeatureInfo, createdFeatureIsAssociationSource);
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
		return await QueuedTask.Run<FeatureLayerInfo>((Func<FeatureLayerInfo>)delegate
		{
			QueryFilter val = new QueryFilter
			{
				ObjectIDs = new List<long> { createdFeature.ObjectID }
			};
			RowCursor val2 = ((BasicFeatureLayer)createdFeature.Layer).Search(val, (TimeRange)null, (RangeExtent)null, (CIMFloorFilterSettings)null);
			try
			{
				if (val2.MoveNext())
				{
					Feature val3 = (Feature)val2.Current;
					try
					{
						return GetFeatureLayerInfo(createdFeature.Layer, val3);
					}
					finally
					{
						((IDisposable)val3)?.Dispose();
					}
				}
				return (FeatureLayerInfo)null;
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
		}, TaskCreationOptions.None);
	}

	private static bool IsAllowedAssociationCandidate(AssociationType associationType, FeatureLayer candidateLayer, Feature candidateFeature, FeatureLayerInfo createdFeatureInfo, bool createdFeatureIsAssociationSource)
	{
		AssociationRuleCatalog current = AssociationRuleCatalog.Current;
		if (!current.HasRules || createdFeatureInfo == null || candidateFeature == null)
		{
			return true;
		}
		FeatureLayerInfo featureLayerInfo = GetFeatureLayerInfo(candidateLayer, candidateFeature);
		return createdFeatureIsAssociationSource ? current.Allows(associationType, createdFeatureInfo, featureLayerInfo) : current.Allows(associationType, featureLayerInfo, createdFeatureInfo);
	}

	private static FeatureLayerInfo GetFeatureLayerInfo(FeatureLayer layer, Feature feature)
	{
		if (layer == null || feature == null)
		{
			return null;
		}
		string uRI = ((MapMember)layer).URI;
		LayerMetadata value;
		lock (CacheLock)
		{
			if (!LayerMetadataCache.TryGetValue(uRI, out value))
			{
				TableDefinition definition = (TableDefinition)(object)layer.GetFeatureClass().GetDefinition();
				value = new LayerMetadata
				{
					Definition = definition,
					Fields = (((definition == null) ? null : definition.GetFields()?.ToList()) ?? new List<Field>()),
					OwningGroupName = MapMemberLookupService.GetOwningGroupName(layer)
				};
				LayerMetadataCache[uRI] = value;
			}
		}
		string layerAssetGroupName = GetLayerAssetGroupName(layer, value.OwningGroupName, feature, value.Fields, value.Definition);
		string resolvedFieldText = GetResolvedFieldText(feature, value.Fields, value.Definition, "ASSETTYPE");
		return new FeatureLayerInfo
		{
			TableName = ResolveUtilityNetworkTableName(value.OwningGroupName, ((MapMember)layer).Name),
			AssetGroup = layerAssetGroupName,
			AssetType = resolvedFieldText
		};
	}

	private static string GetLayerAssetGroupName(FeatureLayer layer, string owningGroupName, Feature feature, IReadOnlyList<Field> fields, TableDefinition definition)
	{
		if (((Layer)layer).Parent is SubtypeGroupLayer && !string.Equals(((MapMember)layer).Name, owningGroupName, StringComparison.OrdinalIgnoreCase))
		{
			return ((MapMember)layer).Name;
		}
		string resolvedFieldText = GetResolvedFieldText(feature, fields, definition, "ASSETGROUP");
		return string.IsNullOrWhiteSpace(resolvedFieldText) ? ((MapMember)layer).Name : resolvedFieldText;
	}

	private static string GetResolvedFieldText(Feature feature, IReadOnlyList<Field> fields, TableDefinition definition, string requestedFieldName)
	{
		Field val = fields.FirstOrDefault((Field candidate) => string.Equals(candidate.Name, requestedFieldName, StringComparison.OrdinalIgnoreCase));
		if (val == null)
		{
			return null;
		}
		object obj = ((Row)feature)[val.Name];
		if (obj == null || obj == DBNull.Value)
		{
			return null;
		}
		Subtype subtype = FeatureInfoCache.GetSubtype(definition, feature);
		string text = FeatureInfoCache.GetDomainDescription(val.GetDomain(subtype), obj) ?? FeatureInfoCache.GetDomainDescription(val.GetDomain((Subtype)null), obj);
		return string.IsNullOrWhiteSpace(text) ? Convert.ToString(obj) : text;
	}

	private static string ResolveUtilityNetworkTableName(string owningGroupName, string layerName)
	{
		string text = NormalizeAssociationName(owningGroupName) ?? NormalizeAssociationName(layerName);
		if (string.IsNullOrWhiteSpace(text))
		{
			return owningGroupName ?? layerName;
		}
		if (text.Contains("ELECTRICASSEMBLY"))
		{
			return "ElectricAssembly";
		}
		if (text.Contains("ELECTRICDEVICE"))
		{
			return "ElectricDevice";
		}
		if (text.Contains("ELECTRICJUNCTIONOBJECT"))
		{
			return "ElectricJunctionObject";
		}
		if (text.Contains("ELECTRICEDGEOBJECT"))
		{
			return "ElectricEdgeObject";
		}
		if (text.Contains("ELECTRICJUNCTION"))
		{
			return "ElectricJunction";
		}
		if (text.Contains("ELECTRICLINE"))
		{
			return "ElectricLine";
		}
		if (text.Contains("STRUCTUREJUNCTIONOBJECT"))
		{
			return "StructureJunctionObject";
		}
		if (text.Contains("STRUCTUREEDGEOBJECT"))
		{
			return "StructureEdgeObject";
		}
		if (text.Contains("STRUCTUREBOUNDARY"))
		{
			return "StructureBoundary";
		}
		if (text.Contains("STRUCTUREJUNCTION"))
		{
			return "StructureJunction";
		}
		if (text.Contains("STRUCTURELINE"))
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
		return value.Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty)
			.ToUpperInvariant();
	}

	private static bool IsSplitAllowedForPlacedFeature(PlacedFeatureContext createdFeature)
	{
		string owningGroupName = createdFeature?.Template?.GroupLayer;
		object layerName;
		if (createdFeature == null)
		{
			layerName = null;
		}
		else
		{
			FeatureLayer layer = createdFeature.Layer;
			layerName = ((layer != null) ? ((MapMember)layer).Name : null);
		}
		string a = ResolveUtilityNetworkTableName(owningGroupName, (string)layerName);
		return string.Equals(a, "ElectricDevice", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "ElectricJunction", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsElectricLineLayerContext(LayerSearchContext context)
	{
		if (context?.Layer == null)
		{
			return false;
		}
		string a = ResolveUtilityNetworkTableName(context.OwningGroupName, ((MapMember)context.Layer).Name);
		return string.Equals(a, "ElectricLine", StringComparison.OrdinalIgnoreCase);
	}

	private static List<LayerSearchContext> BuildLayerSearchContexts(IEnumerable<string> targetGroupNames, IEnumerable<string> targetLayerNames, bool useRuleCatalogSearchScope, Func<LayerSearchContext, bool> layerContextPredicate)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (targetGroupNames != null)
		{
			foreach (string targetGroupName in targetGroupNames)
			{
				if (!string.IsNullOrWhiteSpace(targetGroupName))
				{
					hashSet.Add(targetGroupName.ToUpperInvariant());
				}
			}
		}
		HashSet<string> hashSet2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (targetLayerNames != null)
		{
			foreach (string targetLayerName in targetLayerNames)
			{
				if (!string.IsNullOrWhiteSpace(targetLayerName))
				{
					hashSet2.Add(targetLayerName.ToUpperInvariant());
				}
			}
		}
		if (!useRuleCatalogSearchScope && hashSet.Count == 0)
		{
			return new List<LayerSearchContext>();
		}
		IEnumerable<FeatureLayer> enumerable = (useRuleCatalogSearchScope ? MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>() : MapMemberLookupService.GetFeatureLayersForGroups(hashSet));
		List<LayerSearchContext> list = new List<LayerSearchContext>();
		foreach (FeatureLayer item in enumerable)
		{
			if (useRuleCatalogSearchScope || hashSet2.Count <= 0 || hashSet2.Contains(((MapMember)item).Name.ToUpperInvariant()))
			{
				LayerSearchContext layerSearchContext = new LayerSearchContext
				{
					Layer = item,
					OwningGroupName = MapMemberLookupService.GetOwningGroupName(item)
				};
				if (layerContextPredicate == null || layerContextPredicate(layerSearchContext))
				{
					list.Add(layerSearchContext);
				}
			}
		}
		return list;
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
		return await QueuedTask.Run<List<FeatureCandidate>>((Func<List<FeatureCandidate>>)delegate
		{
			int capacity = Math.Min(maxCandidates, 100);
			List<FeatureCandidate> list = new List<FeatureCandidate>(capacity);
			foreach (LayerSearchContext item in layerContexts)
			{
				if (list.Count >= maxCandidates)
				{
					break;
				}
				FeatureLayer layer = item.Layer;
				SpatialReference layerSpatialReference = GetLayerSpatialReference(layer);
				Geometry filterGeometry;
				Geometry sourceGeometry2;
				try
				{
					filterGeometry = ProjectGeometry(searchGeometry, layerSpatialReference);
					sourceGeometry2 = ProjectGeometry(sourceGeometry, layerSpatialReference);
				}
				catch (Exception exception)
				{
					LogService.LogException("Could not project automatic search geometry for layer '" + ((layer != null) ? ((MapMember)layer).Name : null) + "'.", exception);
					continue;
				}
				SpatialQueryFilter val = new SpatialQueryFilter
				{
					FilterGeometry = filterGeometry,
					SpatialRelationship = (SpatialRelationship)1
				};
				RowCursor val2;
				try
				{
					val2 = ((BasicFeatureLayer)layer).Search((QueryFilter)(object)val, (TimeRange)null, (RangeExtent)null, (CIMFloorFilterSettings)null);
				}
				catch (Exception exception2)
				{
					LogService.LogException("Automatic candidate search failed for layer '" + ((layer != null) ? ((MapMember)layer).Name : null) + "'.", exception2);
					continue;
				}
				string name = ((MapMember)layer).Name;
				string owningGroupName = item.OwningGroupName;
				RowCursor val3 = val2;
				try
				{
					while (val2.MoveNext() && list.Count < maxCandidates)
					{
						Feature val4 = (Feature)val2.Current;
						try
						{
							Geometry shape = val4.GetShape();
							if (includePredicate(layer, val4, shape))
							{
								double distance = GetDistance(sourceGeometry2, shape);
								string label = labelPrefix + ": " + owningGroupName + "/" + name + " (OID " + ((Row)val4).GetObjectID() + ")";
								list.Add(new FeatureCandidate
								{
									Layer = layer,
									ObjectID = ((Row)val4).GetObjectID(),
									Geometry = shape,
									Label = label,
									Distance = distance
								});
							}
						}
						finally
						{
							((IDisposable)val4)?.Dispose();
						}
					}
				}
				finally
				{
					((IDisposable)val3)?.Dispose();
				}
			}
			if (list.Count > 1)
			{
				list.Sort(delegate(FeatureCandidate a, FeatureCandidate b)
				{
					int num = a.Distance.CompareTo(b.Distance);
					return (num != 0) ? num : a.ObjectID.CompareTo(b.ObjectID);
				});
			}
			return list;
		}, TaskCreationOptions.None);
	}

	private static double GetCompatibleDistance(Geometry sourceGeometry, Geometry candidateGeometry)
	{
		if (sourceGeometry == null || candidateGeometry == null)
		{
			return 0.0;
		}
		try
		{
			Geometry val = ProjectGeometry(sourceGeometry, candidateGeometry.SpatialReference);
			return GeometryEngine.Instance.Distance(val, candidateGeometry);
		}
		catch (Exception exception)
		{
			LogService.LogException("Automatic candidate distance could not be calculated with compatible spatial references.", exception);
			return double.MaxValue;
		}
	}

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
		catch (Exception exception)
		{
			LogService.LogException("Automatic candidate distance could not be calculated.", exception);
			return double.MaxValue;
		}
	}

	private static SpatialReference GetLayerSpatialReference(FeatureLayer layer)
	{
		object obj;
		if (layer == null)
		{
			obj = null;
		}
		else
		{
			FeatureClass featureClass = layer.GetFeatureClass();
			obj = ((featureClass != null) ? featureClass.GetDefinition() : null);
		}
		FeatureClassDefinition val = (FeatureClassDefinition)obj;
		return (val != null) ? val.GetSpatialReference() : null;
	}

	private static Geometry ProjectGeometry(Geometry geometry, SpatialReference outputSpatialReference)
	{
		if (geometry == null || outputSpatialReference == null)
		{
			return geometry;
		}
		SpatialReference spatialReference = geometry.SpatialReference;
		if (spatialReference == null || SpatialReference.AreEqual(spatialReference, outputSpatialReference, true, false))
		{
			return geometry;
		}
		return GeometryEngine.Instance.Project(geometry, outputSpatialReference);
	}

	private static bool IsInteriorSplitCandidate(Geometry candidateGeometry, MapPoint splitPoint)
	{
		Polyline val = (Polyline)(object)((candidateGeometry is Polyline) ? candidateGeometry : null);
		if (val == null || splitPoint == null || ((Geometry)val).PointCount < 2)
		{
			return true;
		}
		return !AreSameSplitPoint(((IEnumerable<MapPoint>)((Multipart)val).Points).First(), splitPoint) && !AreSameSplitPoint(((IEnumerable<MapPoint>)((Multipart)val).Points).Last(), splitPoint);
	}

	private static string GetFeatureIdentifier(Feature feature, FeatureLayer layer)
	{
		string uRI = ((MapMember)layer).URI;
		string value;
		lock (CacheLock)
		{
			if (!FacilityIdFieldCache.TryGetValue(uRI, out value))
			{
				value = (from field in ((TableDefinition)layer.GetFeatureClass().GetDefinition()).GetFields()
					select field.Name).FirstOrDefault((string fieldName) => string.Equals(fieldName, "FACILITYID", StringComparison.OrdinalIgnoreCase));
				FacilityIdFieldCache[uRI] = value;
			}
		}
		if (!string.IsNullOrWhiteSpace(value))
		{
			object obj = ((Row)feature)[value];
			string text = Convert.ToString(obj);
			if (!string.IsNullOrWhiteSpace(text))
			{
				return "Facility ID " + text;
			}
		}
		return "OID " + ((Row)feature).GetObjectID();
	}

	private static async Task<Geometry> CreateSearchGeometryAsync(Geometry geometry, double searchDistance)
	{
		return await QueuedTask.Run<Geometry>((Func<Geometry>)delegate
		{
			if (geometry == null)
			{
				return (Geometry)null;
			}
			return (searchDistance <= 0.0) ? geometry : GeometryEngine.Instance.Buffer(geometry, searchDistance);
		}, TaskCreationOptions.None);
	}

	private static async Task ExecuteSplitAsync(FeatureLayer targetLayer, long targetObjectId, MapPoint splitPoint)
	{
		await QueuedTask.Run((Action)delegate
		{
			EditOperation val = new EditOperation
			{
				Name = "Split underlying line",
				ProgressMessage = "Splitting underlying line...",
				ShowProgressor = true
			};
			val.Split((Layer)(object)targetLayer, targetObjectId, (Geometry)(object)splitPoint);
			if (!val.IsEmpty && !val.Execute())
			{
				throw new InvalidOperationException(string.IsNullOrWhiteSpace(val.ErrorMessage) ? "The line split did not complete." : val.ErrorMessage);
			}
		}, TaskCreationOptions.None);
	}

	private static async Task ExecuteAssociationAsync(PlacedFeatureContext createdFeature, FeatureCandidate candidate)
	{
		await QueuedTask.Run((Action)delegate
		{
			EditOperation val = new EditOperation
			{
				Name = "Create association",
				ProgressMessage = "Creating association...",
				ShowProgressor = true
			};
			RowHandle val2 = new RowHandle((MapMember)(object)candidate.Layer, candidate.ObjectID);
			RowHandle val3 = new RowHandle((MapMember)(object)createdFeature.Layer, createdFeature.ObjectID);
			RowHandle val4 = (candidate.CreatedFeatureIsAssociationSource ? val3 : val2);
			RowHandle val5 = (candidate.CreatedFeatureIsAssociationSource ? val2 : val3);
			AssociationDescription val6 = (((int)candidate.AssociationType != 2) ? new AssociationDescription(candidate.AssociationType, val4, val5) : new AssociationDescription((AssociationType)2, val4, val5, !candidate.CreatedFeatureIsAssociationSource && createdFeature.Layer != null));
			val.Create(val6);
			if (!val.IsEmpty && !val.Execute())
			{
				throw new InvalidOperationException(string.IsNullOrWhiteSpace(val.ErrorMessage) ? "The association could not be created." : val.ErrorMessage);
			}
		}, TaskCreationOptions.None);
	}

	private static async Task<FeatureCandidate> ChooseCandidateAsync(PlacedFeatureContext createdFeature, string title, string prompt, IReadOnlyList<FeatureCandidate> candidates, bool isSplitCandidate)
	{
		if (candidates == null || candidates.Count == 0)
		{
			return null;
		}
		using (await ShowSourceContextAsync(createdFeature))
		{
			CandidateSelectionOverlay selectedOverlay = new CandidateSelectionOverlay(createdFeature, isSplitCandidate);
			try
			{
				List<string> labels = candidates.Select((FeatureCandidate candidate, int index) => $"{index + 1}. {candidate.Label}").ToList();
				await selectedOverlay.UpdateAsync(candidates[0]);
				CandidateChoiceDialog dialog = await ShowDialogAsync(() => new CandidateChoiceDialog(title, prompt + "\n\nCandidates are ordered by proximity. The selected candidate is highlighted in yellow on the map.", labels, delegate(int selectedIndex)
				{
					TaskObservationService.Forget(selectedOverlay.UpdateAsync(candidates[selectedIndex]), "Candidate selection highlight update failed.");
				}));
				return (dialog != null && dialog.Result == CandidateChoiceResult.UseCandidate && dialog.SelectedIndex >= 0 && dialog.SelectedIndex < candidates.Count) ? candidates[dialog.SelectedIndex] : null;
			}
			finally
			{
				if (selectedOverlay != null)
				{
					((IDisposable)selectedOverlay).Dispose();
				}
			}
		}
	}

	private static async Task<IDisposable> ShowCandidateContextAsync(PlacedFeatureContext createdFeature, FeatureCandidate candidate, bool isSplitCandidate)
	{
		if (candidate == null)
		{
			return null;
		}
		return await ShowCandidateContextAsync(createdFeature, new FeatureCandidate[1] { candidate }, isSplitCandidate);
	}

	private static async Task<IDisposable> ShowSourceContextAsync(PlacedFeatureContext createdFeature)
	{
		if (createdFeature?.Geometry == null)
		{
			return null;
		}
		TemplateEditorSettings settings = AddinConfiguration.Settings;
		return await QueuedTask.Run<IDisposable>((Func<IDisposable>)delegate
		{
			MapView active = MapView.Active;
			return (active != null) ? MappingExtensions.AddOverlay(active, createdFeature.Geometry, CreateSourceHintSymbol(createdFeature.Geometry, settings), -1.0) : null;
		}, TaskCreationOptions.None);
	}

	private static async Task<IDisposable> ShowCandidateContextAsync(PlacedFeatureContext createdFeature, IEnumerable<FeatureCandidate> candidates, bool isSplitCandidate)
	{
		List<FeatureCandidate> candidateList = (candidates ?? Enumerable.Empty<FeatureCandidate>()).Where((FeatureCandidate candidate) => candidate?.Layer != null && candidate.ObjectID > 0 && candidate.Geometry != null).ToList();
		if (candidateList.Count == 0)
		{
			return null;
		}
		TemplateEditorSettings settings = AddinConfiguration.Settings;
		int num;
		if (!isSplitCandidate)
		{
			TemplateEditorSettings templateEditorSettings = settings;
			num = ((templateEditorSettings == null || !templateEditorSettings.HighlightAssociationCandidates) ? 1 : 0);
		}
		else
		{
			TemplateEditorSettings templateEditorSettings2 = settings;
			num = ((templateEditorSettings2 == null || !templateEditorSettings2.HighlightSplitCandidates) ? 1 : 0);
		}
		if (num != 0)
		{
			return null;
		}
		IDisposable overlay = null;
		await QueuedTask.Run((Action)delegate
		{
			List<IDisposable> list = new List<IDisposable>();
			if (MapView.Active != null)
			{
				foreach (FeatureCandidate item in candidateList)
				{
					list.Add(MappingExtensions.AddOverlay(MapView.Active, item.Geometry, isSplitCandidate ? CreateSplitCandidateSymbol(item.Geometry, settings) : CreateAssociationTargetSymbol(item.Geometry, settings), -1.0));
				}
				if (createdFeature?.Geometry != null)
				{
					list.Add(MappingExtensions.AddOverlay(MapView.Active, createdFeature.Geometry, CreateSourceHintSymbol(createdFeature.Geometry, settings), -1.0));
				}
			}
			overlay = new OverlayGroup(list);
		}, TaskCreationOptions.None);
		return overlay;
	}

	private static async Task<IDisposable> ShowCandidateSelectionOverlayAsync(PlacedFeatureContext createdFeature, FeatureCandidate candidate, bool isSplitCandidate)
	{
		if (candidate?.Geometry == null)
		{
			return null;
		}
		return await QueuedTask.Run<IDisposable>((Func<IDisposable>)(() => (MapView.Active == null) ? null : MappingExtensions.AddOverlay(MapView.Active, candidate.Geometry, CreateSelectedCandidateSymbol(candidate.Geometry, isSplitCandidate), -1.0)), TaskCreationOptions.None);
	}

	private static CIMSymbolReference CreateSelectedCandidateSymbol(Geometry geometry, bool isSplitCandidate)
	{
		CIMColor val = ColorFactory.Instance.CreateRGBColor(255.0, 215.0, 0.0, isSplitCandidate ? 85.0 : 60.0);
		CIMColor val2 = ColorFactory.Instance.CreateRGBColor(255.0, 215.0, 0.0, 100.0);
		if (geometry is Polyline)
		{
			return SymbolExtensionMethods.MakeSymbolReference((CIMSymbol)(object)SymbolFactory.Instance.ConstructLineSymbol(val2, 7.0, (SimpleLineStyle)0));
		}
		if (geometry is Polygon)
		{
			return SymbolExtensionMethods.MakeSymbolReference((CIMSymbol)(object)SymbolFactory.Instance.ConstructPolygonSymbol(val, (SimpleFillStyle)0, SymbolFactory.Instance.ConstructStroke(val2, 4.0, (SimpleLineStyle)0)));
		}
		return CreatePointHintSymbol(val, val2, 24.0, 3.0);
	}

	private static CIMSymbolReference CreateSourceHintSymbol(Geometry geometry, TemplateEditorSettings settings)
	{
		CIMColor val = CreateHintColor(settings?.HintSourceColorHex, "#00FF50", 75.0);
		CIMColor val2 = CreateHintColor(settings?.HintSourceColorHex, "#00FF50", 100.0);
		if (geometry is Polyline)
		{
			return SymbolExtensionMethods.MakeSymbolReference((CIMSymbol)(object)SymbolFactory.Instance.ConstructLineSymbol(val2, 4.0, (SimpleLineStyle)0));
		}
		if (geometry is Polygon)
		{
			return SymbolExtensionMethods.MakeSymbolReference((CIMSymbol)(object)SymbolFactory.Instance.ConstructPolygonSymbol(val, (SimpleFillStyle)0, SymbolFactory.Instance.ConstructStroke(val2, 2.0, (SimpleLineStyle)0)));
		}
		return CreatePointHintSymbol(val, val2, 10.0, 1.5);
	}

	private static CIMSymbolReference CreateSplitCandidateSymbol(Geometry geometry, TemplateEditorSettings settings)
	{
		CIMColor val = CreateHintColor(settings?.HintSplitCandidateColorHex, "#FF0000", 60.0);
		CIMColor val2 = CreateHintColor(settings?.HintSplitCandidateColorHex, "#FF0000", 100.0);
		if (geometry is Polyline)
		{
			return SymbolExtensionMethods.MakeSymbolReference((CIMSymbol)(object)SymbolFactory.Instance.ConstructLineSymbol(val2, 5.0, (SimpleLineStyle)0));
		}
		if (geometry is Polygon)
		{
			return SymbolExtensionMethods.MakeSymbolReference((CIMSymbol)(object)SymbolFactory.Instance.ConstructPolygonSymbol(val, (SimpleFillStyle)0, SymbolFactory.Instance.ConstructStroke(val2, 3.0, (SimpleLineStyle)0)));
		}
		return CreatePointHintSymbol(val, val2, 12.0, 1.75);
	}

	private static CIMSymbolReference CreateAssociationTargetSymbol(Geometry geometry, TemplateEditorSettings settings)
	{
		CIMColor val = CreateHintColor(settings?.HintAssociationTargetColorHex, "#FF0000", 35.0);
		CIMColor val2 = CreateHintColor(settings?.HintAssociationTargetColorHex, "#FF0000", 100.0);
		if (geometry is Polyline)
		{
			return SymbolExtensionMethods.MakeSymbolReference((CIMSymbol)(object)SymbolFactory.Instance.ConstructLineSymbol(val2, 5.0, (SimpleLineStyle)0));
		}
		if (geometry is Polygon)
		{
			return SymbolExtensionMethods.MakeSymbolReference((CIMSymbol)(object)SymbolFactory.Instance.ConstructPolygonSymbol(val, (SimpleFillStyle)0, SymbolFactory.Instance.ConstructStroke(val2, 3.0, (SimpleLineStyle)0)));
		}
		return CreatePointHintSymbol(val, val2, 19.0, 2.25);
	}

	private static CIMSymbolReference CreatePointHintSymbol(CIMColor fillColor, CIMColor outlineColor, double size, double outlineWidth)
	{
		CIMPolygonSymbol symbol = SymbolFactory.Instance.ConstructPolygonSymbol(fillColor, (SimpleFillStyle)0, SymbolFactory.Instance.ConstructStroke(outlineColor, outlineWidth, (SimpleLineStyle)0));
		CIMPointSymbol val = SymbolFactory.Instance.ConstructPointSymbol(fillColor, size, (SimpleMarkerStyle)0);
		CIMSymbolLayer[] array = ((CIMMultiLayerSymbol)val).SymbolLayers ?? Array.Empty<CIMSymbolLayer>();
		foreach (CIMSymbolLayer val2 in array)
		{
			CIMVectorMarker val3 = (CIMVectorMarker)(object)((val2 is CIMVectorMarker) ? val2 : null);
			if (val3 != null && val3.MarkerGraphics != null)
			{
				CIMMarkerGraphic[] markerGraphics = val3.MarkerGraphics;
				foreach (CIMMarkerGraphic val4 in markerGraphics)
				{
					val4.Symbol = (CIMSymbol)(object)symbol;
				}
			}
		}
		return SymbolExtensionMethods.MakeSymbolReference((CIMSymbol)(object)val);
	}

	private static CIMColor CreateHintColor(string hexColor, string fallbackHexColor, double alpha)
	{
		string text = NormalizeHintColor(hexColor, fallbackHexColor);
		int num = Convert.ToInt32(text.Substring(1, 2), 16);
		int num2 = Convert.ToInt32(text.Substring(3, 2), 16);
		int num3 = Convert.ToInt32(text.Substring(5, 2), 16);
		return ColorFactory.Instance.CreateRGBColor((double)num, (double)num2, (double)num3, alpha);
	}

	private static string NormalizeHintColor(string hexColor, string fallbackHexColor)
	{
		string text = (hexColor ?? string.Empty).Trim();
		if (text.StartsWith("#", StringComparison.Ordinal))
		{
			text = text.Substring(1);
		}
		if (text.Length != 6 || text.Any((char c) => !Uri.IsHexDigit(c)))
		{
			return fallbackHexColor;
		}
		return "#" + text;
	}

	private static async Task<MessageBoxResult> ShowMessageBoxAsync(string message, string title, MessageBoxButton buttons)
	{
		return await ((DispatcherObject)Application.Current).Dispatcher.InvokeAsync<MessageBoxResult>((Func<MessageBoxResult>)(() => DialogService.Show(message, title, buttons)));
	}

	private static async Task<bool> ShowConfirmationAsync(string message, string title, string confirmLabel = "Yes", string cancelLabel = "No")
	{
		return await ShowDialogAsync(() => new EnhancementConfirmationDialog(title, message, confirmLabel, cancelLabel)) != null;
	}

	private static async Task<TDialog> ShowDialogAsync<TDialog>(Func<TDialog> createDialog) where TDialog : Window
	{
		return await ((DispatcherObject)Application.Current).Dispatcher.InvokeAsync<TDialog>((Func<TDialog>)delegate
		{
			TDialog val = createDialog();
			Window window = Application.Current?.MainWindow;
			if (window != null && val != window)
			{
				val.Owner = window;
			}
			return (val.ShowDialog() == true) ? val : null;
		});
	}
}
