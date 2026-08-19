using System.Diagnostics;
using System.Runtime.CompilerServices;
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
		base.Cursor = ToolCursorLoader.Load("cursor_polygon.cur");
	}

	protected override Task OnToolActivateAsync(bool active)
	{
		return base.OnToolActivateAsync(active);
	}

	protected override async Task<bool> OnSketchCompleteAsync(Geometry geometry)
	{
		await RefreshPlacementRotationAsync();
		Geometry val = (Geometry)base.PlacementAnchorOverride;
		object obj = (object)val;
		if (val == null)
		{
			obj = geometry;
		}
		Geometry placementGeometry = (Geometry)obj;
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
		bool completed = await _003C_003En__0(geometry);
		ResetAfterPlacement(!returnToSelect);
		return completed;
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private Task<bool> _003C_003En__0(Geometry geometry)
	{
		return base.OnSketchCompleteAsync(geometry);
	}
}
