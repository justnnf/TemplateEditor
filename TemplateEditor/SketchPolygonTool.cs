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
		Geometry placementGeometry = (Geometry)(object)PlacementAnchorOverride ?? geometry;
		SuspendPreview();
		await CommonFunctions.CreateFeatures(placementGeometry, RotationDegrees);
		ToolReactivationService.ActivateSelectTool();
		return await base.OnSketchCompleteAsync(geometry);
	}
}
