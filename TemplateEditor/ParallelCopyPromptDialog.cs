using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TemplateEditor;

internal sealed class ParallelCopyPromptDialog : Window
{
	private static readonly Brush WindowBackgroundBrush = new SolidColorBrush(Color.FromRgb(243, 243, 243));
	private static readonly Brush SurfaceBrush = Brushes.White;
	private static readonly Brush PrimaryTextBrush = new SolidColorBrush(Color.FromRgb(32, 32, 32));
	private static readonly Brush ControlBorderBrush = new SolidColorBrush(Color.FromRgb(150, 150, 150));
	private static readonly Brush ButtonHoverBrush = new SolidColorBrush(Color.FromRgb(225, 235, 245));

	private readonly TextBox _offsetTextBox;
	private readonly RadioButton _leftRadioButton;
	private readonly RadioButton _rightRadioButton;

	public double OffsetDistance { get; private set; }

	public bool LeftSide => _leftRadioButton.IsChecked == true;

	private ParallelCopyPromptDialog()
	{
		Title = "Create Parallel Copy";
		Width = 360.0;
		Height = 195.0;
		MinHeight = 195.0;
		ResizeMode = ResizeMode.NoResize;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		Background = WindowBackgroundBrush;
		Foreground = PrimaryTextBrush;

		Grid panel = new Grid
		{
			Margin = new Thickness(16.0),
			Background = WindowBackgroundBrush
		};
		panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		panel.Children.Add(new TextBlock
		{
			Text = "Create parallel copy from selected line?",
			FontWeight = FontWeights.SemiBold,
			Foreground = PrimaryTextBrush,
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		});

		Grid inputGrid = new Grid
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		};
		inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
		TextBlock label = new TextBlock
		{
			Text = "Offset (m)",
			VerticalAlignment = VerticalAlignment.Center,
			Foreground = PrimaryTextBrush,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0)
		};
		inputGrid.Children.Add(label);
		_offsetTextBox = new TextBox
		{
			Text = "1",
			MinWidth = 90.0,
			Background = SurfaceBrush,
			Foreground = PrimaryTextBrush,
			CaretBrush = PrimaryTextBrush,
			BorderBrush = ControlBorderBrush,
			SelectionBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
			SelectionTextBrush = Brushes.White,
			Padding = new Thickness(6.0, 3.0, 6.0, 3.0),
			Style = CreateTextBoxStyle()
		};
		Grid.SetColumn(_offsetTextBox, 1);
		inputGrid.Children.Add(_offsetTextBox);
		Grid.SetRow(inputGrid, 1);
		panel.Children.Add(inputGrid);

		StackPanel sidePanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0)
		};
		_leftRadioButton = new RadioButton
		{
			Content = "Left",
			IsChecked = true,
			Foreground = PrimaryTextBrush,
			Margin = new Thickness(0.0, 0.0, 18.0, 0.0)
		};
		sidePanel.Children.Add(_leftRadioButton);
		_rightRadioButton = new RadioButton
		{
			Content = "Right",
			Foreground = PrimaryTextBrush
		};
		sidePanel.Children.Add(_rightRadioButton);
		Grid.SetRow(sidePanel, 2);
		panel.Children.Add(sidePanel);

		StackPanel buttons = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 4.0, 0.0, 0.0)
		};
		Button createButton = new Button
		{
			Content = "Create",
			MinWidth = 72.0,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0),
			Style = CreateButtonStyle()
		};
		createButton.Click += OnCreateClicked;
		buttons.Children.Add(createButton);
		Button drawButton = new Button
		{
			Content = "Draw instead",
			MinWidth = 96.0,
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0),
			Style = CreateButtonStyle()
		};
		drawButton.Click += delegate { DialogResult = false; };
		buttons.Children.Add(drawButton);
		Grid.SetRow(buttons, 3);
		panel.Children.Add(buttons);
		Content = panel;
	}

	public static ParallelCopyPromptDialog ShowPrompt()
	{
		ParallelCopyPromptDialog dialog = new ParallelCopyPromptDialog();
		Window mainWindow = Application.Current?.MainWindow;
		if (mainWindow != null)
		{
			dialog.Owner = mainWindow;
		}
		return dialog.ShowDialog() == true ? dialog : null;
	}

	private static Style CreateTextBoxStyle()
	{
		Style style = new Style(typeof(TextBox));
		style.Setters.Add(new Setter(Control.BackgroundProperty, SurfaceBrush));
		style.Setters.Add(new Setter(Control.ForegroundProperty, PrimaryTextBrush));
		style.Setters.Add(new Setter(Control.BorderBrushProperty, ControlBorderBrush));
		style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1.0)));
		style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6.0, 3.0, 6.0, 3.0)));
		style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
		return style;
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

	private void OnCreateClicked(object sender, RoutedEventArgs e)
	{
		if (!TryParsePositiveDistance(_offsetTextBox.Text, out double offsetDistance))
		{
			DialogService.Show("Enter a positive offset distance.", "Template Editor");
			return;
		}
		OffsetDistance = offsetDistance;
		DialogResult = true;
	}

	private static bool TryParsePositiveDistance(string text, out double distance)
	{
		bool parsed = double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out distance) ||
			double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out distance);
		return parsed && distance > 0.0;
	}
}
