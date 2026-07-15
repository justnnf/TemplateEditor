using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcGIS.Desktop.Framework;

namespace TemplateEditor;

internal enum CandidateChoiceResult
{
	UseCandidate,
	PreviousCandidate,
	NextCandidate,
	Skip
}

internal sealed class CandidateChoiceDialog : Window
{
	private static bool IsDarkTheme => FrameworkApplication.ApplicationTheme == ApplicationTheme.Dark;
	private static Brush WindowBackgroundBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(45, 45, 48)) : new SolidColorBrush(Color.FromRgb(243, 243, 243));
	private static Brush SurfaceBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(31, 31, 31)) : Brushes.White;
	private static Brush PrimaryTextBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(242, 242, 242)) : new SolidColorBrush(Color.FromRgb(32, 32, 32));
	private static Brush ControlBorderBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(96, 96, 100)) : new SolidColorBrush(Color.FromRgb(150, 150, 150));
	private static Brush ButtonBackgroundBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(58, 58, 62)) : new SolidColorBrush(Color.FromRgb(232, 232, 232));
	private static Brush ButtonHoverBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(72, 72, 78)) : new SolidColorBrush(Color.FromRgb(225, 235, 245));

	private readonly Button _nextButton;
	private readonly Button _previousButton;

	public CandidateChoiceResult Result { get; private set; } = CandidateChoiceResult.Skip;

	public CandidateChoiceDialog(string title, string prompt, string candidateLabel, bool canMovePrevious, bool canMoveNext)
	{
		Title = title;
		Width = 440.0;
		SizeToContent = SizeToContent.Height;
		WindowStartupLocation = WindowStartupLocation.Manual;
		ResizeMode = ResizeMode.NoResize;
		Topmost = true;
		Background = WindowBackgroundBrush;
		Foreground = PrimaryTextBrush;
		FontFamily = new FontFamily("Segoe UI");
		FontSize = 12.0;
		_previousButton = new Button
		{
			Content = "Back",
			MinWidth = 88.0,
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0),
			IsEnabled = canMovePrevious,
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0),
			Style = CreateButtonStyle()
		};
		_previousButton.Click += delegate
		{
			Result = CandidateChoiceResult.PreviousCandidate;
			DialogResult = true;
			Close();
		};
		_nextButton = new Button
		{
			Content = "Next",
			MinWidth = 88.0,
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0),
			IsEnabled = canMoveNext,
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0),
			Style = CreateButtonStyle()
		};
		_nextButton.Click += delegate
		{
			Result = CandidateChoiceResult.NextCandidate;
			DialogResult = true;
			Close();
		};
		Content = BuildContent(prompt, candidateLabel);
		Loaded += delegate { WindowPlacementHelper.PositionAwayFromMapCenter(this); };
	}

	private UIElement BuildContent(string prompt, string candidateLabel)
	{
		StackPanel root = new StackPanel
		{
			Margin = new Thickness(16.0),
			Background = WindowBackgroundBrush
		};
		root.Children.Add(new TextBlock
		{
			Text = prompt,
			TextWrapping = TextWrapping.Wrap,
			Foreground = PrimaryTextBrush
		});
		root.Children.Add(new Border
		{
			BorderThickness = new Thickness(1.0),
			BorderBrush = ControlBorderBrush,
			Background = SurfaceBrush,
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0),
			Padding = new Thickness(12.0),
			Child = new TextBlock
			{
				Text = candidateLabel,
				TextWrapping = TextWrapping.Wrap,
				Foreground = PrimaryTextBrush,
				MaxWidth = 390.0
			}
		});
		StackPanel buttons = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 16.0, 0.0, 0.0)
		};
		Button cancelButton = new Button
		{
			Content = "Cancel",
			MinWidth = 88.0,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0),
			Style = CreateButtonStyle()
		};
		cancelButton.Click += delegate
		{
			Result = CandidateChoiceResult.Skip;
			DialogResult = false;
			Close();
		};
		Button useButton = new Button
		{
			Content = "Use This",
			MinWidth = 88.0,
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0),
			Style = CreateButtonStyle()
		};
		useButton.Click += delegate
		{
			Result = CandidateChoiceResult.UseCandidate;
			DialogResult = true;
			Close();
		};
		buttons.Children.Add(cancelButton);
		buttons.Children.Add(_previousButton);
		buttons.Children.Add(_nextButton);
		buttons.Children.Add(useButton);
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
