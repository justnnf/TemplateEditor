using System.Threading.Tasks;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;

namespace TemplateEditor;

internal class SketchPolygonTool : PreviewSketchTool
{
	public SketchPolygonTool()
	{
		base.IsSketchTool = true;
		base.SketchType = (SketchGeometryType)4;
		base.SketchOutputMode = (SketchOutputMode)1;
		base.UseSnapping = true;
		base.Cursor = ToolCursorLoader.Load("polygonCursor.cur");
	}

	protected override Task OnToolActivateAsync(bool active)
	{
		return base.OnToolActivateAsync(active);
	}

	protected override async Task<bool> OnSketchCompleteAsync(Geometry geometry)
	{
		// Preview rotation may have supplied an anchor different from the final
		// sketch geometry; use it when present so preview and placement agree.
		await RefreshPlacementRotationAsync();
		Geometry placementGeometry = PlacementAnchorOverride ?? geometry;
		SuspendPreview();
		bool placementSucceeded = false;
		await RunWithPlacementCursorAsync(async delegate
		{
			placementSucceeded = await CommonFunctions.CreateFeatures(placementGeometry, base.RotationDegrees);
		});
		bool returnToSelect = EditorDockpaneViewModel.ShouldReturnToSelectAfterPlacement(placementSucceeded);
		if (returnToSelect)
		{
			ToolReactivationService.ActivateSelectTool();
		}
		bool completed = await base.OnSketchCompleteAsync(geometry);
		ResetAfterPlacement(!returnToSelect);
		return completed;
	}
}
