using System.Windows;
using System.Windows.Controls;

namespace TemplateEditor;

internal enum CandidateChoiceResult
{
	UseCandidate,
	NextCandidate,
	Skip
}

internal sealed class CandidateChoiceDialog : Window
{
	private readonly Button _nextButton;

	public CandidateChoiceResult Result { get; private set; } = CandidateChoiceResult.Skip;

	public CandidateChoiceDialog(string title, string prompt, string candidateLabel, bool canMoveNext)
	{
		Title = title;
		Width = 440.0;
		Height = 220.0;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		ResizeMode = ResizeMode.NoResize;
		_nextButton = new Button
		{
			Content = "Next",
			MinWidth = 88.0,
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0),
			IsEnabled = canMoveNext,
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0)
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
			Margin = new Thickness(16.0)
		};
		root.Children.Add(new TextBlock
		{
			Text = prompt,
			TextWrapping = TextWrapping.Wrap
		});
		root.Children.Add(new Border
		{
			BorderThickness = new Thickness(1.0),
			BorderBrush = SystemColors.ControlDarkBrush,
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0),
			Padding = new Thickness(12.0),
			Child = new TextBlock
			{
				Text = candidateLabel,
				TextWrapping = TextWrapping.Wrap
			}
		});
		StackPanel buttons = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 16.0, 0.0, 0.0)
		};
		Button skipButton = new Button
		{
			Content = "Skip",
			MinWidth = 88.0,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0)
		};
		skipButton.Click += delegate
		{
			Result = CandidateChoiceResult.Skip;
			DialogResult = false;
			Close();
		};
		Button useButton = new Button
		{
			Content = "Use This",
			MinWidth = 88.0,
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0)
		};
		useButton.Click += delegate
		{
			Result = CandidateChoiceResult.UseCandidate;
			DialogResult = true;
			Close();
		};
		buttons.Children.Add(skipButton);
		buttons.Children.Add(_nextButton);
		buttons.Children.Add(useButton);
		root.Children.Add(buttons);
		return root;
	}
}
