using System;
using System.Windows;

namespace TemplateEditor;

internal static class WindowPlacementHelper
{
	public static void PositionAwayFromMapCenter(Window window)
	{
		PositionNearOwnerBottomRight(window, 32.0, 128.0);
	}

	public static void PositionNearOwnerBottomRight(Window window, double rightInset = 32.0, double bottomInset = 128.0)
	{
		if (window != null)
		{
			// Map workflow prompts stay near the lower-right of the ArcGIS Pro window
			// so they do not cover the sketch/edit focus near the center of the map.
			Rect val = (Rect)((window.Owner != null && window.Owner.ActualWidth > 0.0 && window.Owner.ActualHeight > 0.0) ? new Rect(window.Owner.Left, window.Owner.Top, window.Owner.ActualWidth, window.Owner.ActualHeight) : SystemParameters.WorkArea);
			Rect workArea = SystemParameters.WorkArea;
			window.Left = ClampToWorkArea(val.Right - window.ActualWidth - rightInset, window.ActualWidth, workArea, horizontal: true);
			window.Top = ClampToWorkArea(val.Bottom - window.ActualHeight - bottomInset, window.ActualHeight, workArea, horizontal: false);
		}
	}

	private static double ClampToWorkArea(double value, double size, Rect workArea, bool horizontal)
	{
		double val = (horizontal ? workArea.Left : workArea.Top) + 12.0;
		double val2 = (horizontal ? workArea.Right : workArea.Bottom) - size - 24.0;
		return Math.Max(val, Math.Min(value, val2));
	}
}
