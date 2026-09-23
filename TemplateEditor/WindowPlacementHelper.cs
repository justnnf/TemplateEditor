using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcGIS.Desktop.Mapping;

namespace TemplateEditor;

internal static class WindowPlacementHelper
{
	public static void PositionAwayFromMapCenter(Window window)
	{
		PositionNearOwnerBottomRight(window, 32.0, 32.0);
	}

	public static void PositionNearOwnerBottomRight(Window window, double rightInset = 32.0, double bottomInset = 128.0)
	{
		if (window != null)
		{
			Rect ownerBounds = GetActiveMapBounds() ?? (window.Owner != null && window.Owner.ActualWidth > 0.0 && window.Owner.ActualHeight > 0.0
				? new Rect(window.Owner.Left, window.Owner.Top, window.Owner.ActualWidth, window.Owner.ActualHeight)
				: SystemParameters.WorkArea);
			window.Left = ClampToBounds(ownerBounds.Left + (ownerBounds.Width - window.ActualWidth) / 2.0, window.ActualWidth, ownerBounds.Left + rightInset, ownerBounds.Right - rightInset);
			window.Top = ClampToBounds(ownerBounds.Bottom - window.ActualHeight - bottomInset, window.ActualHeight, ownerBounds.Top + 12.0, ownerBounds.Bottom - 12.0);
		}
	}

	public static IDisposable ShowPendingMapDecision()
	{
		Rect? bounds = GetActiveMapBounds();
		if (!bounds.HasValue)
		{
			return null;
		}
		Rect mapBounds = bounds.Value;
		Window outline = new Window
		{
			Left = mapBounds.Left,
			Top = mapBounds.Top,
			Width = mapBounds.Width,
			Height = mapBounds.Height,
			WindowStyle = WindowStyle.None,
			AllowsTransparency = true,
			Background = Brushes.Transparent,
			ShowInTaskbar = false,
			ShowActivated = false,
			Topmost = true,
			IsHitTestVisible = false,
			Content = new Border { BorderBrush = Brushes.Red, BorderThickness = new Thickness(4) }
		};
		outline.Show();
		return new WindowScope(outline);
	}

	private static Rect? GetActiveMapBounds()
	{
		MapView view = MapView.Active;
		if (view == null)
		{
			return null;
		}
		Point origin = view.ClientToScreen(new Point(0, 0));
		Size size = view.GetViewSize();
		PresentationSource source = PresentationSource.FromVisual(Application.Current?.MainWindow);
		if (source?.CompositionTarget != null)
		{
			Matrix fromDevice = source.CompositionTarget.TransformFromDevice;
			origin = fromDevice.Transform(origin);
		}
		return size.Width > 0 && size.Height > 0 ? new Rect(origin, size) : null;
	}

	private sealed class WindowScope : IDisposable
	{
		private Window _window;

		public WindowScope(Window window) => _window = window;

		public void Dispose()
		{
			Window window = _window;
			_window = null;
			window?.Close();
		}
	}

	private static double ClampToBounds(double value, double size, double minimum, double maximum)
	{
		return Math.Clamp(value, minimum, Math.Max(minimum, maximum - size));
	}
}
