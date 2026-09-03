using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace TemplateEditor;

internal sealed class ParallelCopyPromptDialog : Window
{
	private readonly TextBox _offsetTextBox;

	private readonly RadioButton _leftRadioButton;

	private readonly RadioButton _rightRadioButton;

	private IDisposable _previewOverlay;

	private int _previewVersion;

	private static Brush WindowBackgroundBrush => DialogAppearance.Background;

	private static Brush SurfaceBrush => DialogAppearance.InputBackground;

	private static Brush PrimaryTextBrush => DialogAppearance.Foreground;

	private static Brush ControlBorderBrush => DialogAppearance.ControlBorder;

	private static Brush ButtonBackgroundBrush => DialogAppearance.ButtonBackground;

	private static Brush ButtonHoverBrush => DialogAppearance.ButtonHoverBackground;

	public double OffsetDistance { get; private set; }

	public bool LeftSide => _leftRadioButton.IsChecked == true;

	private ParallelCopyPromptDialog(double defaultOffsetDistance, bool defaultLeftSide)
	{
		base.Title = "Create Parallel Copy";
		base.Width = 360.0;
		base.SizeToContent = SizeToContent.Height;
		base.ResizeMode = ResizeMode.NoResize;
		base.WindowStartupLocation = WindowStartupLocation.Manual;
		base.Background = WindowBackgroundBrush;
		base.Foreground = PrimaryTextBrush;
		Grid grid = new Grid
		{
			Margin = new Thickness(16.0),
			Background = WindowBackgroundBrush
		};
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.Children.Add(new TextBlock
		{
			Text = "Create parallel copy from selected line?",
			FontWeight = FontWeights.SemiBold,
			Foreground = PrimaryTextBrush,
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		});
		Grid grid2 = new Grid
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		};
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		TextBlock element = new TextBlock
		{
			Text = "Offset (m)",
			VerticalAlignment = VerticalAlignment.Center,
			Foreground = PrimaryTextBrush,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0)
		};
		grid2.Children.Add(element);
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
		grid2.Children.Add(_offsetTextBox);
		_offsetTextBox.TextChanged += delegate
		{
			QueuePreviewRefresh();
		};
		Grid.SetRow(grid2, 1);
		grid.Children.Add(grid2);
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0)
		};
		_leftRadioButton = new RadioButton
		{
			Content = "Left",
			IsChecked = defaultLeftSide,
			Foreground = PrimaryTextBrush,
			Margin = new Thickness(0.0, 0.0, 6.0, 0.0),
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0),
			Style = CreateSegmentedRadioButtonStyle()
		};
		_leftRadioButton.Checked += delegate
		{
			QueuePreviewRefresh();
		};
		stackPanel.Children.Add(_leftRadioButton);
		_rightRadioButton = new RadioButton
		{
			Content = "Right",
			IsChecked = !defaultLeftSide,
			Foreground = PrimaryTextBrush,
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0),
			Style = CreateSegmentedRadioButtonStyle()
		};
		_rightRadioButton.Checked += delegate
		{
			QueuePreviewRefresh();
		};
		stackPanel.Children.Add(_rightRadioButton);
		Grid.SetRow(stackPanel, 2);
		grid.Children.Add(stackPanel);
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 4.0, 0.0, 0.0)
		};
		Button button = new Button
		{
			Content = "Create",
			MinWidth = 72.0,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0),
			Style = CreateButtonStyle()
		};
		button.Click += OnCreateClicked;
		stackPanel2.Children.Add(button);
		Button button2 = new Button
		{
			Content = "Draw instead",
			MinWidth = 96.0,
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0),
			Style = CreateButtonStyle()
		};
		button2.Click += delegate
		{
			base.DialogResult = false;
		};
		stackPanel2.Children.Add(button2);
		Grid.SetRow(stackPanel2, 3);
		grid.Children.Add(stackPanel2);
		base.Content = DialogAppearance.WithChrome(this, "Create Parallel Copy", grid);
		base.Loaded += delegate
		{
			WindowPlacementHelper.PositionAwayFromMapCenter(this);
			QueuePreviewRefresh();
		};
		base.Closed += delegate
		{
			ClearPreviewOverlay();
		};
	}

	public static ParallelCopyPromptDialog ShowPrompt(double defaultOffsetDistance, bool defaultLeftSide)
	{
		ParallelCopyPromptDialog parallelCopyPromptDialog = new ParallelCopyPromptDialog(defaultOffsetDistance, defaultLeftSide);
		Window window = Application.Current?.MainWindow;
		if (window != null)
		{
			parallelCopyPromptDialog.Owner = window;
		}
		return (parallelCopyPromptDialog.ShowDialog() == true) ? parallelCopyPromptDialog : null;
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

	private static Style CreateSegmentedRadioButtonStyle()
	{
		Style style = new Style(typeof(RadioButton));
		style.Setters.Add(new Setter(Control.BackgroundProperty, ButtonBackgroundBrush));
		style.Setters.Add(new Setter(Control.ForegroundProperty, PrimaryTextBrush));
		style.Setters.Add(new Setter(Control.BorderBrushProperty, ControlBorderBrush));
		style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1.0)));
		style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
		ControlTemplate controlTemplate = new ControlTemplate(typeof(RadioButton));
		FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
		border.SetBinding(Border.BackgroundProperty, new Binding("Background")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		border.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		border.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
		content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		content.SetBinding(FrameworkElement.MarginProperty, new Binding("Padding")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		border.AppendChild(content);
		controlTemplate.VisualTree = border;
		style.Setters.Add(new Setter(Control.TemplateProperty, controlTemplate));
		Trigger selectedTrigger = new Trigger
		{
			Property = ToggleButton.IsCheckedProperty,
			Value = true
		};
		selectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, DialogAppearance.Accent));
		selectedTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, DialogAppearance.Accent));
		selectedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
		style.Triggers.Add(selectedTrigger);
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
		if (!TryParsePositiveDistance(_offsetTextBox.Text, out var distance))
		{
			DialogService.Show("Enter a positive offset distance.", "Template Editor");
			return;
		}
		OffsetDistance = distance;
		ClearPreviewOverlay();
		base.DialogResult = true;
	}

	private void QueuePreviewRefresh()
	{
		RefreshPreviewAsync(++_previewVersion);
	}

	private async Task RefreshPreviewAsync(int previewVersion)
	{
		if (!TryParsePositiveDistance(_offsetTextBox?.Text, out var offsetDistance))
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
		catch (Exception exception)
		{
			LogService.LogException("Parallel copy preview overlay update failed.", exception);
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
		return (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out distance) || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out distance)) && distance > 0.0;
	}
}
