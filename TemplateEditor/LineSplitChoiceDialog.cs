using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcGIS.Desktop.Framework;

namespace TemplateEditor;

internal sealed class LineSplitChoiceDialog : Window
{
	private static bool IsDarkTheme => FrameworkApplication.ApplicationTheme == ApplicationTheme.Dark;
	private static Brush WindowBackgroundBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(45, 45, 48)) : new SolidColorBrush(Color.FromRgb(243, 243, 243));
	private static Brush SurfaceBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(31, 31, 31)) : Brushes.White;
	private static Brush PrimaryTextBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(242, 242, 242)) : new SolidColorBrush(Color.FromRgb(32, 32, 32));
	private static Brush ControlBorderBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(96, 96, 100)) : new SolidColorBrush(Color.FromRgb(150, 150, 150));
	private static Brush ButtonBackgroundBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(58, 58, 62)) : new SolidColorBrush(Color.FromRgb(232, 232, 232));
	private static Brush ButtonHoverBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(72, 72, 78)) : new SolidColorBrush(Color.FromRgb(225, 235, 245));

	private readonly CheckBox _startCheckBox;

	private readonly CheckBox _endCheckBox;

	public bool SplitAtStart => _startCheckBox != null && _startCheckBox.IsChecked == true;

	public bool SplitAtEnd => _endCheckBox != null && _endCheckBox.IsChecked == true;

	public LineSplitChoiceDialog(IEnumerable<string> options)
	{
		List<string> optionList = options?.ToList() ?? new List<string>();
		Title = "Split Underlying Line";
		Width = 380.0;
		SizeToContent = SizeToContent.Height;
		WindowStartupLocation = WindowStartupLocation.Manual;
		ResizeMode = ResizeMode.NoResize;
		Topmost = true;
		Background = WindowBackgroundBrush;
		Foreground = PrimaryTextBrush;
		FontFamily = new FontFamily("Segoe UI");
		FontSize = 12.0;
		_startCheckBox = optionList.Contains("Start") ? new CheckBox
		{
			Content = "Insert/start point",
			IsChecked = true,
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0),
			Foreground = PrimaryTextBrush
		} : null;
		_endCheckBox = optionList.Contains("End") ? new CheckBox
		{
			Content = "End point",
			IsChecked = true,
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0),
			Foreground = PrimaryTextBrush
		} : null;
		Content = BuildContent();
		Loaded += delegate { WindowPlacementHelper.PositionAwayFromMapCenter(this); };
	}

	private UIElement BuildContent()
	{
		StackPanel root = new StackPanel
		{
			Margin = new Thickness(16.0),
			Background = WindowBackgroundBrush
		};
		root.Children.Add(new TextBlock
		{
			Text = "Choose which point on the placed line should split the existing underlying line.",
			TextWrapping = TextWrapping.Wrap,
			Foreground = PrimaryTextBrush,
			MaxWidth = 340.0
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
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0),
			Style = CreateButtonStyle()
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
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0),
			Style = CreateButtonStyle()
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
