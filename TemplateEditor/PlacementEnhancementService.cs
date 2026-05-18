using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.UtilityNetwork;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

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
		await EnhancementPromptGate.WaitAsync();
		try
		{
			foreach (PlacedFeatureContext createdFeature in createdFeatures)
			{
				if (createdFeature?.Layer == null || createdFeature.Geometry == null || createdFeature.ObjectID <= 0 || !createdFeature.AllowPlacementEnhancements)
				{
					continue;
				}
				await RunEnhancementStepAsync("line split", () => TryPromptForLineSplitAsync(createdFeature, createdFeatures));
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
			await ShowMessageBoxAsync($"The automatic {stepName} step could not be completed.\n\n{ex.Message}\n\nTemplate Editor will continue with the next automatic placement step.", "Template Editor", MessageBoxButton.OK);
		}
	}

	private static async Task WaitForEnhancementSettleAsync()
	{
		await Application.Current.Dispatcher.InvokeAsync(() => { });
		await Task.Delay(EnhancementSettleDelayMilliseconds);
	}

	private static async Task TryPromptForLineSplitAsync(PlacedFeatureContext createdFeature, IReadOnlyList<PlacedFeatureContext> featuresCreatedByOperation)
	{
		TemplateEditorSettings settings = AddinConfiguration.Settings;
		if (settings == null || !settings.EnableLineSplitPrompts)
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
			await TryPromptForSingleSplitPointAsync(createdFeature, featuresCreatedByOperation, (MapPoint)createdFeature.Geometry, "Split underlying line at the placement point?");
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
			bool hasStartCandidate = settings.EnableSplitAtLineStartPoint && (await FindSplitCandidatesAsync(createdFeature, featuresCreatedByOperation, polyline.Points.First())).Count > 0;
			bool hasEndCandidate = settings.EnableSplitAtLineEndPoint && (await FindSplitCandidatesAsync(createdFeature, featuresCreatedByOperation, polyline.Points.Last())).Count > 0;
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
			LineSplitChoiceDialog choiceDialog = await ShowDialogAsync(() => new LineSplitChoiceDialog(availableOptions));
			if (choiceDialog == null)
			{
				return;
			}
			if (choiceDialog.SplitAtStart)
			{
				await TryPromptForSingleSplitPointAsync(createdFeature, featuresCreatedByOperation, polyline.Points.First(), "Split underlying line at the start point?");
			}
			if (choiceDialog.SplitAtEnd)
			{
				await TryPromptForSingleSplitPointAsync(createdFeature, featuresCreatedByOperation, polyline.Points.Last(), "Split underlying line at the end point?");
			}
		}
	}

	private static async Task TryPromptForSingleSplitPointAsync(PlacedFeatureContext createdFeature, IReadOnlyList<PlacedFeatureContext> featuresCreatedByOperation, MapPoint splitPoint, string message)
	{
		List<FeatureCandidate> splitCandidates = await FindSplitCandidatesAsync(createdFeature, featuresCreatedByOperation, splitPoint);
		if (splitCandidates.Count == 0)
		{
			return;
		}
		FeatureCandidate splitCandidate = splitCandidates.Count == 1 ? splitCandidates[0] : await ChooseCandidateAsync("Choose Line To Split", "Review the highlighted line and choose which one to split.", splitCandidates);
		if (splitCandidate == null)
		{
			return;
		}
		await ShowCandidateContextAsync(splitCandidate);
		MessageBoxResult result = await ShowMessageBoxAsync(message, "Template Editor", MessageBoxButton.YesNo);
		if (result != MessageBoxResult.Yes)
		{
			return;
		}
		await ExecuteSplitAsync(splitCandidate.Layer, splitCandidate.ObjectID, splitPoint);
	}

	private static async Task TryPromptForAssociationsAsync(PlacedFeatureContext createdFeature, IReadOnlyList<PlacedFeatureContext> featuresCreatedByOperation)
	{
		TemplateEditorSettings settings = AddinConfiguration.Settings;
		string groupName = createdFeature.Template?.GroupLayer?.ToUpperInvariant();
		if (settings == null || !settings.EnableAssociationPrompts || !settings.AssociationPlacementGroups.Contains(groupName))
		{
			return;
		}
		if (settings.EnableStructuralAttachmentPrompts)
		{
			List<FeatureCandidate> attachmentCandidates = await FindAssociationCandidatesAsync(createdFeature, featuresCreatedByOperation, settings.StructuralAttachmentTargetGroups, AssociationType.Attachment, "Structural attachment");
			AssociationPromptResult attachmentResult = await PromptForAssociationAsync(createdFeature, attachmentCandidates, "Create structural attachment?", "Review the highlighted structural attachment candidate.");
			if (attachmentResult.WasCreated)
			{
				return;
			}
		}
		if (settings.EnableContainmentPointPrompts || settings.EnableContainmentBoundaryPrompts)
		{
			List<FeatureCandidate> containmentCandidates = new List<FeatureCandidate>();
			if (settings.EnableContainmentPointPrompts)
			{
				containmentCandidates.AddRange(await FindAssociationCandidatesAsync(createdFeature, featuresCreatedByOperation, settings.ContainmentPointTargetGroups, AssociationType.Containment, "Containment in structure point"));
			}
			if (settings.EnableContainmentBoundaryPrompts)
			{
				containmentCandidates.AddRange(await FindAssociationCandidatesAsync(createdFeature, featuresCreatedByOperation, settings.ContainmentBoundaryTargetGroups, AssociationType.Containment, "Containment in structure boundary"));
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
		FeatureCandidate chosenCandidate = candidates.Count == 1 ? candidates[0] : await ChooseCandidateAsync("Choose Association Target", chooserPrompt, candidates);
		if (chosenCandidate == null)
		{
			return AssociationPromptResult.NotAttempted;
		}
		await ShowCandidateContextAsync(chosenCandidate);
		MessageBoxResult result = await ShowMessageBoxAsync(singlePrompt + "\n\n" + chosenCandidate.Label, "Template Editor", MessageBoxButton.YesNo);
		if (result != MessageBoxResult.Yes)
		{
			return AssociationPromptResult.NotAttempted;
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

	private static async Task<List<FeatureCandidate>> FindSplitCandidatesAsync(PlacedFeatureContext createdFeature, IReadOnlyList<PlacedFeatureContext> featuresCreatedByOperation, MapPoint point)
	{
		TemplateEditorSettings settings = AddinConfiguration.Settings;
		Geometry searchGeometry = await CreateSearchGeometryAsync(point, settings.SplitSearchDistance);
		return await FindFeatureCandidatesAsync(settings.SplitTargetLineGroups, searchGeometry, createdFeature.Geometry, delegate(FeatureLayer layer, long objectId)
		{
			return !WasCreatedByOperation(featuresCreatedByOperation, layer, objectId);
		}, "Line");
	}

	private static bool WasCreatedByOperation(IReadOnlyList<PlacedFeatureContext> featuresCreatedByOperation, FeatureLayer layer, long objectId)
	{
		return featuresCreatedByOperation != null && featuresCreatedByOperation.Any((PlacedFeatureContext feature) => feature?.Layer == layer && feature.ObjectID == objectId);
	}

	private static async Task<List<FeatureCandidate>> FindAssociationCandidatesAsync(PlacedFeatureContext createdFeature, IReadOnlyList<PlacedFeatureContext> featuresCreatedByOperation, IEnumerable<string> targetGroups, AssociationType associationType, string labelPrefix)
	{
		TemplateEditorSettings settings = AddinConfiguration.Settings;
		Geometry searchGeometry = await CreateSearchGeometryAsync(createdFeature.Geometry, settings.AssociationSearchDistance);
		List<FeatureCandidate> candidates = await FindFeatureCandidatesAsync(targetGroups, searchGeometry, createdFeature.Geometry, delegate(FeatureLayer layer, long objectId)
		{
			return !WasCreatedByOperation(featuresCreatedByOperation, layer, objectId);
		}, labelPrefix);
		foreach (FeatureCandidate candidate in candidates)
		{
			candidate.AssociationType = associationType;
		}
		return candidates;
	}

	private static async Task<List<FeatureCandidate>> FindFeatureCandidatesAsync(IEnumerable<string> targetGroupNames, Geometry searchGeometry, Geometry sourceGeometry, Func<FeatureLayer, long, bool> includePredicate, string labelPrefix)
	{
		if (MapView.Active == null || searchGeometry == null)
		{
			return new List<FeatureCandidate>();
		}
		List<string> targetGroups = (targetGroupNames ?? Enumerable.Empty<string>()).Select((string name) => name?.ToUpperInvariant()).Where((string name) => !string.IsNullOrWhiteSpace(name)).Distinct().ToList();
		if (targetGroups.Count == 0)
		{
			return new List<FeatureCandidate>();
		}
		List<LayerSearchContext> layerContexts = CommonFunctions.GetFeatureLayersForGroups(targetGroups)
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
					if (!includePredicate(layer, feature.GetObjectID()))
					{
						continue;
					}
					Geometry geometry = feature.GetShape();
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
			AssociationDescription associationDescription = (candidate.AssociationType != AssociationType.Containment) ? new AssociationDescription(candidate.AssociationType, targetHandle, createdHandle) : new AssociationDescription(AssociationType.Containment, targetHandle, createdHandle, createdFeature.Layer != null);
			editOperation.Create(associationDescription);
			if (!editOperation.IsEmpty && !editOperation.Execute())
			{
				throw new InvalidOperationException(string.IsNullOrWhiteSpace(editOperation.ErrorMessage) ? "The association could not be created." : editOperation.ErrorMessage);
			}
		});
	}

	private static async Task<FeatureCandidate> ChooseCandidateAsync(string title, string prompt, IReadOnlyList<FeatureCandidate> candidates)
	{
		for (int i = 0; i < candidates.Count; i++)
		{
			FeatureCandidate candidate = candidates[i];
			await ShowCandidateContextAsync(candidate);
			CandidateChoiceDialog dialog = await ShowDialogAsync(() => new CandidateChoiceDialog(title, prompt, candidate.Label, i < candidates.Count - 1));
			if (dialog == null)
			{
				return null;
			}
			if (dialog.Result == CandidateChoiceResult.UseCandidate)
			{
				return candidate;
			}
			if (dialog.Result == CandidateChoiceResult.Skip)
			{
				return null;
			}
		}
		return null;
	}

	private static async Task ShowCandidateContextAsync(FeatureCandidate candidate)
	{
		if (candidate?.Layer == null || candidate.ObjectID <= 0)
		{
			return;
		}
		if (!AddinConfiguration.Settings.HighlightAssociationCandidates)
		{
			return;
		}
		await Application.Current.Dispatcher.InvokeAsync(delegate
		{
			MapView.Active?.FlashFeature((BasicFeatureLayer)candidate.Layer, candidate.ObjectID, false);
		});
		await WaitForEnhancementSettleAsync();
	}

	private static async Task<MessageBoxResult> ShowMessageBoxAsync(string message, string title, MessageBoxButton buttons)
	{
		return await Application.Current.Dispatcher.InvokeAsync(delegate
		{
			return DialogService.Show(message, title, buttons);
		});
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
}

internal sealed class LayerSearchContext
{
	public FeatureLayer Layer { get; set; }

	public string OwningGroupName { get; set; }
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
