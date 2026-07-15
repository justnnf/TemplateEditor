using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
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
		if (AddinConfiguration.Settings?.EnableParallelCopyPrompt != true)
		{
			return false;
		}
		if (!await HasSelectedLineAsync())
		{
			return false;
		}
		TemplateEditorSettings settings = AddinConfiguration.Settings ?? new TemplateEditorSettings();
		double offsetDistance = settings.DefaultParallelCopyOffsetDistance;
		bool leftSide = settings.DefaultParallelCopyLeftSide;
		if (!settings.AutoCreateParallelCopyWhenSelectedLineExists)
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
			if (settings.RememberLastParallelCopyOptions &&
				(Math.Abs(settings.DefaultParallelCopyOffsetDistance - offsetDistance) > 0.0001 || settings.DefaultParallelCopyLeftSide != leftSide))
			{
				TemplateEditorSettings rememberedSettings = settings.Clone();
				rememberedSettings.DefaultParallelCopyOffsetDistance = offsetDistance;
				rememberedSettings.DefaultParallelCopyLeftSide = leftSide;
				AddinConfiguration.ApplySettings(rememberedSettings);
			}
			Geometry offsetGeometry = await CreateFromSelectedLineAsync(offsetDistance, leftSide);
			await CommonFunctions.CreateFeatures(offsetGeometry);
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
			return await QueuedTask.Run(delegate
			{
				return GetSelectedPolylines(layers).Count > 0;
			});
		}
		catch (Exception ex)
		{
			LogService.LogException("Could not inspect selected lines for parallel copy.", ex);
			return false;
		}
	}

	public static async Task<Geometry> CreateFromSelectedLineAsync(double offsetDistance, bool leftSide)
	{
		List<FeatureLayer> layers = GetActiveFeatureLayers();
		return await QueuedTask.Run(delegate
		{
			Polyline sourceLine = CreateSinglePolylineFromSelection(layers);
			if (sourceLine == null)
			{
				throw new InvalidOperationException("Select one or more existing line features to copy parallel from.");
			}
			double signedDistance = leftSide ? -offsetDistance : offsetDistance;
			return OffsetPolyline(sourceLine, signedDistance);
		});
	}

	public static async Task<IDisposable> CreatePreviewOverlayAsync(double offsetDistance, bool leftSide)
	{
		Geometry previewGeometry = await CreateFromSelectedLineAsync(offsetDistance, leftSide);
		return await QueuedTask.Run(delegate
		{
			return MapView.Active?.AddOverlay(previewGeometry, CreatePreviewSymbol());
		});
	}

	private static List<FeatureLayer> GetActiveFeatureLayers()
	{
		return MapView.Active?.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList() ?? new List<FeatureLayer>();
	}

	private static Polyline CreateSinglePolylineFromSelection(IEnumerable<FeatureLayer> layers)
	{
		List<Polyline> selectedLines = GetSelectedPolylines(layers);
		if (selectedLines.Count == 0)
		{
			return null;
		}
		if (selectedLines.Count == 1)
		{
			return selectedLines[0];
		}
		TemplateEditorSettings settings = AddinConfiguration.Settings ?? new TemplateEditorSettings();
		if (!settings.EnableMultiSegmentParallelCopy)
		{
			throw new InvalidOperationException("Multiple selected lines were found. Enable multi-segment parallel copy in Settings or select one line.");
		}
		List<List<MapPoint>> lineParts = selectedLines
			.Select((Polyline line) => line.Points?.ToList())
			.Where((List<MapPoint> points) => points != null && points.Count >= 2)
			.ToList();
		List<MapPoint> stitchedPoints = StitchConnectedLineParts(lineParts, settings.RequireConnectedParallelCopySpan, settings.ParallelCopyEndpointMatchTolerance);
		SpatialReference spatialReference = selectedLines.Select((Polyline line) => line.SpatialReference).FirstOrDefault((SpatialReference reference) => reference != null);
		return PolylineBuilderEx.CreatePolyline(stitchedPoints, spatialReference);
	}

	private static List<Polyline> GetSelectedPolylines(IEnumerable<FeatureLayer> layers)
	{
		List<Polyline> selectedPolylines = new List<Polyline>();
		foreach (FeatureLayer layer in layers ?? Enumerable.Empty<FeatureLayer>())
		{
			List<long> objectIds = GetSelectedObjectIds(layer);
			if (objectIds.Count == 0)
			{
				continue;
			}
			QueryFilter queryFilter = new QueryFilter
			{
				ObjectIDs = objectIds
			};
			using RowCursor rowCursor = layer.Search(queryFilter);
			while (rowCursor.MoveNext())
			{
				using Feature feature = (Feature)rowCursor.Current;
				if (feature.GetShape() is Polyline polyline)
				{
					selectedPolylines.Add(polyline);
				}
			}
		}
		return selectedPolylines;
	}

	private static List<long> GetSelectedObjectIds(FeatureLayer layer)
	{
		if (layer == null)
		{
			return new List<long>();
		}
		Selection selection = ((BasicFeatureLayer)layer).GetSelection();
		return selection?.GetObjectIDs()?.ToList() ?? new List<long>();
	}

	private static List<MapPoint> StitchConnectedLineParts(List<List<MapPoint>> lineParts, bool requireConnectedSpan, double endpointMatchTolerance)
	{
		List<List<MapPoint>> remainingParts = lineParts?
			.Select((List<MapPoint> points) => points.Where((MapPoint point) => point != null).ToList())
			.Where((List<MapPoint> points) => points.Count >= 2)
			.ToList() ?? new List<List<MapPoint>>();
		if (remainingParts.Count == 0)
		{
			throw new InvalidOperationException("The selected lines must contain valid geometry.");
		}
		List<MapPoint> stitchedPoints = new List<MapPoint>(remainingParts[0]);
		remainingParts.RemoveAt(0);
		while (remainingParts.Count > 0)
		{
			int matchingIndex = remainingParts.FindIndex((List<MapPoint> part) => CanConnect(stitchedPoints, part, endpointMatchTolerance));
			if (matchingIndex < 0)
			{
				if (requireConnectedSpan)
				{
					throw new InvalidOperationException("The selected lines must form one connected span before a single parallel copy can be created.");
				}
				matchingIndex = FindNearestPartIndex(stitchedPoints, remainingParts);
			}
			AppendConnectedPart(stitchedPoints, remainingParts[matchingIndex], endpointMatchTolerance);
			remainingParts.RemoveAt(matchingIndex);
		}
		return stitchedPoints;
	}

	private static bool CanConnect(List<MapPoint> stitchedPoints, List<MapPoint> part, double endpointMatchTolerance)
	{
		return stitchedPoints.Count >= 2 &&
			part.Count >= 2 &&
			(AreSameEndpoint(stitchedPoints.Last(), part.First(), endpointMatchTolerance) ||
				AreSameEndpoint(stitchedPoints.Last(), part.Last(), endpointMatchTolerance) ||
				AreSameEndpoint(stitchedPoints.First(), part.Last(), endpointMatchTolerance) ||
				AreSameEndpoint(stitchedPoints.First(), part.First(), endpointMatchTolerance));
	}

	private static void AppendConnectedPart(List<MapPoint> stitchedPoints, List<MapPoint> part, double endpointMatchTolerance)
	{
		if (AreSameEndpoint(stitchedPoints.Last(), part.First(), endpointMatchTolerance))
		{
			stitchedPoints.AddRange(part.Skip(1));
			return;
		}
		if (AreSameEndpoint(stitchedPoints.Last(), part.Last(), endpointMatchTolerance))
		{
			stitchedPoints.AddRange(part.AsEnumerable().Reverse().Skip(1));
			return;
		}
		if (AreSameEndpoint(stitchedPoints.First(), part.Last(), endpointMatchTolerance))
		{
			stitchedPoints.InsertRange(0, part.Take(part.Count - 1));
			return;
		}
		if (AreSameEndpoint(stitchedPoints.First(), part.First(), endpointMatchTolerance))
		{
			stitchedPoints.InsertRange(0, part.AsEnumerable().Reverse().Take(part.Count - 1));
			return;
		}
		if (Distance(stitchedPoints.Last(), part.First()) <= Distance(stitchedPoints.Last(), part.Last()))
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
		double bestDistance = double.MaxValue;
		int bestIndex = 0;
		for (int i = 0; i < remainingParts.Count; i++)
		{
			double distance = Math.Min(Distance(stitchedPoints.Last(), remainingParts[i].First()), Distance(stitchedPoints.Last(), remainingParts[i].Last()));
			if (distance < bestDistance)
			{
				bestDistance = distance;
				bestIndex = i;
			}
		}
		return bestIndex;
	}

	private static bool AreSameEndpoint(MapPoint firstPoint, MapPoint secondPoint, double endpointMatchTolerance)
	{
		if (firstPoint == null || secondPoint == null)
		{
			return false;
		}
		double coordinateTolerance = Math.Max(1e-6, endpointMatchTolerance);
		return Distance(firstPoint, secondPoint) <= coordinateTolerance &&
			(firstPoint.SpatialReference == null ||
				secondPoint.SpatialReference == null ||
				firstPoint.SpatialReference.Wkid == secondPoint.SpatialReference.Wkid);
	}

	private static double Distance(MapPoint firstPoint, MapPoint secondPoint)
	{
		if (firstPoint == null || secondPoint == null)
		{
			return double.MaxValue;
		}
		double x = firstPoint.X - secondPoint.X;
		double y = firstPoint.Y - secondPoint.Y;
		return Math.Sqrt((x * x) + (y * y));
	}

	private static Polyline OffsetPolyline(Polyline sourceLine, double offsetDistance)
	{
		if (sourceLine == null || sourceLine.IsEmpty)
		{
			throw new InvalidOperationException("The selected line must contain a valid geometry.");
		}
		double coordinateOffsetDistance = ConvertMetersToSourceUnits(sourceLine, offsetDistance);
		Geometry offsetGeometry = GeometryEngine.Instance.Offset(sourceLine, coordinateOffsetDistance, OffsetType.Round, Math.Abs(coordinateOffsetDistance));
		if (offsetGeometry is Polyline offsetLine && !offsetLine.IsEmpty)
		{
			return offsetLine;
		}
		throw new InvalidOperationException("ArcGIS Pro could not create a parallel copy from the selected line.");
	}

	private static double ConvertMetersToSourceUnits(Polyline sourceLine, double meters)
	{
		SpatialReference spatialReference = sourceLine?.SpatialReference;
		if (spatialReference == null || spatialReference.IsUnknown)
		{
			throw new InvalidOperationException("The selected line has an unknown coordinate system. Choose a line from a projected coordinate system so the meter offset can be converted correctly.");
		}
		if (!spatialReference.IsProjected || spatialReference.Unit is not LinearUnit linearUnit)
		{
			throw new InvalidOperationException($"The selected line uses '{spatialReference.Name}', which is not a projected linear coordinate system. Project the source layer to a meter/foot-based coordinate system before using parallel copy.");
		}
		return linearUnit.ConvertFromMeters(meters);
	}

	private static CIMSymbolReference CreatePreviewSymbol()
	{
		CIMColor color = ColorFactory.Instance.CreateRGBColor(0.0, 122.0, 255.0, 90.0);
		return SymbolFactory.Instance.ConstructLineSymbol(color, 4.0, SimpleLineStyle.Solid).MakeSymbolReference();
	}

}
