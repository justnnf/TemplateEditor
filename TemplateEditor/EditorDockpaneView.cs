using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace TemplateEditor;

public class EditorDockpaneView : UserControl
{
	internal ListView listViewUnits;

	private GridViewColumn _nameColumn;

	private GridViewColumn _typeColumn;

	private GridViewColumn _descriptionColumn;

	public EditorDockpaneView()
	{
		InitializeComponent();
	}

	public void InitializeComponent()
	{
		Grid root = new Grid
		{
			Margin = new Thickness(6.0)
		};
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) });
		PreviewKeyDown += OnPreviewKeyDown;

		UIElement filterPanel = CreateFilterPanel();
		Grid.SetRow(filterPanel, 0);
		root.Children.Add(filterPanel);

		ListView templateList = CreateTemplateList();
		Grid.SetRow(templateList, 1);
		root.Children.Add(templateList);
		Content = root;
	}

	private static UIElement CreateFilterPanel()
	{
		Grid panel = new Grid
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
		};
		panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
		panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

		Grid searchContainer = new Grid
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 4.0),
			MinWidth = 240.0
		};
		TextBox searchBox = new TextBox
		{
			Padding = new Thickness(2.0, 2.0, 24.0, 2.0)
		};
		searchBox.SetBinding(TextBox.TextProperty, new Binding("SearchText")
		{
			UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
		});
		searchContainer.Children.Add(searchBox);

		Button clearSearchButton = new Button
		{
			Content = "X",
			Width = 18.0,
			Height = 18.0,
			Margin = new Thickness(0.0, 0.0, 4.0, 0.0),
			Padding = new Thickness(0.0),
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center,
			Foreground = Brushes.Red,
			Background = Brushes.Transparent,
			BorderBrush = Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			FontWeight = FontWeights.Bold,
			ToolTip = "Clear search",
			Focusable = false
		};
		clearSearchButton.SetBinding(Button.CommandProperty, new Binding("ClearSearchCommand"));
		clearSearchButton.Style = CreateClearSearchButtonStyle();
		searchContainer.Children.Add(clearSearchButton);

		Grid.SetColumnSpan(searchContainer, 2);
		panel.Children.Add(searchContainer);

		StackPanel templateTypePanel = CreateHorizontalPanel();
		templateTypePanel.Children.Add(CreateRadioButton("Groups", "ShowGroupTemplates"));
		templateTypePanel.Children.Add(CreateRadioButton("Simple", "ShowSimpleTemplates"));
		templateTypePanel.Children.Add(CreateRadioButton("All", "ShowAllTemplates"));
		Grid.SetRow(templateTypePanel, 1);
		panel.Children.Add(templateTypePanel);

		TextBlock count = new TextBlock
		{
			Foreground = new SolidColorBrush(Color.FromRgb(96, 96, 96)),
			Margin = new Thickness(12.0, 0.0, 0.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		count.SetBinding(TextBlock.TextProperty, new Binding("TemplateCount"));
		Grid.SetRow(count, 1);
		Grid.SetColumn(count, 1);
		panel.Children.Add(count);

		return panel;
	}

	private ListView CreateTemplateList()
	{
		listViewUnits = new ListView
		{
			MinHeight = 80.0,
			VerticalAlignment = VerticalAlignment.Stretch,
			HorizontalAlignment = HorizontalAlignment.Stretch
		};
		ScrollViewer.SetHorizontalScrollBarVisibility(listViewUnits, ScrollBarVisibility.Auto);
		ScrollViewer.SetVerticalScrollBarVisibility(listViewUnits, ScrollBarVisibility.Auto);
		ScrollViewer.SetCanContentScroll(listViewUnits, true);
		VirtualizingPanel.SetIsVirtualizing(listViewUnits, true);
		VirtualizingPanel.SetVirtualizationMode(listViewUnits, VirtualizationMode.Recycling);
		listViewUnits.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("Templates")
		{
			NotifyOnTargetUpdated = true
		});
		listViewUnits.SetBinding(Selector.SelectedItemProperty, new Binding("SelectedTemplate")
		{
			Mode = BindingMode.TwoWay
		});
		listViewUnits.PreviewMouseLeftButtonDown += OnTemplateListPreviewMouseLeftButtonDown;
		_nameColumn = new GridViewColumn { Header = CreateSortableHeader("Name", "Name"), CellTemplate = CreateNameCellTemplate(), Width = 220.0 };
		_typeColumn = new GridViewColumn { Header = CreateSortableHeader("Type", "TemplateType"), CellTemplate = CreateTextCellTemplate("TemplateType"), Width = 160.0 };
		_descriptionColumn = new GridViewColumn { Header = CreateSortableHeader("Description", "Description"), CellTemplate = CreateTextCellTemplate("Description"), Width = 360.0 };
		listViewUnits.View = new GridView
		{
			Columns =
			{
				_nameColumn,
				_typeColumn,
				_descriptionColumn
			}
		};
		listViewUnits.Loaded += delegate { QueueAutoSizeColumns(); };
		listViewUnits.SizeChanged += delegate { QueueAutoSizeColumns(); };
		listViewUnits.TargetUpdated += delegate { QueueAutoSizeColumns(); };
		return listViewUnits;
	}

	private void OnTemplateListPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (IsFromGroupToggleButton(e.OriginalSource as DependencyObject))
		{
			return;
		}
		ListViewItem item = ItemsControl.ContainerFromElement(listViewUnits, e.OriginalSource as DependencyObject) as ListViewItem;
		if (item?.DataContext == null)
		{
			return;
		}
		if (!Equals(item.DataContext, listViewUnits?.SelectedItem))
		{
			return;
		}
		if (DataContext is EditorDockpaneViewModel viewModel && viewModel.ActivateSelectedTemplateCommand.CanExecute(null))
		{
			viewModel.ActivateSelectedTemplateCommand.Execute(null);
		}
	}

	private static bool IsFromGroupToggleButton(DependencyObject source)
	{
		while (source != null)
		{
			if (source is Button { Tag: "GroupToggle" })
			{
				return true;
			}
			source = GetParentObject(source);
		}
		return false;
	}

	private static DependencyObject GetParentObject(DependencyObject source)
	{
		if (source == null)
		{
			return null;
		}
		if (source is Visual || source is Visual3D)
		{
			return VisualTreeHelper.GetParent(source);
		}
		if (source is FrameworkContentElement frameworkContentElement)
		{
			return frameworkContentElement.Parent;
		}
		return LogicalTreeHelper.GetParent(source);
	}

	private void OnPreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (DataContext is not EditorDockpaneViewModel viewModel)
		{
			return;
		}
		if (e.Key == Key.Enter && viewModel.ActivateSelectedTemplateCommand.CanExecute(null))
		{
			viewModel.ActivateSelectedTemplateCommand.Execute(null);
			e.Handled = true;
			return;
		}
		if (e.Key == Key.Escape && viewModel.DeactivateTemplateCommand.CanExecute(null))
		{
			viewModel.DeactivateTemplateCommand.Execute(null);
			e.Handled = true;
		}
	}

	private static DataTemplate CreateTextCellTemplate(string bindingPath)
	{
		FrameworkElementFactory textBlock = new FrameworkElementFactory(typeof(SearchHighlightTextBlock));
		textBlock.SetBinding(SearchHighlightTextBlock.HighlightTextProperty, new Binding(bindingPath));
		textBlock.SetBinding(SearchHighlightTextBlock.SearchTextProperty, new Binding("DataContext.SearchText")
		{
			RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(EditorDockpaneView), 1)
		});
		textBlock.SetBinding(FrameworkElement.ToolTipProperty, new Binding(bindingPath));
		textBlock.SetValue(TextBlock.TextWrappingProperty, TextWrapping.NoWrap);
		textBlock.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.None);
		textBlock.SetValue(FrameworkElement.MarginProperty, new Thickness(0.0, 0.0, 12.0, 0.0));
		return new DataTemplate
		{
			VisualTree = textBlock
		};
	}

	private DataTemplate CreateNameCellTemplate()
	{
		FrameworkElementFactory panel = new FrameworkElementFactory(typeof(DockPanel));
		panel.SetValue(FrameworkElement.MarginProperty, new Thickness(0.0, 0.0, 12.0, 0.0));
		panel.SetValue(DockPanel.LastChildFillProperty, true);

		FrameworkElementFactory toggleButton = new FrameworkElementFactory(typeof(Button));
		toggleButton.SetValue(FrameworkElement.WidthProperty, 18.0);
		toggleButton.SetValue(FrameworkElement.HeightProperty, 18.0);
		toggleButton.SetValue(FrameworkElement.MarginProperty, new Thickness(0.0, 0.0, 4.0, 0.0));
		toggleButton.SetValue(Control.PaddingProperty, new Thickness(0.0));
		toggleButton.SetValue(Control.BackgroundProperty, Brushes.Transparent);
		toggleButton.SetValue(Control.BorderBrushProperty, Brushes.Transparent);
		toggleButton.SetValue(Control.BorderThicknessProperty, new Thickness(0.0));
		toggleButton.SetValue(Button.StyleProperty, CreateGroupToggleButtonStyle());
		toggleButton.SetBinding(Control.ForegroundProperty, new Binding("Foreground")
		{
			RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ListViewItem), 1),
			FallbackValue = SystemColors.ControlTextBrush
		});
		toggleButton.SetValue(FrameworkElement.FocusVisualStyleProperty, null);
		toggleButton.SetValue(FrameworkElement.TagProperty, "GroupToggle");
		toggleButton.SetValue(FrameworkElement.ToolTipProperty, "Expand group");
		toggleButton.SetBinding(ContentControl.ContentProperty, new Binding("IsExpanded")
		{
			Converter = new GroupExpansionGlyphConverter()
		});
		toggleButton.SetBinding(Button.CommandProperty, new Binding("DataContext.ToggleGroupExpansionCommand")
		{
			RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(EditorDockpaneView), 1)
		});
		toggleButton.SetBinding(Button.CommandParameterProperty, new Binding("."));
		toggleButton.SetBinding(UIElement.VisibilityProperty, new Binding("HasChildTemplates")
		{
			Converter = new BooleanToVisibilityConverter()
		});
		toggleButton.SetValue(DockPanel.DockProperty, Dock.Left);
		panel.AppendChild(toggleButton);

		FrameworkElementFactory simpleName = CreateHighlightedTextFactory("DisplayName");
		simpleName.SetValue(TextBlock.TextWrappingProperty, TextWrapping.NoWrap);
		simpleName.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.None);
		simpleName.SetBinding(FrameworkElement.MarginProperty, new Binding("IsGroupChild")
		{
			Converter = new ChildRowIndentConverter()
		});
		panel.AppendChild(simpleName);
		return new DataTemplate { VisualTree = panel };
	}

	private static Style CreateGroupToggleButtonStyle()
	{
		Style style = new Style(typeof(Button));
		style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
		style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
		style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0.0)));
		style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0.0)));
		style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));

		ControlTemplate template = new ControlTemplate(typeof(Button));
		FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
		presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
		template.VisualTree = presenter;
		style.Setters.Add(new Setter(Control.TemplateProperty, template));

		Trigger mouseOverTrigger = new Trigger
		{
			Property = UIElement.IsMouseOverProperty,
			Value = true
		};
		mouseOverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(255, 232, 232))));
		style.Triggers.Add(mouseOverTrigger);

		return style;
	}

	private static FrameworkElementFactory CreateHighlightedTextFactory(string bindingPath)
	{
		FrameworkElementFactory textBlock = new FrameworkElementFactory(typeof(SearchHighlightTextBlock));
		textBlock.SetBinding(SearchHighlightTextBlock.HighlightTextProperty, new Binding(bindingPath));
		textBlock.SetBinding(SearchHighlightTextBlock.SearchTextProperty, new Binding("DataContext.SearchText")
		{
			RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(EditorDockpaneView), 1)
		});
		textBlock.SetBinding(FrameworkElement.ToolTipProperty, new Binding(bindingPath));
		return textBlock;
	}

	private void QueueAutoSizeColumns()
	{
		if (listViewUnits == null)
		{
			return;
		}
		listViewUnits.Dispatcher.BeginInvoke(new Action(AutoSizeColumns), DispatcherPriority.Background);
	}

	private void AutoSizeColumns()
	{
		if (listViewUnits?.Items == null || _nameColumn == null || _typeColumn == null || _descriptionColumn == null)
		{
			return;
		}
		DisplayTemplate[] templates = listViewUnits.Items.OfType<DisplayTemplate>().ToArray();
		double nameWidth = MeasureColumnWidth("Name", templates.SelectMany(GetNameColumnText), 120.0) + 36.0;
		double typeWidth = MeasureColumnWidth("Type", templates.Select((DisplayTemplate template) => template.TemplateType), 90.0);
		double descriptionWidth = MeasureColumnWidth("Description", templates.Select((DisplayTemplate template) => template.Description), 140.0);
		double availableWidth = Math.Max(0.0, listViewUnits.ActualWidth - SystemParameters.VerticalScrollBarWidth - 8.0);
		if (availableWidth > 0.0)
		{
			nameWidth = Math.Min(nameWidth, Math.Max(140.0, availableWidth - typeWidth - 140.0));
		}
		double measuredTotal = nameWidth + typeWidth + descriptionWidth;
		if (availableWidth > measuredTotal)
		{
			descriptionWidth += availableWidth - measuredTotal;
		}
		_nameColumn.Width = nameWidth;
		_typeColumn.Width = typeWidth;
		_descriptionColumn.Width = descriptionWidth;
	}

	private double MeasureColumnWidth(string header, IEnumerable<string> values, double minimumWidth)
	{
		double maxWidth = MeasureText(header);
		foreach (string value in values)
		{
			maxWidth = Math.Max(maxWidth, MeasureText(value));
		}
		return Math.Ceiling(Math.Max(minimumWidth, maxWidth + 28.0));
	}

	private static IEnumerable<string> GetNameColumnText(DisplayTemplate template)
	{
		yield return template.DisplayName;
	}

	private double MeasureText(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return 0.0;
		}
		DpiScale dpi = VisualTreeHelper.GetDpi(this);
		FormattedText formattedText = new FormattedText(
			text,
			CultureInfo.CurrentCulture,
			FlowDirection.LeftToRight,
			new Typeface(listViewUnits.FontFamily, listViewUnits.FontStyle, listViewUnits.FontWeight, listViewUnits.FontStretch),
			listViewUnits.FontSize,
			listViewUnits.Foreground,
			dpi.PixelsPerDip);
		return formattedText.WidthIncludingTrailingWhitespace;
	}

	private static Button CreateSortableHeader(string text, string sortField)
	{
		Button button = new Button
		{
			Content = text,
			CommandParameter = sortField,
			HorizontalContentAlignment = HorizontalAlignment.Left,
			Padding = new Thickness(0.0),
			Margin = new Thickness(0.0),
			Background = Brushes.Transparent,
			BorderBrush = Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			Focusable = false,
			Style = CreateHeaderButtonStyle()
		};
		button.SetBinding(Button.CommandProperty, new Binding("SortCommand"));
		button.SetBinding(Control.ForegroundProperty, new Binding("Foreground")
		{
			RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(GridViewColumnHeader), 1),
			FallbackValue = SystemColors.ControlTextBrush
		});
		return button;
	}

	private static Style CreateHeaderButtonStyle()
	{
		Style style = new Style(typeof(Button));
		style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
		style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
		style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0.0)));
		style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0.0)));
		style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));

		ControlTemplate template = new ControlTemplate(typeof(Button));
		FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
		presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
		presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
		template.VisualTree = presenter;
		style.Setters.Add(new Setter(Control.TemplateProperty, template));

		return style;
	}

	private static Style CreateClearSearchButtonStyle()
	{
		Style style = new Style(typeof(Button));
		style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
		style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
		style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0.0)));
		style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0.0)));
		style.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.Red));
		style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
		style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));

		ControlTemplate template = new ControlTemplate(typeof(Button));
		FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
		presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
		template.VisualTree = presenter;
		style.Setters.Add(new Setter(Control.TemplateProperty, template));

		style.Triggers.Add(new DataTrigger
		{
			Binding = new Binding("SearchText"),
			Value = string.Empty,
			Setters =
			{
				new Setter(UIElement.VisibilityProperty, Visibility.Collapsed)
			}
		});
		Trigger mouseOverTrigger = new Trigger
		{
			Property = UIElement.IsMouseOverProperty,
			Value = true
		};
		mouseOverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(255, 232, 232))));
		style.Triggers.Add(mouseOverTrigger);

		return style;
	}

	private static StackPanel CreateHorizontalPanel()
	{
		return new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(0.0)
		};
	}

	private static RadioButton CreateRadioButton(string text, string bindingPath)
	{
		RadioButton radioButton = new RadioButton
		{
			Content = text,
			Margin = new Thickness(0.0, 0.0, 12.0, 0.0)
		};
		radioButton.SetBinding(ToggleButton.IsCheckedProperty, new Binding(bindingPath)
		{
			Mode = BindingMode.TwoWay
		});
		return radioButton;
	}
}

internal sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return value is bool boolValue && boolValue ? Visibility.Collapsed : Visibility.Visible;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return value is Visibility visibility && visibility != Visibility.Visible;
	}
}

internal sealed class ChildRowIndentConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return value is bool isGroupChild && isGroupChild ? new Thickness(28.0, 0.0, 0.0, 0.0) : new Thickness(0.0);
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Binding.DoNothing;
	}
}

internal sealed class GroupExpansionGlyphConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return value is bool isExpanded && isExpanded ? "v" : ">";
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Binding.DoNothing;
	}
}
