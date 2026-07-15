using System.Threading.Tasks;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;

namespace TemplateEditor;

internal class SketchPolygonTool : PreviewSketchTool
{
	public SketchPolygonTool()
	{
		IsSketchTool = true;
		SketchType = (SketchGeometryType)4;
		SketchOutputMode = (SketchOutputMode)1;
		UseSnapping = true;
		Cursor = ToolCursorLoader.Load("cursor_polygon.cur");
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
