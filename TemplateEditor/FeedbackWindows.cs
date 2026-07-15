using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using ArcGIS.Desktop.Framework;

namespace TemplateEditor;

internal sealed class DialogButtonChoice
{
	public DialogButtonChoice(string label, MessageBoxResult result, bool isPrimary = false, bool isCancel = false)
	{
		Label = label;
		Result = result;
		IsPrimary = isPrimary;
		IsCancel = isCancel;
	}

	public string Label { get; }

	public MessageBoxResult Result { get; }

	public bool IsPrimary { get; }

	public bool IsCancel { get; }
}

internal enum FeedbackSeverity
{
	Info,
	Success,
	Warning,
	Error
}

internal sealed class FeedbackToastWindow : Window
{
	public FeedbackToastWindow(string title, string message, FeedbackSeverity severity)
		: this(title, message, null, severity)
	{
	}

	public FeedbackToastWindow(string title, string message, string detail, FeedbackSeverity severity)
	{
		Width = 380.0;
		SizeToContent = SizeToContent.Height;
		WindowStyle = WindowStyle.None;
		ResizeMode = ResizeMode.NoResize;
		ShowInTaskbar = false;
		ShowActivated = false;
		Topmost = true;
		Background = Brushes.Transparent;
		Content = BuildContent(title, message, detail, severity);
		Loaded += delegate
		{
			PositionNearOwner();
			DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4.5) };
			timer.Tick += delegate
			{
				timer.Stop();
				Close();
			};
			timer.Start();
		};
		MouseLeftButtonDown += delegate { Close(); };
	}

	private static UIElement BuildContent(string title, string message, string detail, FeedbackSeverity severity)
	{
		Border border = new Border
		{
			Background = SystemColors.WindowBrush,
			BorderBrush = GetBorderBrush(severity),
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(5.0),
			Padding = new Thickness(12.0),
			Effect = new System.Windows.Media.Effects.DropShadowEffect
			{
				BlurRadius = 12.0,
				ShadowDepth = 2.0,
				Opacity = 0.22
			}
		};
		StackPanel panel = new StackPanel();
		panel.Children.Add(new TextBlock
		{
			Text = title,
			FontWeight = FontWeights.SemiBold,
			Foreground = SystemColors.ControlTextBrush,
			TextWrapping = TextWrapping.Wrap
		});
		panel.Children.Add(new TextBlock
		{
			Text = message,
			Margin = new Thickness(0.0, 4.0, 0.0, 0.0),
			Foreground = SystemColors.ControlTextBrush,
			TextWrapping = TextWrapping.Wrap,
			MaxHeight = 72.0
		});
		if (!string.IsNullOrWhiteSpace(detail))
		{
			panel.Children.Add(new TextBlock
			{
				Text = detail,
				Margin = new Thickness(0.0, 4.0, 0.0, 0.0),
				Foreground = SystemColors.GrayTextBrush,
				TextWrapping = TextWrapping.Wrap,
				MaxHeight = 96.0
			});
		}
		border.Child = panel;
		return border;
	}

	private static Brush GetBorderBrush(FeedbackSeverity severity)
	{
		return severity switch
		{
			FeedbackSeverity.Success => new SolidColorBrush(Color.FromRgb(86, 158, 92)),
			FeedbackSeverity.Warning => new SolidColorBrush(Color.FromRgb(204, 155, 48)),
			FeedbackSeverity.Error => new SolidColorBrush(Color.FromRgb(196, 72, 72)),
			_ => new SolidColorBrush(Color.FromRgb(91, 139, 190))
		};
	}

	private void PositionNearOwner()
	{
		Rect bounds = GetOwnerBounds();
		Left = ClampToVirtualScreen(bounds.Right - ActualWidth - 24.0, ActualWidth, horizontal: true);
		Top = ClampToVirtualScreen(bounds.Bottom - ActualHeight - 24.0, ActualHeight, horizontal: false);
	}

	private Rect GetOwnerBounds()
	{
		return Owner != null && Owner.ActualWidth > 0.0 && Owner.ActualHeight > 0.0
			? new Rect(Owner.Left, Owner.Top, Owner.ActualWidth, Owner.ActualHeight)
			: SystemParameters.WorkArea;
	}

	private static double ClampToVirtualScreen(double value, double size, bool horizontal)
	{
		double minimum = (horizontal ? SystemParameters.VirtualScreenLeft : SystemParameters.VirtualScreenTop) + 8.0;
		double maximum = minimum + (horizontal ? SystemParameters.VirtualScreenWidth : SystemParameters.VirtualScreenHeight) - size - 16.0;
		return Math.Max(minimum, Math.Min(value, maximum));
	}
}

