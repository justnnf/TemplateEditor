using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcGIS.Desktop.Framework;

namespace TemplateEditor;

internal sealed class ParallelCopyPromptDialog : Window
{
	private static bool IsDarkTheme => FrameworkApplication.ApplicationTheme == ApplicationTheme.Dark;
	private static Brush WindowBackgroundBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(45, 45, 48)) : new SolidColorBrush(Color.FromRgb(243, 243, 243));
	private static Brush SurfaceBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(31, 31, 31)) : Brushes.White;
	private static Brush PrimaryTextBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(242, 242, 242)) : new SolidColorBrush(Color.FromRgb(32, 32, 32));
	private static Brush ControlBorderBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(96, 96, 100)) : new SolidColorBrush(Color.FromRgb(150, 150, 150));
	private static Brush ButtonBackgroundBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(58, 58, 62)) : new SolidColorBrush(Color.FromRgb(232, 232, 232));
	private static Brush ButtonHoverBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(72, 72, 78)) : new SolidColorBrush(Color.FromRgb(225, 235, 245));

	private readonly TextBox _offsetTextBox;
	private readonly RadioButton _leftRadioButton;
	private readonly RadioButton _rightRadioButton;
	private IDisposable _previewOverlay;
	private int _previewVersion;

	public double OffsetDistance { get; private set; }

	public bool LeftSide => _leftRadioButton.IsChecked == true;

	private ParallelCopyPromptDialog(double defaultOffsetDistance, bool defaultLeftSide)
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
			Text = defaultOffsetDistance.ToString("0.###", CultureInfo.CurrentCulture),
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
		_offsetTextBox.TextChanged += delegate { QueuePreviewRefresh(); };
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
			IsChecked = defaultLeftSide,
			Foreground = PrimaryTextBrush,
			Margin = new Thickness(0.0, 0.0, 18.0, 0.0)
		};
		_leftRadioButton.Checked += delegate { QueuePreviewRefresh(); };
		sidePanel.Children.Add(_leftRadioButton);
		_rightRadioButton = new RadioButton
		{
			Content = "Right",
			IsChecked = !defaultLeftSide,
			Foreground = PrimaryTextBrush
		};
		_rightRadioButton.Checked += delegate { QueuePreviewRefresh(); };
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
		Loaded += delegate { QueuePreviewRefresh(); };
		Closed += delegate { ClearPreviewOverlay(); };
	}

	public static ParallelCopyPromptDialog ShowPrompt(double defaultOffsetDistance, bool defaultLeftSide)
	{
		ParallelCopyPromptDialog dialog = new ParallelCopyPromptDialog(defaultOffsetDistance, defaultLeftSide);
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

	private void OnCreateClicked(object sender, RoutedEventArgs e)
	{
		if (!TryParsePositiveDistance(_offsetTextBox.Text, out double offsetDistance))
		{
			DialogService.Show("Enter a positive offset distance.", "Template Editor");
			return;
		}
		OffsetDistance = offsetDistance;
		ClearPreviewOverlay();
		DialogResult = true;
	}

	private void QueuePreviewRefresh()
	{
		int previewVersion = ++_previewVersion;
		_ = RefreshPreviewAsync(previewVersion);
	}

	private async Task RefreshPreviewAsync(int previewVersion)
	{
		if (!TryParsePositiveDistance(_offsetTextBox?.Text, out double offsetDistance))
		{
			ClearPreviewOverlay();
			return;
		}
		try
		{
			IDisposable previewOverlay = await ParallelCopyService.CreatePreviewOverlayAsync(offsetDistance, LeftSide);
			if (previewVersion != _previewVersion)
			{
				previewOverlay?.Dispose();
				return;
			}
			ClearPreviewOverlay();
			_previewOverlay = previewOverlay;
		}
		catch (Exception ex)
		{
			LogService.LogException("Parallel copy preview overlay update failed.", ex);
			if (previewVersion == _previewVersion)
			{
				ClearPreviewOverlay();
			}
		}
	}

	private void ClearPreviewOverlay()
	{
		_previewOverlay?.Dispose();
		_previewOverlay = null;
	}

	private static bool TryParsePositiveDistance(string text, out double distance)
	{
		bool parsed = double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out distance) ||
			double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out distance);
		return parsed && distance > 0.0;
	}
}
