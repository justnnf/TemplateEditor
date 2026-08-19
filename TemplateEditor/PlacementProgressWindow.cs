using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcGIS.Desktop.Framework;

namespace TemplateEditor;

internal sealed class PlacementProgressWindow : Window
{
	private TextBlock _messageText;

	private static bool IsDarkTheme
	{
		get
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Invalid comparison between Unknown and I4
			return (int)FrameworkApplication.ApplicationTheme == 1;
		}
	}

	public PlacementProgressWindow(string title, string message)
	{
		base.Title = title;
		base.Width = 360.0;
		base.SizeToContent = SizeToContent.Height;
		base.WindowStartupLocation = WindowStartupLocation.Manual;
		base.ShowInTaskbar = false;
		base.WindowStyle = WindowStyle.None;
		base.ResizeMode = ResizeMode.NoResize;
		base.ShowActivated = false;
		base.Topmost = true;
		base.Background = (IsDarkTheme ? new SolidColorBrush(Color.FromRgb(45, 45, 48)) : new SolidColorBrush(Color.FromRgb(250, 250, 250)));
		base.Content = BuildContent();
		SetMessage(message);
		base.Loaded += delegate
		{
			PositionNearOwner();
		};
	}

	public void SetMessage(string message)
	{
		_messageText.Text = (string.IsNullOrWhiteSpace(message) ? "Working on placement..." : message);
	}

	private UIElement BuildContent()
	{
		Border border = new Border
		{
			BorderBrush = (IsDarkTheme ? new SolidColorBrush(Color.FromRgb(96, 96, 100)) : new SolidColorBrush(Color.FromRgb(168, 168, 168))),
			BorderThickness = new Thickness(1.0),
			Padding = new Thickness(12.0)
		};
		StackPanel stackPanel = new StackPanel();
		ProgressBar element = new ProgressBar
		{
			IsIndeterminate = true,
			Height = 10.0,
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		};
		stackPanel.Children.Add(element);
		_messageText = new TextBlock
		{
			TextWrapping = TextWrapping.Wrap,
			Foreground = (IsDarkTheme ? new SolidColorBrush(Color.FromRgb(242, 242, 242)) : new SolidColorBrush(Color.FromRgb(20, 20, 20))),
			MaxHeight = 120.0
		};
		stackPanel.Children.Add(_messageText);
		border.Child = stackPanel;
		return border;
	}

	private void PositionNearOwner()
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		Rect val = (Rect)((base.Owner != null && base.Owner.ActualWidth > 0.0 && base.Owner.ActualHeight > 0.0) ? new Rect(base.Owner.Left, base.Owner.Top, base.Owner.ActualWidth, base.Owner.ActualHeight) : SystemParameters.WorkArea);
		base.Left = Math.Max(SystemParameters.VirtualScreenLeft + 8.0, val.Right - base.ActualWidth - 24.0);
		base.Top = Math.Max(SystemParameters.VirtualScreenTop + 8.0, val.Bottom - base.ActualHeight - 88.0);
	}
}
