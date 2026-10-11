using System.Threading.Tasks;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;

namespace TemplateEditor;

internal class SketchPolylineTool : PreviewSketchTool
{
	private MapPoint _fromPoint;
	private int? _fromTerminalId;
	private bool _choosingFrom;

	public SketchPolylineTool()
	{
		base.IsSketchTool = true;
		base.SketchType = (SketchGeometryType)3;
		base.SketchOutputMode = (SketchOutputMode)1;
		base.UseSnapping = true;
		base.Cursor = ToolCursorLoader.Load("lineCursor.cur");
	}

	protected override Task OnToolActivateAsync(bool active)
	{
		_fromPoint = null;
		_fromTerminalId = null;
		return base.OnToolActivateAsync(active);
	}

	protected override async Task<bool> OnSketchModifiedAsync()
	{
		bool modified = await base.OnSketchModifiedAsync();
		if (_choosingFrom || !LineTerminalPromptService.HasEligibleTemplate())
		{
			return modified;
		}
		if (await GetCurrentSketchAsync() is not Polyline line || line.PointCount == 0)
		{
			_fromPoint = null;
			_fromTerminalId = null;
			return modified;
		}
		MapPoint first = line.Points[0];
		if (_fromPoint != null && GeometryEngine.Instance.Equals(_fromPoint, first))
		{
			return modified;
		}
		_choosingFrom = true;
		try
		{
			_fromPoint = first;
			_fromTerminalId = await LineTerminalPromptService.ChooseTerminalAsync(first, "From");
		}
		finally
		{
			_choosingFrom = false;
		}
		return modified;
	}

	protected override Task<bool> OnSketchCanceledAsync()
	{
		_fromPoint = null;
		_fromTerminalId = null;
		return base.OnSketchCanceledAsync();
	}

	protected override async Task<bool> OnSketchCompleteAsync(Geometry geometry)
	{
		LineTerminalSelection terminals = null;
		if (geometry is Polyline line && line.PointCount > 1 && LineTerminalPromptService.HasEligibleTemplate())
		{
			MapPoint first = line.Points[0];
			int? from = _fromPoint != null && GeometryEngine.Instance.Equals(_fromPoint, first)
				? _fromTerminalId
				: await LineTerminalPromptService.ChooseTerminalAsync(first, "From");
			terminals = new LineTerminalSelection
			{
				FromTerminalId = from,
				ToTerminalId = await LineTerminalPromptService.ChooseTerminalAsync(line.Points[line.PointCount - 1], "To")
			};
		}
		// Preview rotation may have supplied an anchor different from the final
		// sketch geometry; use it when present so preview and placement agree.
		await RefreshPlacementRotationAsync();
		Geometry placementGeometry = PlacementAnchorOverride ?? geometry;
		SuspendPreview();
		bool placementSucceeded = false;
		await RunWithPlacementCursorAsync(async delegate
		{
			placementSucceeded = await CommonFunctions.CreateFeatures(placementGeometry, base.RotationDegrees, lineTerminals: terminals);
		});
		bool returnToSelect = EditorDockpaneViewModel.ShouldReturnToSelectAfterPlacement(placementSucceeded);
		if (returnToSelect)
		{
			ToolReactivationService.ActivateSelectTool();
		}
		bool completed = await base.OnSketchCompleteAsync(geometry);
		ResetAfterPlacement(!returnToSelect);
		_fromPoint = null;
		_fromTerminalId = null;
		return completed;
	}
}