internal sealed class FeedbackPromptWindow : Window
{
	private MessageBoxResult _result = MessageBoxResult.None;

	public FeedbackPromptWindow(string message, string title, MessageBoxButton buttons)
		: this(message, title, GetButtons(buttons))
	{
	}

	public FeedbackPromptWindow(string message, string title, params DialogButtonChoice[] choices)
		: this(message, title, (System.Collections.Generic.IEnumerable<DialogButtonChoice>)choices)
	{
	}

	private FeedbackPromptWindow(string message, string title, System.Collections.Generic.IEnumerable<DialogButtonChoice> choices)
	{
		Title = title;
		Width = 430.0;
		SizeToContent = SizeToContent.Height;
		WindowStartupLocation = WindowStartupLocation.Manual;
		ShowInTaskbar = false;
		ResizeMode = ResizeMode.NoResize;
		Background = WindowBackgroundBrush;
		Foreground = PrimaryTextBrush;
		Topmost = true;
		Content = BuildContent(message, choices);
		Loaded += delegate { PositionNearOwner(); };
	}

	public MessageBoxResult ShowPrompt()
	{
		ShowDialog();
		return _result == MessageBoxResult.None ? MessageBoxResult.Cancel : _result;
	}

	private UIElement BuildContent(string message, System.Collections.Generic.IEnumerable<DialogButtonChoice> choices)
	{
		DockPanel root = new DockPanel
		{
			LastChildFill = true,
			Margin = new Thickness(14.0)
		};
		StackPanel buttonPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
		};
		foreach (DialogButtonChoice choice in choices)
		{
			Button button = new Button
			{
				Content = choice.Label,
				MinWidth = 86.0,
				Margin = new Thickness(8.0, 0.0, 0.0, 0.0),
				Padding = new Thickness(12.0, 4.0, 12.0, 4.0),
				IsDefault = choice.IsPrimary,
				IsCancel = choice.IsCancel,
				Style = CreatePromptButtonStyle(choice.IsPrimary)
			};
			button.Click += delegate
			{
				_result = choice.Result;
				DialogResult = true;
				Close();
			};
			buttonPanel.Children.Add(button);
		}
		DockPanel.SetDock(buttonPanel, Dock.Bottom);
		root.Children.Add(buttonPanel);

