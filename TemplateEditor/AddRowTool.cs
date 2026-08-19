using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;

namespace TemplateEditor;

internal class AddRowTool : MapTool
{
	public AddRowTool()
	{
		base.IsSketchTool = true;
		base.SketchType = (SketchGeometryType)0;
		base.SketchOutputMode = (SketchOutputMode)1;
		base.Cursor = ToolCursorLoader.Load("cursor_row.cur");
	}

	protected override Task OnToolActivateAsync(bool active)
	{
		return base.OnToolActivateAsync(active);
	}

	protected override async Task<bool> OnSketchCompleteAsync(Geometry geometry)
	{
		Cursor previousCursor = base.Cursor;
		base.Cursor = Cursors.Wait;
		bool placementSucceeded = false;
		await Task.Yield();
		try
		{
			placementSucceeded = await CommonFunctions.CreateFeatures(geometry);
		}
		finally
		{
			base.Cursor = previousCursor;
		}
		if (EditorDockpaneViewModel.ShouldReturnToSelectAfterPlacement(placementSucceeded))
		{
			ToolReactivationService.ActivateSelectTool();
		}
		return await _003C_003En__0(geometry);
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private Task<bool> _003C_003En__0(Geometry geometry)
	{
		return base.OnSketchCompleteAsync(geometry);
	}
}
