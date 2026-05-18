using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace TemplateEditor;

internal sealed class LineSplitChoiceDialog : Window
{
	private readonly CheckBox _startCheckBox;

	private readonly CheckBox _endCheckBox;

	public bool SplitAtStart => _startCheckBox != null && _startCheckBox.IsChecked == true;

	public bool SplitAtEnd => _endCheckBox != null && _endCheckBox.IsChecked == true;

	public LineSplitChoiceDialog(IEnumerable<string> options)
	{
		List<string> optionList = options?.ToList() ?? new List<string>();
		Title = "Split Underlying Line";
		Width = 420.0;
		Height = 230.0;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		ResizeMode = ResizeMode.NoResize;
		_startCheckBox = optionList.Contains("Start") ? new CheckBox
		{
			Content = "Split at the start point",
			IsChecked = true,
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
		} : null;
		_endCheckBox = optionList.Contains("End") ? new CheckBox
		{
			Content = "Split at the end point",
			IsChecked = true,
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
		} : null;
		Content = BuildContent();
	}

	private UIElement BuildContent()
	{
		StackPanel root = new StackPanel
		{
			Margin = new Thickness(16.0)
		};
		root.Children.Add(new TextBlock
		{
			Text = "Choose which line endpoints should trigger a split on the underlying line.",
			TextWrapping = TextWrapping.Wrap
		});
		if (_startCheckBox != null)
		{
			root.Children.Add(_startCheckBox);
		}
		if (_endCheckBox != null)
		{
			root.Children.Add(_endCheckBox);
		}
		StackPanel buttons = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 16.0, 0.0, 0.0)
		};
		Button cancelButton = new Button
		{
			Content = "Skip",
			MinWidth = 88.0,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0)
		};
		cancelButton.Click += delegate
		{
			DialogResult = false;
			Close();
		};
		Button applyButton = new Button
		{
			Content = "Apply",
			MinWidth = 88.0,
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0)
		};
		applyButton.Click += delegate
		{
			if (!SplitAtStart && !SplitAtEnd)
			{
				DialogResult = false;
			}
			else
			{
				DialogResult = true;
			}
			Close();
		};
		buttons.Children.Add(cancelButton);
		buttons.Children.Add(applyButton);
		root.Children.Add(buttons);
		return root;
	}
}
