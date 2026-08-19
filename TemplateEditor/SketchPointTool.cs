using System.Diagnostics;
using System.Runtime.CompilerServices;
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
		base.Cursor = ToolCursorLoader.Load("cursor_point.cur");
	}

	protected override Task OnToolActivateAsync(bool active)
	{
		return base.OnToolActivateAsync(active);
	}

	protected override async Task<bool> OnSketchCompleteAsync(Geometry geometry)
	{
		if (OffsetPlacementSession.IsActive && OffsetPlacementSession.InsertPoint == null)
		{
			OffsetPlacementSession.SetInsertPoint((MapPoint)(object)((geometry is MapPoint) ? geometry : null));
			EditorDockpaneViewModel.SetPlacementStatus("Offset placement: move the mouse around the distance ring to choose a direction, then click to place.");
			return await _003C_003En__0(geometry);
		}
		await RefreshPlacementRotationAsync();
		MapPoint splitPointOverride = (OffsetPlacementSession.IsActive ? OffsetPlacementSession.InsertPoint : null);
		object obj;
		if (!OffsetPlacementSession.IsActive)
		{
			Geometry val = (Geometry)base.PlacementAnchorOverride;
			obj = (object)val;
			if (val == null)
			{
				obj = geometry;
			}
		}
		else
		{
			obj = OffsetPlacementSession.GetOffsetPoint((MapPoint)(object)((geometry is MapPoint) ? geometry : null));
		}
		Geometry placementGeometry = (Geometry)obj;
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
