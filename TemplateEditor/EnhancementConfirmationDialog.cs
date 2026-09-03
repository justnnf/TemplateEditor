using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TemplateEditor;

internal sealed class EnhancementConfirmationDialog : Window
{
	private readonly string _message;

	private readonly string _confirmLabel;

	private readonly string _cancelLabel;

	private static Brush WindowBackgroundBrush => DialogAppearance.Background;

	private static Brush PrimaryTextBrush => DialogAppearance.Foreground;

	private static Brush ControlBorderBrush => DialogAppearance.ControlBorder;

	private static Brush ButtonBackgroundBrush => DialogAppearance.ButtonBackground;

	private static Brush ButtonHoverBrush => DialogAppearance.ButtonHoverBackground;

	public EnhancementConfirmationDialog(string title, string message)
		: this(title, message, "Yes", "No")
	{
	}

	public EnhancementConfirmationDialog(string title, string message, string confirmLabel, string cancelLabel)
	{
		_message = message;
		_confirmLabel = (string.IsNullOrWhiteSpace(confirmLabel) ? "Yes" : confirmLabel);
		_cancelLabel = (string.IsNullOrWhiteSpace(cancelLabel) ? "No" : cancelLabel);
		base.Title = title;
		base.Width = 360.0;
		base.SizeToContent = SizeToContent.Height;
		base.WindowStartupLocation = WindowStartupLocation.Manual;
		base.ResizeMode = ResizeMode.NoResize;
		base.Topmost = true;
		base.Background = WindowBackgroundBrush;
		base.Foreground = PrimaryTextBrush;
		base.FontFamily = new FontFamily("Segoe UI");
		base.FontSize = 12.0;
		base.Content = DialogAppearance.WithChrome(this, title, BuildContent());
		base.Loaded += delegate
		{
			WindowPlacementHelper.PositionAwayFromMapCenter(this);
		};
	}

	private UIElement BuildContent()
	{
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(14.0),
			Background = WindowBackgroundBrush
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = _message,
			TextWrapping = TextWrapping.Wrap,
			Foreground = PrimaryTextBrush,
			MaxWidth = 320.0
		});
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
		};
		Button button = new Button
		{
			Content = _cancelLabel,
			MinWidth = 72.0,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
			Padding = new Thickness(10.0, 3.0, 10.0, 3.0),
			IsCancel = true,
			Style = CreateButtonStyle()
		};
		button.Click += delegate
		{
			base.DialogResult = false;
			Close();
		};
		Button button2 = new Button
		{
			Content = _confirmLabel,
			MinWidth = 72.0,
			Padding = new Thickness(10.0, 3.0, 10.0, 3.0),
			IsDefault = true,
			Style = CreateButtonStyle()
		};
		button2.Click += delegate
		{
			base.DialogResult = true;
			Close();
		};
		stackPanel2.Children.Add(button);
		stackPanel2.Children.Add(button2);
		stackPanel.Children.Add(stackPanel2);
		return stackPanel;
	}

	private static Style CreateButtonStyle()
	{
		Style style = new Style(typeof(Button));
		style.Setters.Add(new Setter(Control.BackgroundProperty, ButtonBackgroundBrush));
		style.Setters.Add(new Setter(Control.ForegroundProperty, PrimaryTextBrush));
		style.Setters.Add(new Setter(Control.BorderBrushProperty, ControlBorderBrush));
		style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1.0)));
		style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
		DialogAppearance.ApplySquareButtonTemplate(style);
		Trigger trigger = new Trigger
		{
			Property = UIElement.IsMouseOverProperty,
			Value = true
		};
		trigger.Setters.Add(new Setter(Control.BackgroundProperty, ButtonHoverBrush));
		trigger.Setters.Add(new Setter(Control.ForegroundProperty, PrimaryTextBrush));
		style.Triggers.Add(trigger);
		return style;
	}
}
