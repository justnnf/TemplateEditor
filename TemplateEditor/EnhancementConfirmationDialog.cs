using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TemplateEditor;

internal sealed class EnhancementConfirmationDialog : Window
{
	private static readonly Brush WindowBackgroundBrush = new SolidColorBrush(Color.FromRgb(243, 243, 243));
	private static readonly Brush PrimaryTextBrush = new SolidColorBrush(Color.FromRgb(32, 32, 32));
	private static readonly Brush ControlBorderBrush = new SolidColorBrush(Color.FromRgb(150, 150, 150));
	private static readonly Brush ButtonHoverBrush = new SolidColorBrush(Color.FromRgb(225, 235, 245));

	private readonly string _message;

	public EnhancementConfirmationDialog(string title, string message)
	{
		_message = message;
		Title = title;
		Width = 360.0;
		SizeToContent = SizeToContent.Height;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		ResizeMode = ResizeMode.NoResize;
		Background = WindowBackgroundBrush;
		Foreground = PrimaryTextBrush;
		FontFamily = new FontFamily("Segoe UI");
		FontSize = 12.0;
		Content = BuildContent();
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
			Content = "No",
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
			Content = "Yes",
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
		style.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(232, 232, 232))));
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
		style.Triggers.Add(hoverTrigger);
		return style;
	}
}
