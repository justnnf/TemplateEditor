using System.Threading.Tasks;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Mapping;

namespace TemplateEditor;

internal class SketchPointTool : PreviewSketchTool
{
	public SketchPointTool()
	{
		IsSketchTool = true;
		SketchType = (SketchGeometryType)0;
		SketchOutputMode = (SketchOutputMode)1;
		UseSnapping = true;
		Cursor = ToolCursorLoader.Load("cursor_point.cur");
	}

	protected override Task OnToolActivateAsync(bool active)
	{
		return base.OnToolActivateAsync(active);
	}

	protected override async Task<bool> OnSketchCompleteAsync(Geometry geometry)
	{
		await RefreshPlacementRotationAsync();
		Geometry placementGeometry = (Geometry)(object)PlacementAnchorOverride ?? geometry;
		SuspendPreview();
		bool placementSucceeded = false;
		await RunWithPlacementCursorAsync(async () => placementSucceeded = await CommonFunctions.CreateFeatures(placementGeometry, RotationDegrees));
		if (EditorDockpaneViewModel.ShouldReturnToSelectAfterPlacement(placementSucceeded))
		{
			ToolReactivationService.ActivateSelectTool();
		}
		else
		{
			ResumePreviewAfterPlacement();
		}
		return await base.OnSketchCompleteAsync(geometry);
	}
}
