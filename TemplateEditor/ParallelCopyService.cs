using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace TemplateEditor;

internal static class ParallelCopyService
{
	public static async Task<bool> PromptAndCreateIfRequestedAsync()
	{
		TemplateEditorSettings settings = AddinConfiguration.Settings;
		if (settings == null || !settings.EnableParallelCopyPrompt)
		{
			return false;
		}
		if (!(await HasSelectedLineAsync()))
		{
			return false;
		}
		TemplateEditorSettings settings2 = AddinConfiguration.Settings ?? new TemplateEditorSettings();
		double offsetDistance = settings2.DefaultParallelCopyOffsetDistance;
		bool leftSide = settings2.DefaultParallelCopyLeftSide;
		if (!settings2.AutoCreateParallelCopyWhenSelectedLineExists)
		{
			ParallelCopyPromptDialog dialog = ParallelCopyPromptDialog.ShowPrompt(offsetDistance, leftSide);
			if (dialog == null)
			{
				return false;
			}
			offsetDistance = dialog.OffsetDistance;
			leftSide = dialog.LeftSide;
		}
		try
		{
			if (settings2.RememberLastParallelCopyOptions && (Math.Abs(settings2.DefaultParallelCopyOffsetDistance - offsetDistance) > 0.0001 || settings2.DefaultParallelCopyLeftSide != leftSide))
			{
				TemplateEditorSettings rememberedSettings = settings2.Clone();
				rememberedSettings.DefaultParallelCopyOffsetDistance = offsetDistance;
				rememberedSettings.DefaultParallelCopyLeftSide = leftSide;
				AddinConfiguration.ApplySettings(rememberedSettings);
			}
			await CommonFunctions.CreateFeatures(await CreateFromSelectedLineAsync(offsetDistance, leftSide));
			return true;
		}
		catch (Exception ex)
		{
			DialogService.Show(ex.Message, "Template Editor");
			return false;
		}
	}

	public static async Task<bool> HasSelectedLineAsync()
	{
		List<FeatureLayer> layers = GetActiveFeatureLayers();
		try
		{
			return await QueuedTask.Run<bool>((Func<bool>)(() => GetSelectedPolylines(layers).Count > 0), TaskCreationOptions.None);
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			LogService.LogException("Could not inspect selected lines for parallel copy.", ex2);
			return false;
		}
	}

	public static async Task<Geometry> CreateFromSelectedLineAsync(double offsetDistance, bool leftSide)
	{
		List<FeatureLayer> layers = GetActiveFeatureLayers();
		return (Geometry)(object)(await QueuedTask.Run<Polyline>((Func<Polyline>)delegate
		{
			Polyline val = CreateSinglePolylineFromSelection(layers);
			if (val == null)
			{
				throw new InvalidOperationException("Select one or more existing line features to copy parallel from.");
			}
			double offsetDistance2 = (leftSide ? (0.0 - offsetDistance) : offsetDistance);
			return OffsetPolyline(val, offsetDistance2);
		}, TaskCreationOptions.None));
	}

	public static async Task<IDisposable> CreatePreviewOverlayAsync(double offsetDistance, bool leftSide)
	{
		Geometry previewGeometry = await CreateFromSelectedLineAsync(offsetDistance, leftSide);
		return await QueuedTask.Run<IDisposable>((Func<IDisposable>)delegate
		{
			MapView active = MapView.Active;
			return (active != null) ? MappingExtensions.AddOverlay(active, previewGeometry, CreatePreviewSymbol(), -1.0) : null;
		}, TaskCreationOptions.None);
	}

	private static List<FeatureLayer> GetActiveFeatureLayers()
	{
		MapView active = MapView.Active;
		return ((active != null) ? active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList() : null) ?? new List<FeatureLayer>();
	}

