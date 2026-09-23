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
			WindowPlacementHelper.PositionNearOwnerBottomRight(this, 24.0, 24.0);
			DispatcherTimer timer = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(7.5)
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
		return severity switch
		{
			FeedbackSeverity.Success => new SolidColorBrush(Color.FromRgb(86, 158, 92)),
			FeedbackSeverity.Warning => new SolidColorBrush(Color.FromRgb(204, 155, 48)),
			FeedbackSeverity.Error => new SolidColorBrush(Color.FromRgb(196, 72, 72)),
			_ => new SolidColorBrush(Color.FromRgb(91, 139, 190)),
		};
	}

}
