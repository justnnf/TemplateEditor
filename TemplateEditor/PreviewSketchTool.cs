using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace TemplateEditor;

internal abstract class PreviewSketchTool : MapTool
{
	private static PreviewSketchTool _activePreviewTool;
	private readonly List<IDisposable> _previewOverlays = new List<IDisposable>();
	private readonly object _previewOverlayLock = new object();
	private bool _isPreviewUpdateQueued;
	private bool _isPreviewSuspended;
	private bool _isSuspendedForPlacement;
	private Point? _lastClientPoint;
	private Point? _rotationAnchorClientPoint;

	protected double RotationDegrees { get; private set; }
	protected MapPoint PlacementAnchorOverride { get; private set; }

	protected override Task OnToolActivateAsync(bool active)
	{
		if (active)
		{
			_activePreviewTool = this;
			ResetPreviewState(clearRotation: true);
		}
		return base.OnToolActivateAsync(active);
	}

	internal static void ResetActivePreviewTool()
	{
		_activePreviewTool?.ResetPreviewState(clearRotation: true);
	}

	protected override void OnToolMouseMove(MapViewMouseEventArgs args)
	{
		HandlePreviewMouseMove(args.ClientPoint);
	}

	private void HandlePreviewMouseMove(Point clientPoint)
	{
		if (_isPreviewSuspended)
		{
			if (_isSuspendedForPlacement)
			{
				return;
			}
			_isPreviewSuspended = false;
		}
		_lastClientPoint = clientPoint;
		QueuePreviewUpdate(clientPoint);
	}

	protected override void OnToolKeyDown(MapViewKeyEventArgs args)
	{
		if (_isPreviewSuspended)
		{
			return;
		}
		if (Keyboard.IsKeyDown(Key.E))
		{
			ResetRotation();
			args.Handled = true;
		}
		else if (Keyboard.IsKeyDown(Key.R))
		{
			BeginRotateMode(args.ClientPoint);
			args.Handled = true;
		}
	}

	protected override void OnToolKeyUp(MapViewKeyEventArgs args)
	{
		if (!Keyboard.IsKeyDown(Key.R) && _rotationAnchorClientPoint.HasValue)
		{
			EndRotateMode();
			args.Handled = true;
		}
	}

	protected override Task OnToolDeactivateAsync(bool hasMapViewChanged)
	{
		_isPreviewSuspended = true;
		_isSuspendedForPlacement = false;
		ClearPreviewOverlay();
		if (ReferenceEquals(_activePreviewTool, this))
		{
			_activePreviewTool = null;
		}
		return base.OnToolDeactivateAsync(hasMapViewChanged);
	}

	protected override Task<bool> OnSketchCanceledAsync()
	{
		ClearPreviewOverlay();
		_isPreviewSuspended = false;
		_isSuspendedForPlacement = false;
		_lastClientPoint = null;
		_rotationAnchorClientPoint = null;
		PlacementAnchorOverride = null;
		return base.OnSketchCanceledAsync();
	}

	private void ResetPreviewState(bool clearRotation)
	{
		_isPreviewSuspended = false;
		_isSuspendedForPlacement = false;
		_isPreviewUpdateQueued = false;
		_lastClientPoint = null;
		_rotationAnchorClientPoint = null;
		PlacementAnchorOverride = null;
		if (clearRotation)
		{
			RotationDegrees = 0.0;
		}
		ClearPreviewOverlay();
	}

	protected void SuspendPreview()
	{
		_isPreviewSuspended = true;
		_isSuspendedForPlacement = true;
		ClearPreviewOverlay();
	}

	protected void ResumePreviewAfterPlacement()
	{
		_isPreviewSuspended = false;
		_isSuspendedForPlacement = false;
		PlacementAnchorOverride = null;
		RefreshPreview();
	}

	protected async Task RunWithPlacementCursorAsync(Func<Task> placementAction)
	{
		Cursor previousCursor = Cursor;
		Cursor = Cursors.Wait;
		await Task.Yield();
		try
		{
			await placementAction();
		}
		finally
		{
			Cursor = previousCursor;
		}
	}

