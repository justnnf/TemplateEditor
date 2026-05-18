using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
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
		ParallelCopyPromptDialog dialog = ParallelCopyPromptDialog.ShowPrompt();
		if (dialog == null)
		{
			return false;
		}
		try
		{
			Geometry offsetGeometry = await CreateFromSelectedLineAsync(dialog.OffsetDistance, dialog.LeftSide);
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
		return await QueuedTask.Run(delegate
		{
			return GetFirstSelectedPolyline(layers) != null;
		});
	}

	public static async Task<Geometry> CreateFromSelectedLineAsync(double offsetDistance, bool leftSide)
	{
		List<FeatureLayer> layers = GetActiveFeatureLayers();
		return await QueuedTask.Run(delegate
		{
			Polyline sourceLine = GetFirstSelectedPolyline(layers);
			if (sourceLine == null)
			{
				throw new InvalidOperationException("Select an existing line feature to copy parallel from.");
			}
			double signedDistance = leftSide ? -offsetDistance : offsetDistance;
			return OffsetPolyline(sourceLine, signedDistance);
		});
	}

	private static List<FeatureLayer> GetActiveFeatureLayers()
	{
		return MapView.Active?.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList() ?? new List<FeatureLayer>();
	}

	private static Polyline GetFirstSelectedPolyline(IEnumerable<FeatureLayer> layers)
	{
		foreach (FeatureLayer layer in layers ?? Enumerable.Empty<FeatureLayer>())
		{
			List<long> objectIds = ((BasicFeatureLayer)layer).GetSelection().GetObjectIDs().ToList();
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
					return polyline;
				}
			}
		}
		return null;
	}

	private static Polyline OffsetPolyline(Polyline sourceLine, double offsetDistance)
	{
		if (sourceLine == null || sourceLine.IsEmpty)
		{
			throw new InvalidOperationException("The selected line must contain a valid geometry.");
		}
		Geometry offsetGeometry = GeometryEngine.Instance.Offset(sourceLine, offsetDistance, OffsetType.Round, Math.Abs(offsetDistance));
		if (offsetGeometry is Polyline offsetLine && !offsetLine.IsEmpty)
		{
			return offsetLine;
		}
		throw new InvalidOperationException("ArcGIS Pro could not create a parallel copy from the selected line.");
	}

}
