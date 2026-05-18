using System.Threading.Tasks;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;

namespace TemplateEditor;

internal class AddRowTool : MapTool
{
	public AddRowTool()
	{
		IsSketchTool = true;
		SketchType = (SketchGeometryType)0;
		SketchOutputMode = (SketchOutputMode)1;
		Cursor = ToolCursorLoader.Load("cursor_row.cur");
	}

	protected override Task OnToolActivateAsync(bool active)
	{
		return base.OnToolActivateAsync(active);
	}

	protected override async Task<bool> OnSketchCompleteAsync(Geometry geometry)
	{
		await CommonFunctions.CreateFeatures(geometry);
		ToolReactivationService.ActivateSelectTool();
		return await base.OnSketchCompleteAsync(geometry);
	}
}
