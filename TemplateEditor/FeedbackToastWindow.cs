using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace TemplateEditor;

internal sealed class FeedbackToastWindow : Window
{
	public FeedbackToastWindow(string title, string message, FeedbackSeverity severity)
		: this(title, message, null, severity)
	{
	}

	public FeedbackToastWindow(string title, string message, string detail, FeedbackSeverity severity)
	{
		base.Width = 380.0;
		base.SizeToContent = SizeToContent.Height;
		base.WindowStyle = WindowStyle.None;
		base.ResizeMode = ResizeMode.NoResize;
		base.ShowInTaskbar = false;
		base.ShowActivated = false;
		base.Topmost = true;
		base.AllowsTransparency = true;
		base.Background = Brushes.Transparent;
		base.Content = BuildContent(title, message, detail, severity);
		base.Loaded += delegate
		{
			PositionNearOwner();
			DispatcherTimer timer = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(4.5)
			};
			timer.Tick += delegate
			{
				timer.Stop();
				Close();
			};
			timer.Start();
		};
		base.MouseLeftButtonDown += delegate
		{
			Close();
		};
	}

	private static UIElement BuildContent(string title, string message, string detail, FeedbackSeverity severity)
	{
		Border border = new Border
		{
			Background = DialogAppearance.Background,
			BorderBrush = GetBorderBrush(severity),
			BorderThickness = new Thickness(2.0),
			CornerRadius = new CornerRadius(0.0),
			Padding = new Thickness(12.0),
			Effect = new DropShadowEffect
			{
				BlurRadius = 12.0,
				ShadowDepth = 2.0,
				Opacity = 0.22
			}
		};
		StackPanel stackPanel = new StackPanel();
		stackPanel.Children.Add(new TextBlock
		{
			Text = title,
			FontWeight = FontWeights.SemiBold,
			Foreground = DialogAppearance.Foreground,
			TextWrapping = TextWrapping.Wrap
		});
		stackPanel.Children.Add(new TextBlock
		{
			Text = message,
			Margin = new Thickness(0.0, 4.0, 0.0, 0.0),
			Foreground = DialogAppearance.Foreground,
			TextWrapping = TextWrapping.Wrap,
			MaxHeight = 72.0
		});
		if (!string.IsNullOrWhiteSpace(detail))
		{
			stackPanel.Children.Add(new TextBlock
			{
				Text = detail,
				Margin = new Thickness(0.0, 4.0, 0.0, 0.0),
				Foreground = DialogAppearance.Foreground,
				Opacity = 0.72,
				TextWrapping = TextWrapping.Wrap,
				MaxHeight = 96.0
			});
		}
		border.Child = stackPanel;
		return border;
	}

	private static Brush GetBorderBrush(FeedbackSeverity severity)
	{
		if (1 == 0)
		{
		}
		SolidColorBrush result = severity switch
		{
			FeedbackSeverity.Success => new SolidColorBrush(Color.FromRgb(86, 158, 92)), 
			FeedbackSeverity.Warning => new SolidColorBrush(Color.FromRgb(204, 155, 48)), 
			FeedbackSeverity.Error => new SolidColorBrush(Color.FromRgb(196, 72, 72)), 
			_ => new SolidColorBrush(Color.FromRgb(91, 139, 190)), 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	private void PositionNearOwner()
	{
		Rect ownerBounds = GetOwnerBounds();
		base.Left = ClampToVirtualScreen(ownerBounds.Right - base.ActualWidth - 24.0, base.ActualWidth, horizontal: true);
		base.Top = ClampToVirtualScreen(ownerBounds.Bottom - base.ActualHeight - 24.0, base.ActualHeight, horizontal: false);
	}

	private Rect GetOwnerBounds()
	{
		return (Rect)((base.Owner != null && base.Owner.ActualWidth > 0.0 && base.Owner.ActualHeight > 0.0) ? new Rect(base.Owner.Left, base.Owner.Top, base.Owner.ActualWidth, base.Owner.ActualHeight) : SystemParameters.WorkArea);
	}

	private static double ClampToVirtualScreen(double value, double size, bool horizontal)
	{
		double num = (horizontal ? SystemParameters.VirtualScreenLeft : SystemParameters.VirtualScreenTop) + 8.0;
		double val = num + (horizontal ? SystemParameters.VirtualScreenWidth : SystemParameters.VirtualScreenHeight) - size - 16.0;
		return Math.Max(num, Math.Min(value, val));
	}
}