	protected void ClearPreviewOverlay()
	{
		lock (_previewOverlayLock)
		{
			foreach (IDisposable previewOverlay in _previewOverlays)
			{
				previewOverlay.Dispose();
			}
			_previewOverlays.Clear();
		}
		_isPreviewUpdateQueued = false;
	}

	private void BeginRotateMode(Point clientPoint)
	{
		if (!_rotationAnchorClientPoint.HasValue)
		{
			_rotationAnchorClientPoint = _lastClientPoint ?? clientPoint;
		}
	}

	private void EndRotateMode()
	{
		_rotationAnchorClientPoint = null;
		PlacementAnchorOverride = null;
		RefreshPreview();
	}

	private void ResetRotation()
	{
		RotationDegrees = 0.0;
		_rotationAnchorClientPoint = null;
		PlacementAnchorOverride = null;
		RefreshPreview();
	}

	protected async Task RefreshPlacementRotationAsync()
	{
		if (!_rotationAnchorClientPoint.HasValue || !_lastClientPoint.HasValue)
		{
			return;
		}
		Point anchorClientPoint = _rotationAnchorClientPoint.Value;
		Point currentClientPoint = _lastClientPoint.Value;
		await QueuedTask.Run(() =>
		{
			MapView mapView = MapView.Active;
			if (mapView == null)
			{
				return;
			}
			MapPoint anchorMapPoint = mapView.ClientToMap(anchorClientPoint);
			MapPoint currentMapPoint = mapView.ClientToMap(currentClientPoint);
			UpdateRotationFromMapPoints(anchorMapPoint, currentMapPoint);
			PlacementAnchorOverride = anchorMapPoint;
		});
	}

	private void UpdateRotationFromMapPoints(MapPoint anchorPoint, MapPoint currentPoint)
	{
		if (anchorPoint == null || currentPoint == null)
		{
			return;
		}
		double x = currentPoint.X - anchorPoint.X;
		double y = currentPoint.Y - anchorPoint.Y;
		if (Math.Abs(x) < 0.001 && Math.Abs(y) < 0.001)
		{
			return;
		}
		RotationDegrees = NormalizeDegrees(Math.Atan2(y, x) * 180.0 / Math.PI);
	}

	private void RefreshPreview()
	{
		if (_lastClientPoint.HasValue)
		{
			QueuePreviewUpdate(_lastClientPoint.Value);
		}
	}

	private static double NormalizeDegrees(double degrees)
	{
		double normalized = degrees % 360.0;
		return normalized < 0.0 ? normalized + 360.0 : normalized;
	}

	private void QueuePreviewUpdate(Point clientPoint)
	{
		if (_isPreviewSuspended || _isPreviewUpdateQueued)
		{
			return;
		}
		_isPreviewUpdateQueued = true;
		TaskObservationService.Forget(UpdatePreviewAsync(clientPoint), "Preview overlay update failed.");
	}

	private async Task UpdatePreviewAsync(Point clientPoint)
	{
		try
		{
			await QueuedTask.Run(() =>
			{
				if (_isPreviewSuspended)
				{
					return;
				}
				MapView mapView = MapView.Active;
				if (mapView == null)
				{
					ClearPreviewOverlay();
					return;
				}
				MapPoint anchorPoint = mapView.ClientToMap(_rotationAnchorClientPoint ?? clientPoint);
				if (_rotationAnchorClientPoint.HasValue)
				{
					MapPoint currentPoint = mapView.ClientToMap(clientPoint);
					UpdateRotationFromMapPoints(anchorPoint, currentPoint);
					PlacementAnchorOverride = anchorPoint;
				}
				else
				{
					PlacementAnchorOverride = null;
				}
				ClearPreviewOverlay();
				if (_isPreviewSuspended)
				{
					return;
				}
				foreach (PreviewOverlayGraphic graphic in CommonFunctions.CreatePreviewGraphics(anchorPoint, RotationDegrees))
				{
					IDisposable overlay = mapView.AddOverlay(graphic.Geometry, graphic.Symbol);
					lock (_previewOverlayLock)
					{
						_previewOverlays.Add(overlay);
					}
				}
			});
		}
		catch (Exception ex)
		{
			LogService.LogException("Preview overlay update failed.", ex);
			ClearPreviewOverlay();
		}
		finally
		{
			_isPreviewUpdateQueued = false;
		}
	}
}
