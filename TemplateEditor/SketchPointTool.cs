using System.Threading.Tasks;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;

namespace TemplateEditor;

internal class SketchPointTool : PreviewSketchTool
{
	public SketchPointTool()
	{
		base.IsSketchTool = true;
		base.SketchType = (SketchGeometryType)0;
		base.SketchOutputMode = (SketchOutputMode)1;
		base.UseSnapping = true;
		base.Cursor = ToolCursorLoader.Load("pointCursor.cur");
	}

	protected override Task OnToolActivateAsync(bool active)
	{
		return base.OnToolActivateAsync(active);
	}

	protected override async Task<bool> OnSketchCompleteAsync(Geometry geometry)
	{
		// Offset placement uses the first click as its origin and the second click
		// only to choose direction. The actual feature is created after that second click.
		if (OffsetPlacementSession.IsActive && OffsetPlacementSession.InsertPoint == null)
		{
			OffsetPlacementSession.SetInsertPoint(geometry as MapPoint);
			EditorDockpaneViewModel.SetPlacementStatus("Offset placement: move the mouse around the distance ring to choose a direction, then click to place.");
			return await base.OnSketchCompleteAsync(geometry);
		}
		await RefreshPlacementRotationAsync();
		MapPoint splitPointOverride = (OffsetPlacementSession.IsActive ? OffsetPlacementSession.InsertPoint : null);
		Geometry placementGeometry = OffsetPlacementSession.IsActive
			? OffsetPlacementSession.GetOffsetPoint(geometry as MapPoint)
			: PlacementAnchorOverride ?? geometry;
		SuspendPreview();
		bool placementSucceeded = false;
		await RunWithPlacementCursorAsync(async delegate
		{
			placementSucceeded = await CommonFunctions.CreateFeatures(placementGeometry, base.RotationDegrees, splitPointOverride);
		});
		OffsetPlacementSession.End();
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
