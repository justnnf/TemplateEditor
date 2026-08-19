using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace TemplateEditor;

internal abstract class PreviewSketchTool : MapTool
{
	private const int PreviewUpdateIntervalMilliseconds = 33;

	private static PreviewSketchTool _activePreviewTool;

	private readonly List<IDisposable> _previewOverlays = new List<IDisposable>();

	private readonly object _previewOverlayLock = new object();

	private bool _isPreviewUpdateQueued;

	private bool _isPreviewDelayQueued;

	private bool _isPreviewSuspended;

	private bool _isSuspendedForPlacement;

	private bool _resetRotationOnNextMouseMove;

	private Point? _lastClientPoint;

	private Point? _rotationAnchorClientPoint;

	private MapPoint _offsetRotationAnchor;

	private Point? _lastRenderedClientPoint;

	private DateTime _lastPreviewUpdateStartedUtc = DateTime.MinValue;

	private int _previewGeneration;

	protected double RotationDegrees { get; private set; }

	protected MapPoint PlacementAnchorOverride { get; private set; }

	protected override Task OnToolActivateAsync(bool active)
	{
		if (active)
		{
			_activePreviewTool = this;
			ResetPreviewState(clearRotation: true);
			_resetRotationOnNextMouseMove = true;
		}
		return base.OnToolActivateAsync(active);
	}

	internal static void ResetActivePreviewTool()
	{
		_activePreviewTool?.ResetPreviewState(clearRotation: true);
	}