	private static Polyline CreateSinglePolylineFromSelection(IEnumerable<FeatureLayer> layers)
	{
		List<Polyline> selectedPolylines = GetSelectedPolylines(layers);
		if (selectedPolylines.Count == 0)
		{
			return null;
		}
		if (selectedPolylines.Count == 1)
		{
			return selectedPolylines[0];
		}
		TemplateEditorSettings templateEditorSettings = AddinConfiguration.Settings ?? new TemplateEditorSettings();
		if (!templateEditorSettings.EnableMultiSegmentParallelCopy)
		{
			throw new InvalidOperationException("Multiple selected lines were found. Enable multi-segment parallel copy in Settings or select one line.");
		}
		List<List<MapPoint>> lineParts = (from line in selectedPolylines
			select ((IEnumerable<MapPoint>)((Multipart)line).Points)?.ToList() into points
			where points != null && points.Count >= 2
			select points).ToList();
		List<MapPoint> list = StitchConnectedLineParts(lineParts, templateEditorSettings.RequireConnectedParallelCopySpan, templateEditorSettings.ParallelCopyEndpointMatchTolerance);
		SpatialReference val = selectedPolylines.Select((Polyline line) => ((Geometry)line).SpatialReference).FirstOrDefault((SpatialReference reference) => reference != null);
		return PolylineBuilderEx.CreatePolyline((IEnumerable<MapPoint>)list, val);
	}

