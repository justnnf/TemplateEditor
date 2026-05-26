using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
	private static readonly Brush WindowBackgroundBrush = new SolidColorBrush(Color.FromRgb(243, 243, 243));
	private static readonly Brush SurfaceBrush = Brushes.White;
	private static readonly Brush PrimaryTextBrush = new SolidColorBrush(Color.FromRgb(32, 32, 32));
	private static readonly Brush ControlBorderBrush = new SolidColorBrush(Color.FromRgb(150, 150, 150));
	private static readonly Brush ButtonHoverBrush = new SolidColorBrush(Color.FromRgb(225, 235, 245));

	private readonly Button _nextButton;
	private readonly Button _previousButton;

	public CandidateChoiceResult Result { get; private set; } = CandidateChoiceResult.Skip;

	public CandidateChoiceDialog(string title, string prompt, string candidateLabel, bool canMovePrevious, bool canMoveNext)
	{
		Title = title;
		Width = 440.0;
		SizeToContent = SizeToContent.Height;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		ResizeMode = ResizeMode.NoResize;
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
