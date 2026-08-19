using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcGIS.Desktop.Framework;

namespace TemplateEditor;

internal sealed class LineSplitChoiceDialog : Window
{
	private readonly CheckBox _startCheckBox;

	private readonly CheckBox _endCheckBox;

	private static bool IsDarkTheme
	{
		get
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Invalid comparison between Unknown and I4
			return (int)FrameworkApplication.ApplicationTheme == 1;
		}
	}

	private static Brush WindowBackgroundBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(45, 45, 48)) : new SolidColorBrush(Color.FromRgb(243, 243, 243));

	private static Brush SurfaceBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(31, 31, 31)) : Brushes.White;

	private static Brush PrimaryTextBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(242, 242, 242)) : new SolidColorBrush(Color.FromRgb(32, 32, 32));

	private static Brush ControlBorderBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(96, 96, 100)) : new SolidColorBrush(Color.FromRgb(150, 150, 150));

	private static Brush ButtonBackgroundBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(58, 58, 62)) : new SolidColorBrush(Color.FromRgb(232, 232, 232));

	private static Brush ButtonHoverBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(72, 72, 78)) : new SolidColorBrush(Color.FromRgb(225, 235, 245));

	public bool SplitAtStart => _startCheckBox != null && _startCheckBox.IsChecked == true;

	public bool SplitAtEnd => _endCheckBox != null && _endCheckBox.IsChecked == true;

	public LineSplitChoiceDialog(IEnumerable<string> options)
	{
		List<string> list = options?.ToList() ?? new List<string>();
		base.Title = "Split Underlying Line";
		base.Width = 380.0;
		base.SizeToContent = SizeToContent.Height;
		base.WindowStartupLocation = WindowStartupLocation.Manual;
		base.ResizeMode = ResizeMode.NoResize;
		base.Topmost = true;
		base.Background = WindowBackgroundBrush;
		base.Foreground = PrimaryTextBrush;
		base.FontFamily = new FontFamily("Segoe UI");
		base.FontSize = 12.0;
		_startCheckBox = (list.Contains("Start") ? new CheckBox
		{
			Content = "Insert/start point",
			IsChecked = true,
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0),
			Foreground = PrimaryTextBrush
		} : null);
		_endCheckBox = (list.Contains("End") ? new CheckBox
		{
			Content = "End point",
			IsChecked = true,
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0),
			Foreground = PrimaryTextBrush
		} : null);
		base.Content = DialogAppearance.WithChrome(this, "Split Underlying Line", BuildContent());
		base.Loaded += delegate
		{
			WindowPlacementHelper.PositionAwayFromMapCenter(this);
		};
	}

	private UIElement BuildContent()
	{
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(16.0),
			Background = WindowBackgroundBrush
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = "Choose which point on the placed line should split the existing underlying line.",
			TextWrapping = TextWrapping.Wrap,
			Foreground = PrimaryTextBrush,
			MaxWidth = 340.0
		});
		if (_startCheckBox != null)
		{
			stackPanel.Children.Add(_startCheckBox);
		}
		if (_endCheckBox != null)
		{
			stackPanel.Children.Add(_endCheckBox);
		}
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 16.0, 0.0, 0.0)
		};
		Button button = new Button
		{
			Content = "Skip",
			MinWidth = 88.0,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0),
			Style = CreateButtonStyle()
		};
		button.Click += delegate
		{
			base.DialogResult = false;
			Close();
		};
		Button button2 = new Button
		{
			Content = "Apply",
			MinWidth = 88.0,
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0),
			Style = CreateButtonStyle()
		};
		button2.Click += delegate
		{
			if (!SplitAtStart && !SplitAtEnd)
			{
				base.DialogResult = false;
			}
			else
			{
				base.DialogResult = true;
			}
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