	private static List<Polyline> GetSelectedPolylines(IEnumerable<FeatureLayer> layers)
	{
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Expected O, but got Unknown
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Expected O, but got Unknown
		List<Polyline> list = new List<Polyline>();
		foreach (FeatureLayer item in layers ?? Enumerable.Empty<FeatureLayer>())
		{
			List<long> selectedObjectIds = GetSelectedObjectIds(item);
			if (selectedObjectIds.Count == 0)
			{
				continue;
			}
			QueryFilter val = new QueryFilter
			{
				ObjectIDs = selectedObjectIds
			};
			RowCursor val2 = ((BasicFeatureLayer)item).Search(val, (TimeRange)null, (RangeExtent)null, (CIMFloorFilterSettings)null);
			try
			{
				while (val2.MoveNext())
				{
					Feature val3 = (Feature)val2.Current;
					try
					{
						Geometry shape = val3.GetShape();
						Polyline val4 = (Polyline)(object)((shape is Polyline) ? shape : null);
						if (val4 != null)
						{
							list.Add(val4);
						}
					}
					finally
					{
						((IDisposable)val3)?.Dispose();
					}
				}
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
		}
		return list;
	}

	private static List<long> GetSelectedObjectIds(FeatureLayer layer)
	{
		if (layer == null)
		{
			return new List<long>();
		}
		Selection selection = ((BasicFeatureLayer)layer).GetSelection();
		return ((selection == null) ? null : selection.GetObjectIDs()?.ToList()) ?? new List<long>();
	}

	private static List<MapPoint> StitchConnectedLineParts(List<List<MapPoint>> lineParts, bool requireConnectedSpan, double endpointMatchTolerance)
	{
		List<List<MapPoint>> list = lineParts?.Select((List<MapPoint> points) => points.Where((MapPoint point) => point != null).ToList()).Where((List<MapPoint> points) => points.Count >= 2).ToList() ?? new List<List<MapPoint>>();
		if (list.Count == 0)
		{
			throw new InvalidOperationException("The selected lines must contain valid geometry.");
		}
		List<MapPoint> stitchedPoints = new List<MapPoint>(list[0]);
		list.RemoveAt(0);
		while (list.Count > 0)
		{
			int num = list.FindIndex((List<MapPoint> part) => CanConnect(stitchedPoints, part, endpointMatchTolerance));
			if (num < 0)
			{
				if (requireConnectedSpan)
				{
					throw new InvalidOperationException("The selected lines must form one connected span before a single parallel copy can be created.");
				}
				num = FindNearestPartIndex(stitchedPoints, list);
			}
			AppendConnectedPart(stitchedPoints, list[num], endpointMatchTolerance);
			list.RemoveAt(num);
		}
		return stitchedPoints;
	}

	private static bool CanConnect(List<MapPoint> stitchedPoints, List<MapPoint> part, double endpointMatchTolerance)
	{
		return stitchedPoints.Count >= 2 && part.Count >= 2 && (AreSameEndpoint(stitchedPoints.Last(), part.First(), endpointMatchTolerance) || AreSameEndpoint(stitchedPoints.Last(), part.Last(), endpointMatchTolerance) || AreSameEndpoint(stitchedPoints.First(), part.Last(), endpointMatchTolerance) || AreSameEndpoint(stitchedPoints.First(), part.First(), endpointMatchTolerance));
	}

	private static void AppendConnectedPart(List<MapPoint> stitchedPoints, List<MapPoint> part, double endpointMatchTolerance)
	{
		if (AreSameEndpoint(stitchedPoints.Last(), part.First(), endpointMatchTolerance))
		{
			stitchedPoints.AddRange(part.Skip(1));
		}
		else if (AreSameEndpoint(stitchedPoints.Last(), part.Last(), endpointMatchTolerance))
		{
			stitchedPoints.AddRange(part.AsEnumerable().Reverse().Skip(1));
		}
		else if (AreSameEndpoint(stitchedPoints.First(), part.Last(), endpointMatchTolerance))
		{
			stitchedPoints.InsertRange(0, part.Take(part.Count - 1));
		}
		else if (AreSameEndpoint(stitchedPoints.First(), part.First(), endpointMatchTolerance))
		{
			stitchedPoints.InsertRange(0, part.AsEnumerable().Reverse().Take(part.Count - 1));
		}
		else if (Distance(stitchedPoints.Last(), part.First()) <= Distance(stitchedPoints.Last(), part.Last()))
		{
			stitchedPoints.AddRange(part);
		}
		else
		{
			stitchedPoints.AddRange(part.AsEnumerable().Reverse());
		}
	}

	private static int FindNearestPartIndex(List<MapPoint> stitchedPoints, List<List<MapPoint>> remainingParts)
	{
		double num = double.MaxValue;
		int result = 0;
		for (int i = 0; i < remainingParts.Count; i++)
		{
			double num2 = Math.Min(Distance(stitchedPoints.Last(), remainingParts[i].First()), Distance(stitchedPoints.Last(), remainingParts[i].Last()));
			if (num2 < num)
			{
				num = num2;
				result = i;
			}
		}
		return result;
	}

	private static bool AreSameEndpoint(MapPoint firstPoint, MapPoint secondPoint, double endpointMatchTolerance)
	{
		if (firstPoint == null || secondPoint == null)
		{
			return false;
		}
		double num = Math.Max(1E-06, endpointMatchTolerance);
		return Distance(firstPoint, secondPoint) <= num && (((Geometry)firstPoint).SpatialReference == null || ((Geometry)secondPoint).SpatialReference == null || ((Geometry)firstPoint).SpatialReference.Wkid == ((Geometry)secondPoint).SpatialReference.Wkid);
	}

	private static double Distance(MapPoint firstPoint, MapPoint secondPoint)
	{
		if (firstPoint == null || secondPoint == null)
		{
			return double.MaxValue;
		}
		double num = firstPoint.X - secondPoint.X;
		double num2 = firstPoint.Y - secondPoint.Y;
		return Math.Sqrt(num * num + num2 * num2);
	}

	private static Polyline OffsetPolyline(Polyline sourceLine, double offsetDistance)
	{
		if (sourceLine == null || ((Geometry)sourceLine).IsEmpty)
		{
			throw new InvalidOperationException("The selected line must contain a valid geometry.");
		}
		double num = ConvertMetersToSourceUnits(sourceLine, offsetDistance);
		Geometry val = GeometryEngine.Instance.Offset((Geometry)(object)sourceLine, num, (OffsetType)8, Math.Abs(num));
		Polyline val2 = (Polyline)(object)((val is Polyline) ? val : null);
		if (val2 != null && !((Geometry)val2).IsEmpty)
		{
			return val2;
		}
		throw new InvalidOperationException("ArcGIS Pro could not create a parallel copy from the selected line.");
	}

	private static double ConvertMetersToSourceUnits(Polyline sourceLine, double meters)
	{
		SpatialReference val = ((sourceLine != null) ? ((Geometry)sourceLine).SpatialReference : null);
		if (val == null || val.IsUnknown)
		{
			throw new InvalidOperationException("The selected line has an unknown coordinate system. Choose a line from a projected coordinate system so the meter offset can be converted correctly.");
		}
		if (val.IsProjected)
		{
			Unit unit = val.Unit;
			LinearUnit val2 = (LinearUnit)(object)((unit is LinearUnit) ? unit : null);
			if (val2 != null)
			{
				return val2.ConvertFromMeters(meters);
			}
		}
		throw new InvalidOperationException("The selected line uses '" + val.Name + "', which is not a projected linear coordinate system. Project the source layer to a meter/foot-based coordinate system before using parallel copy.");
	}

	private static CIMSymbolReference CreatePreviewSymbol()
	{
		CIMColor val = ColorFactory.Instance.CreateRGBColor(0.0, 122.0, 255.0, 90.0);
		return SymbolExtensionMethods.MakeSymbolReference((CIMSymbol)(object)SymbolFactory.Instance.ConstructLineSymbol(val, 4.0, (SimpleLineStyle)0));
	}
}
