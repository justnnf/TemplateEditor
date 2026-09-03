using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TemplateEditor;

internal sealed class CandidateChoiceDialog : Window
{
	private static Brush WindowBackgroundBrush => DialogAppearance.Background;

	private static Brush SurfaceBrush => DialogAppearance.InputBackground;

	private static Brush PrimaryTextBrush => DialogAppearance.Foreground;

	private static Brush ControlBorderBrush => DialogAppearance.ControlBorder;

	private static Brush ButtonBackgroundBrush => DialogAppearance.ButtonBackground;

	private static Brush ButtonHoverBrush => DialogAppearance.ButtonHoverBackground;

	public CandidateChoiceResult Result { get; private set; } = CandidateChoiceResult.Skip;

	public int SelectedIndex { get; private set; } = -1;

	public CandidateChoiceDialog(string title, string prompt, IReadOnlyList<string> candidateLabels, Action<int> selectionChanged = null)
	{
		base.Title = title;
		base.Width = 560.0;
		base.SizeToContent = SizeToContent.Height;
		base.WindowStartupLocation = WindowStartupLocation.Manual;
		base.ResizeMode = ResizeMode.NoResize;
		base.Topmost = true;
		base.Background = WindowBackgroundBrush;
		base.Foreground = PrimaryTextBrush;
		base.FontFamily = new FontFamily("Segoe UI");
		base.FontSize = 12.0;
		base.Content = DialogAppearance.WithChrome(this, title, BuildContent(prompt, candidateLabels, selectionChanged));
		base.Loaded += delegate
		{
			WindowPlacementHelper.PositionAwayFromMapCenter(this);
		};
	}

	private UIElement BuildContent(string prompt, IReadOnlyList<string> candidateLabels, Action<int> selectionChanged)
	{
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(16.0),
			Background = WindowBackgroundBrush
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = prompt,
			TextWrapping = TextWrapping.Wrap,
			Foreground = PrimaryTextBrush
		});
		ListBox candidates = new ListBox
		{
			BorderBrush = ControlBorderBrush,
			Background = SurfaceBrush,
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0),
			MaxHeight = 260.0,
			Foreground = PrimaryTextBrush
		};
		foreach (string item in candidateLabels ?? Array.Empty<string>())
		{
			candidates.Items.Add(item);
		}
		candidates.SelectionChanged += delegate
		{
			SelectedIndex = candidates.SelectedIndex;
			if (SelectedIndex >= 0)
			{
				selectionChanged?.Invoke(SelectedIndex);
			}
		};
		if (candidates.Items.Count > 0)
		{
			candidates.SelectedIndex = 0;
		}
		stackPanel.Children.Add(candidates);
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 16.0, 0.0, 0.0)
		};
		Button button = new Button
		{
			Content = "Cancel",
			MinWidth = 88.0,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0),
			Style = CreateButtonStyle()
		};
		button.Click += delegate
		{
			Result = CandidateChoiceResult.Skip;
			base.DialogResult = false;
			Close();
		};
		Button button2 = new Button
		{
			Content = "Use Selected",
			MinWidth = 88.0,
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0),
			Style = CreateButtonStyle()
		};
		button2.Click += delegate
		{
			if (SelectedIndex >= 0)
			{
				Result = CandidateChoiceResult.UseCandidate;
				base.DialogResult = true;
				Close();
			}
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
