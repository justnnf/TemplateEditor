using System;
using System.Windows;

namespace TemplateEditor;

internal static class WindowPlacementHelper
{
	public static void PositionAwayFromMapCenter(Window window)
	{
		if (window == null)
		{
			return;
		}
		Rect bounds = window.Owner != null && window.Owner.ActualWidth > 0.0 && window.Owner.ActualHeight > 0.0
			? new Rect(window.Owner.Left, window.Owner.Top, window.Owner.ActualWidth, window.Owner.ActualHeight)
			: SystemParameters.WorkArea;
		window.Left = ClampToVirtualScreen(bounds.Right - window.ActualWidth - 32.0, window.ActualWidth, horizontal: true);
		window.Top = ClampToVirtualScreen(bounds.Bottom - window.ActualHeight - 96.0, window.ActualHeight, horizontal: false);
	}

	private static double ClampToVirtualScreen(double value, double size, bool horizontal)
	{
		double minimum = (horizontal ? SystemParameters.VirtualScreenLeft : SystemParameters.VirtualScreenTop) + 8.0;
		double maximum = minimum + (horizontal ? SystemParameters.VirtualScreenWidth : SystemParameters.VirtualScreenHeight) - size - 16.0;
		return Math.Max(minimum, Math.Min(value, maximum));
	}
}