	protected override void OnToolMouseMove(MapViewMouseEventArgs args)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		if (_resetRotationOnNextMouseMove)
		{
			ResetPreviewState(clearRotation: true);
			_resetRotationOnNextMouseMove = false;
		}
		HandlePreviewMouseMove(args.ClientPoint);
	}

	private void HandlePreviewMouseMove(Point clientPoint)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
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
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		if (!_isPreviewSuspended)
		{
			if (Keyboard.IsKeyDown((Key)48))
			{
				ResetRotation();
				((RoutedEventArgs)(object)args).Handled = true;
			}
			else if (Keyboard.IsKeyDown((Key)61))
			{
				BeginRotateMode(args.ClientPoint);
				((RoutedEventArgs)(object)args).Handled = true;
			}
		}
	}

	protected override void OnToolKeyUp(MapViewKeyEventArgs args)
	{
		if (!Keyboard.IsKeyDown((Key)61) && _rotationAnchorClientPoint.HasValue)
		{
			EndRotateMode();
			((RoutedEventArgs)(object)args).Handled = true;
		}
	}

	protected override Task OnToolDeactivateAsync(bool hasMapViewChanged)
	{
		Interlocked.Increment(ref _previewGeneration);
		_isPreviewSuspended = true;
		_isSuspendedForPlacement = false;
		RotationDegrees = 0.0;
		_rotationAnchorClientPoint = null;
		_offsetRotationAnchor = null;
		PlacementAnchorOverride = null;
		OffsetPlacementSession.End();
		ClearPreviewOverlay();
		if (_activePreviewTool == this)
		{
			_activePreviewTool = null;
		}
		return base.OnToolDeactivateAsync(hasMapViewChanged);
	}

	protected override Task<bool> OnSketchCanceledAsync()
	{
		Interlocked.Increment(ref _previewGeneration);
		ClearPreviewOverlay();
		_isPreviewSuspended = false;
		_isSuspendedForPlacement = false;
		_lastClientPoint = null;
		_lastRenderedClientPoint = null;
		_rotationAnchorClientPoint = null;
		_offsetRotationAnchor = null;
		RotationDegrees = 0.0;
		PlacementAnchorOverride = null;
		OffsetPlacementSession.End();
		return base.OnSketchCanceledAsync();
	}

	private void ResetPreviewState(bool clearRotation)
	{
		Interlocked.Increment(ref _previewGeneration);
		_isPreviewSuspended = false;
		_isSuspendedForPlacement = false;
		_isPreviewUpdateQueued = false;
		_lastClientPoint = null;
		_rotationAnchorClientPoint = null;
		_offsetRotationAnchor = null;
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
	}

	protected void ResetAfterPlacement(bool resumePreview)
	{
		Interlocked.Increment(ref _previewGeneration);
		RotationDegrees = 0.0;
		_rotationAnchorClientPoint = null;
		_offsetRotationAnchor = null;
		PlacementAnchorOverride = null;
		ClearPreviewOverlay();
		if (resumePreview)
		{
			_isPreviewSuspended = false;
			_isSuspendedForPlacement = false;
			RefreshPreview();
		}
		_resetRotationOnNextMouseMove = true;
	}

	protected async Task RunWithPlacementCursorAsync(Func<Task> placementAction)
	{
		Cursor previousCursor = base.Cursor;
		base.Cursor = Cursors.Wait;
		await Task.Yield();
		try
		{
			await placementAction();
		}
		finally
		{
			base.Cursor = previousCursor;
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
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		if (!_rotationAnchorClientPoint.HasValue)
		{
			_rotationAnchorClientPoint = _lastClientPoint ?? clientPoint;
			RefreshPreview();
		}
	}

	private void EndRotateMode()
	{
		_rotationAnchorClientPoint = null;
		_offsetRotationAnchor = null;
		PlacementAnchorOverride = null;
		RefreshPreview();
	}

	private void ResetRotation()
	{
		RotationDegrees = 0.0;
		_rotationAnchorClientPoint = null;
		_offsetRotationAnchor = null;
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
		await QueuedTask.Run((Action)delegate
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			MapView active = MapView.Active;
			if (active != null)
			{
				MapPoint val = active.ClientToMap(anchorClientPoint);
				MapPoint currentPoint = active.ClientToMap(currentClientPoint);
				UpdateRotationFromMapPoints(val, currentPoint);
				PlacementAnchorOverride = val;
			}
		}, TaskCreationOptions.None);
	}

	private void UpdateRotationFromMapPoints(MapPoint anchorPoint, MapPoint currentPoint)
	{
		if (anchorPoint != null && currentPoint != null)
		{
			double num = currentPoint.X - anchorPoint.X;
			double num2 = currentPoint.Y - anchorPoint.Y;
			if (!(Math.Abs(num) < 0.001) || !(Math.Abs(num2) < 0.001))
			{
				RotationDegrees = NormalizeDegrees(Math.Atan2(num2, num) * 180.0 / Math.PI);
			}
		}
	}

	private void RefreshPreview()
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		if (_lastClientPoint.HasValue)
		{
			QueuePreviewUpdate(_lastClientPoint.Value);
		}
	}

	private static double NormalizeDegrees(double degrees)
	{
		double num = degrees % 360.0;
		return (num < 0.0) ? (num + 360.0) : num;
	}

	private void QueuePreviewUpdate(Point clientPoint, bool force = false)
	{
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		if (!_isPreviewSuspended && !_isPreviewUpdateQueued)
		{
			if ((DateTime.UtcNow - _lastPreviewUpdateStartedUtc).TotalMilliseconds < 33.0)
			{
				ScheduleDelayedPreviewUpdate();
				return;
			}
			_isPreviewUpdateQueued = true;
			_lastPreviewUpdateStartedUtc = DateTime.UtcNow;
			int previewGeneration = Volatile.Read(in _previewGeneration);
			TaskObservationService.Forget(UpdatePreviewAsync(clientPoint, previewGeneration), "Preview overlay update failed.");
		}
	}

	private void ScheduleDelayedPreviewUpdate()
	{
		if (!_isPreviewDelayQueued)
		{
			_isPreviewDelayQueued = true;
			TaskObservationService.Forget(QueueDelayedPreviewUpdateAsync(), "Delayed preview overlay update failed.");
		}
	}

	private async Task QueueDelayedPreviewUpdateAsync()
	{
		try
		{
			await Task.Delay(33);
			_isPreviewDelayQueued = false;
			if (!_isPreviewSuspended && _lastClientPoint.HasValue)
			{
				QueuePreviewUpdate(_lastClientPoint.Value);
			}
		}
		finally
		{
			_isPreviewDelayQueued = false;
		}
	}

	private async Task UpdatePreviewAsync(Point clientPoint, int previewGeneration)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			await QueuedTask.Run((Action)delegate
			{
				//IL_0063: Unknown result type (might be due to invalid IL or missing references)
				//IL_0068: Unknown result type (might be due to invalid IL or missing references)
				//IL_008e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0110: Unknown result type (might be due to invalid IL or missing references)
				//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
				//IL_01cb: Unknown result type (might be due to invalid IL or missing references)
				//IL_01e0: Unknown result type (might be due to invalid IL or missing references)
				//IL_01eb: Unknown result type (might be due to invalid IL or missing references)
				//IL_01f7: Unknown result type (might be due to invalid IL or missing references)
				//IL_0203: Unknown result type (might be due to invalid IL or missing references)
				//IL_020f: Unknown result type (might be due to invalid IL or missing references)
				//IL_021b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0236: Expected O, but got Unknown
				if (!_isPreviewSuspended && previewGeneration == Volatile.Read(in _previewGeneration))
				{
					MapView active = MapView.Active;
					if (active == null)
					{
						ClearPreviewOverlay();
					}
					else
					{
						MapPoint val = active.ClientToMap(_rotationAnchorClientPoint.GetValueOrDefault(clientPoint));
						if (OffsetPlacementSession.IsActive && OffsetPlacementSession.InsertPoint != null)
						{
							MapPoint directionPoint = active.ClientToMap(clientPoint);
							if (_rotationAnchorClientPoint.HasValue)
							{
								if (_offsetRotationAnchor == null)
								{
									_offsetRotationAnchor = OffsetPlacementSession.GetOffsetPoint(directionPoint);
								}
								val = _offsetRotationAnchor;
							}
							else
							{
								val = OffsetPlacementSession.GetOffsetPoint(directionPoint);
							}
							PlacementAnchorOverride = val;
						}
						if (_rotationAnchorClientPoint.HasValue)
						{
							MapPoint currentPoint = active.ClientToMap(clientPoint);
							UpdateRotationFromMapPoints(val, currentPoint);
							PlacementAnchorOverride = val;
						}
						else if (!OffsetPlacementSession.IsActive || OffsetPlacementSession.InsertPoint == null)
						{
							PlacementAnchorOverride = null;
						}
						ClearPreviewOverlay();
						if (!_isPreviewSuspended && previewGeneration == Volatile.Read(in _previewGeneration))
						{
							if (OffsetPlacementSession.IsActive && OffsetPlacementSession.InsertPoint != null)
							{
								Geometry val2 = GeometryEngine.Instance.GeodesicEllipse(new GeodesicEllipseParameter
								{
									Center = new Coordinate2D(OffsetPlacementSession.InsertPoint.X, OffsetPlacementSession.InsertPoint.Y),
									SemiAxis1Length = OffsetPlacementSession.DistanceMeters,
									SemiAxis2Length = OffsetPlacementSession.DistanceMeters,
									LinearUnit = LinearUnit.Meters,
									OutGeometryType = (GeometryType)27656,
									VertexCount = 144u
								}, ((Geometry)OffsetPlacementSession.InsertPoint).SpatialReference);
								if (val2 != null)
								{
									IDisposable disposable = MappingExtensions.AddOverlay(active, val2, CreateOffsetGuideSymbol(), -1.0);
									lock (_previewOverlayLock)
									{
										if (previewGeneration == Volatile.Read(in _previewGeneration))
										{
											_previewOverlays.Add(disposable);
										}
										else
										{
											disposable.Dispose();
										}
									}
								}
							}
							foreach (PreviewOverlayGraphic item in CommonFunctions.CreatePreviewGraphics(val, RotationDegrees))
							{
								if (previewGeneration != Volatile.Read(in _previewGeneration))
								{
									break;
								}
								IDisposable disposable2 = MappingExtensions.AddOverlay(active, item.Geometry, item.Symbol, -1.0);
								lock (_previewOverlayLock)
								{
									if (previewGeneration == Volatile.Read(in _previewGeneration))
									{
										_previewOverlays.Add(disposable2);
									}
									else
									{
										disposable2.Dispose();
									}
								}
							}
						}
					}
				}
			}, TaskCreationOptions.None);
			_lastRenderedClientPoint = clientPoint;
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			LogService.LogException("Preview overlay update failed.", ex2);
			ClearPreviewOverlay();
		}
		finally
		{
			_isPreviewUpdateQueued = false;
			if (!_isPreviewSuspended && previewGeneration == Volatile.Read(in _previewGeneration) && _lastClientPoint.HasValue && _lastClientPoint.Value != clientPoint)
			{
				QueuePreviewUpdate(_lastClientPoint.Value, force: true);
			}
		}
	}

	private static CIMSymbolReference CreateOffsetGuideSymbol()
	{
		CIMColor val = ColorFactory.Instance.CreateRGBColor(90.0, 190.0, 255.0, 25.0);
		CIMColor val2 = ColorFactory.Instance.CreateRGBColor(80.0, 195.0, 255.0, 90.0);
		return SymbolExtensionMethods.MakeSymbolReference((CIMSymbol)(object)SymbolFactory.Instance.ConstructPolygonSymbol(val, (SimpleFillStyle)0, SymbolFactory.Instance.ConstructStroke(val2, 2.0, (SimpleLineStyle)0)));
	}
}