		TextBlock text = new TextBlock
		{
			Text = message,
			TextWrapping = TextWrapping.Wrap,
			Foreground = PrimaryTextBrush,
			MaxHeight = 260.0
		};
		root.Children.Add(text);
		return root;
	}

	private static Style CreatePromptButtonStyle(bool primary)
	{
		Brush background = primary ? AccentBrush : ButtonBackgroundBrush;
		Brush hoverBackground = primary ? AccentHoverBrush : ButtonHoverBackgroundBrush;
		Brush foreground = primary ? Brushes.White : PrimaryTextBrush;
		Brush border = primary ? AccentBrush : PromptBorderBrush;
		Style style = new Style(typeof(Button));
		style.Setters.Add(new Setter(Control.BackgroundProperty, background));
		style.Setters.Add(new Setter(Control.ForegroundProperty, foreground));
		style.Setters.Add(new Setter(Control.BorderBrushProperty, border));
		style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1.0)));
		style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));

		ControlTemplate template = new ControlTemplate(typeof(Button));
		FrameworkElementFactory buttonBorder = new FrameworkElementFactory(typeof(Border));
		buttonBorder.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
		buttonBorder.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
		buttonBorder.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = RelativeSource.TemplatedParent });
		buttonBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(3.0));
		FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
		presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
		presenter.SetBinding(ContentPresenter.MarginProperty, new Binding("Padding") { RelativeSource = RelativeSource.TemplatedParent });
		buttonBorder.AppendChild(presenter);
		template.VisualTree = buttonBorder;
		style.Setters.Add(new Setter(Control.TemplateProperty, template));

		Trigger hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
		hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, hoverBackground));
		hoverTrigger.Setters.Add(new Setter(Control.ForegroundProperty, foreground));
		style.Triggers.Add(hoverTrigger);

		Trigger disabledTrigger = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
		disabledTrigger.Setters.Add(new Setter(Control.ForegroundProperty, SecondaryTextBrush));
		disabledTrigger.Setters.Add(new Setter(Control.BackgroundProperty, DisabledButtonBackgroundBrush));
		style.Triggers.Add(disabledTrigger);
		return style;
	}

	private static DialogButtonChoice[] GetButtons(MessageBoxButton buttons)
	{
		return buttons switch
		{
			MessageBoxButton.YesNo => new[] { new DialogButtonChoice("Yes", MessageBoxResult.Yes, isPrimary: true), new DialogButtonChoice("No", MessageBoxResult.No, isCancel: true) },
			MessageBoxButton.OKCancel => new[] { new DialogButtonChoice("OK", MessageBoxResult.OK, isPrimary: true), new DialogButtonChoice("Cancel", MessageBoxResult.Cancel, isCancel: true) },
			MessageBoxButton.YesNoCancel => new[] { new DialogButtonChoice("Yes", MessageBoxResult.Yes, isPrimary: true), new DialogButtonChoice("No", MessageBoxResult.No), new DialogButtonChoice("Cancel", MessageBoxResult.Cancel, isCancel: true) },
			_ => new[] { new DialogButtonChoice("OK", MessageBoxResult.OK, isPrimary: true) }
		};
	}

	private void PositionNearOwner()
	{
		Rect bounds = GetOwnerBounds();
		Left = ClampToVirtualScreen(bounds.Right - ActualWidth - 32.0, ActualWidth, horizontal: true);
		Top = ClampToVirtualScreen(bounds.Bottom - ActualHeight - 96.0, ActualHeight, horizontal: false);
	}

	private Rect GetOwnerBounds()
	{
		return Owner != null && Owner.ActualWidth > 0.0 && Owner.ActualHeight > 0.0
			? new Rect(Owner.Left, Owner.Top, Owner.ActualWidth, Owner.ActualHeight)
			: SystemParameters.WorkArea;
	}

	private static double ClampToVirtualScreen(double value, double size, bool horizontal)
	{
		double minimum = (horizontal ? SystemParameters.VirtualScreenLeft : SystemParameters.VirtualScreenTop) + 8.0;
		double maximum = minimum + (horizontal ? SystemParameters.VirtualScreenWidth : SystemParameters.VirtualScreenHeight) - size - 16.0;
		return Math.Max(minimum, Math.Min(value, maximum));
	}

	private static bool IsDarkTheme => FrameworkApplication.ApplicationTheme == ApplicationTheme.Dark;

	private static Brush WindowBackgroundBrush => IsDarkTheme
		? new SolidColorBrush(Color.FromRgb(45, 45, 48))
		: new SolidColorBrush(Color.FromRgb(250, 250, 250));

	private static Brush ButtonBackgroundBrush => IsDarkTheme
		? new SolidColorBrush(Color.FromRgb(58, 58, 62))
		: new SolidColorBrush(Color.FromRgb(242, 242, 242));

	private static Brush ButtonHoverBackgroundBrush => IsDarkTheme
		? new SolidColorBrush(Color.FromRgb(72, 72, 78))
		: new SolidColorBrush(Color.FromRgb(230, 240, 252));

	private static Brush DisabledButtonBackgroundBrush => IsDarkTheme
		? new SolidColorBrush(Color.FromRgb(48, 48, 52))
		: new SolidColorBrush(Color.FromRgb(235, 235, 235));

	private static Brush PrimaryTextBrush => IsDarkTheme
		? new SolidColorBrush(Color.FromRgb(242, 242, 242))
		: new SolidColorBrush(Color.FromRgb(20, 20, 20));

	private static Brush SecondaryTextBrush => IsDarkTheme
		? new SolidColorBrush(Color.FromRgb(156, 156, 156))
		: new SolidColorBrush(Color.FromRgb(112, 112, 112));

	private static Brush PromptBorderBrush => IsDarkTheme
		? new SolidColorBrush(Color.FromRgb(96, 96, 100))
		: new SolidColorBrush(Color.FromRgb(168, 168, 168));

	private static Brush AccentBrush => new SolidColorBrush(Color.FromRgb(51, 153, 255));

	private static Brush AccentHoverBrush => new SolidColorBrush(Color.FromRgb(32, 128, 224));
}
