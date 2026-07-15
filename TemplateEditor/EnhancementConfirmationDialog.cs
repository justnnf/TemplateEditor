using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcGIS.Desktop.Framework;

namespace TemplateEditor;

internal sealed class EnhancementConfirmationDialog : Window
{
	private static bool IsDarkTheme => FrameworkApplication.ApplicationTheme == ApplicationTheme.Dark;
	private static Brush WindowBackgroundBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(45, 45, 48)) : new SolidColorBrush(Color.FromRgb(243, 243, 243));
	private static Brush PrimaryTextBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(242, 242, 242)) : new SolidColorBrush(Color.FromRgb(32, 32, 32));
	private static Brush ControlBorderBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(96, 96, 100)) : new SolidColorBrush(Color.FromRgb(150, 150, 150));
	private static Brush ButtonBackgroundBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(58, 58, 62)) : new SolidColorBrush(Color.FromRgb(232, 232, 232));
	private static Brush ButtonHoverBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(72, 72, 78)) : new SolidColorBrush(Color.FromRgb(225, 235, 245));

	private readonly string _message;
	private readonly string _confirmLabel;
	private readonly string _cancelLabel;

	public EnhancementConfirmationDialog(string title, string message)
		: this(title, message, "Yes", "No")
	{
	}

	public EnhancementConfirmationDialog(string title, string message, string confirmLabel, string cancelLabel)
	{
		_message = message;
		_confirmLabel = string.IsNullOrWhiteSpace(confirmLabel) ? "Yes" : confirmLabel;
		_cancelLabel = string.IsNullOrWhiteSpace(cancelLabel) ? "No" : cancelLabel;
		Title = title;
		Width = 360.0;
		SizeToContent = SizeToContent.Height;
		WindowStartupLocation = WindowStartupLocation.Manual;
		ResizeMode = ResizeMode.NoResize;
		Topmost = true;
		Background = WindowBackgroundBrush;
		Foreground = PrimaryTextBrush;
		FontFamily = new FontFamily("Segoe UI");
		FontSize = 12.0;
		Content = BuildContent();
		Loaded += delegate { WindowPlacementHelper.PositionAwayFromMapCenter(this); };
	}

	private UIElement BuildContent()
	{
		StackPanel root = new StackPanel
		{
			Margin = new Thickness(14.0),
			Background = WindowBackgroundBrush
		};
		root.Children.Add(new TextBlock
		{
			Text = _message,
			TextWrapping = TextWrapping.Wrap,
			Foreground = PrimaryTextBrush,
			MaxWidth = 320.0
		});
		StackPanel buttons = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
		};
		Button noButton = new Button
		{
			Content = _cancelLabel,
			MinWidth = 72.0,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
			Padding = new Thickness(10.0, 3.0, 10.0, 3.0),
			IsCancel = true,
			Style = CreateButtonStyle()
		};
		noButton.Click += delegate
		{
			DialogResult = false;
			Close();
		};
		Button yesButton = new Button
		{
			Content = _confirmLabel,
			MinWidth = 72.0,
			Padding = new Thickness(10.0, 3.0, 10.0, 3.0),
			IsDefault = true,
			Style = CreateButtonStyle()
		};
		yesButton.Click += delegate
		{
			DialogResult = true;
			Close();
		};
		buttons.Children.Add(noButton);
		buttons.Children.Add(yesButton);
		root.Children.Add(buttons);
		return root;
	}

	private static Style CreateButtonStyle()
	{
		Style style = new Style(typeof(Button));
		style.Setters.Add(new Setter(Control.BackgroundProperty, ButtonBackgroundBrush));
		style.Setters.Add(new Setter(Control.ForegroundProperty, PrimaryTextBrush));
		style.Setters.Add(new Setter(Control.BorderBrushProperty, ControlBorderBrush));
		style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1.0)));
		style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
		Trigger hoverTrigger = new Trigger
		{
			Property = UIElement.IsMouseOverProperty,
			Value = true
		};
		hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, ButtonHoverBrush));
		hoverTrigger.Setters.Add(new Setter(Control.ForegroundProperty, PrimaryTextBrush));
		style.Triggers.Add(hoverTrigger);
		return style;
	}

}
