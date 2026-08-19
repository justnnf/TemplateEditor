using System;
using System.Windows;

namespace TemplateEditor;

internal static class WindowPlacementHelper
{
	public static void PositionAwayFromMapCenter(Window window)
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		if (window != null)
		{
			Rect val = (Rect)((window.Owner != null && window.Owner.ActualWidth > 0.0 && window.Owner.ActualHeight > 0.0) ? new Rect(window.Owner.Left, window.Owner.Top, window.Owner.ActualWidth, window.Owner.ActualHeight) : SystemParameters.WorkArea);
			Rect workArea = SystemParameters.WorkArea;
			window.Left = ClampToWorkArea(val.Right - window.ActualWidth - 32.0, window.ActualWidth, workArea, horizontal: true);
			window.Top = ClampToWorkArea(val.Bottom - window.ActualHeight - 128.0, window.ActualHeight, workArea, horizontal: false);
		}
	}

	private static double ClampToWorkArea(double value, double size, Rect workArea, bool horizontal)
	{
		double val = (horizontal ? workArea.Left : workArea.Top) + 12.0;
		double val2 = (horizontal ? workArea.Right : workArea.Bottom) - size - 24.0;
		return Math.Max(val, Math.Min(value, val2));
	}
}
