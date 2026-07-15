using System.Threading.Tasks;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using System.Windows.Input;

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
		Cursor previousCursor = Cursor;
		Cursor = Cursors.Wait;
		bool placementSucceeded = false;
		await Task.Yield();
		try
		{
			placementSucceeded = await CommonFunctions.CreateFeatures(geometry);
		}
		finally
		{
			Cursor = previousCursor;
		}
		if (EditorDockpaneViewModel.ShouldReturnToSelectAfterPlacement(placementSucceeded))
		{
			ToolReactivationService.ActivateSelectTool();
		}
		return await base.OnSketchCompleteAsync(geometry);
	}
}
