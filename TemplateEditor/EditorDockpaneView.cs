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
using ArcGIS.Desktop.Framework;

namespace TemplateEditor;

public class EditorDockpaneView : UserControl
{
	internal ListView listViewUnits;

	private GridViewColumn _nameColumn;

	private GridViewColumn _typeColumn;

	private GridViewColumn _descriptionColumn;

	private DisplayTemplate _contextMenuTarget;

	private MenuItem _continuousPlacementMenuItem;

	private MenuItem _stopContinuousPlacementMenuItem;

	private MenuItem _mirrorPlacementMenuItem;

	private MenuItem _normalMirrorMenuItem;

	private MenuItem _horizontalMirrorMenuItem;

	private MenuItem _verticalMirrorMenuItem;

	private MenuItem _bothMirrorMenuItem;

	private MenuItem _favouriteMenuItem;

	private MenuItem _placeWithOverridesMenuItem;

	private ContextMenu _itemContextMenu;

	private readonly Dictionary<string, (double HorizontalOffset, double VerticalOffset)> _scrollOffsetsByView = new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase);

	private string _currentTemplateViewKey = "Groups";

	private bool _lastCompactLayout;

	private bool IsCompactLayout => AddinConfiguration.Settings?.UseCompactDockpaneLayout == true;

	public EditorDockpaneView()
	{
		try
		{
			LogService.Write("EditorDockpaneView constructor starting.");
			InitializeComponent();
			PreviewKeyDown += OnPreviewKeyDown;
			Loaded += OnLoaded;
			Unloaded += OnUnloaded;
			LogService.Write("EditorDockpaneView constructor completed.");
		}
		catch (Exception ex)
		{
			LogService.LogException("EditorDockpaneView constructor failed.", ex);
			Content = BuildFailureContent(ex);
		}
	}

	public void InitializeComponent()
	{
		try
		{
			LogService.Write("EditorDockpaneView.InitializeComponent starting.");
			bool compact = IsCompactLayout;
			_lastCompactLayout = compact;
			Grid root = new Grid
			{
				Margin = compact ? new Thickness(4.0, 3.0, 4.0, 3.0) : new Thickness(10.0, 8.0, 10.0, 8.0)
			};
			root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) });
			root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

			UIElement filterPanel = CreateFilterPanel(compact);
			Grid.SetRow(filterPanel, 0);
			root.Children.Add(filterPanel);

			ListView templateList = CreateTemplateList(compact);
			Grid.SetRow(templateList, 1);
			root.Children.Add(templateList);

			UIElement statusFooter = CreateStatusFooter(compact);
			Grid.SetRow(statusFooter, 2);
			root.Children.Add(statusFooter);
			Content = root;
			LogService.Write("EditorDockpaneView.InitializeComponent completed.");
		}
		catch (Exception ex)
		{
			LogService.LogException("EditorDockpaneView.InitializeComponent failed.", ex);
			Content = BuildFailureContent(ex);
		}
	}

	private static UIElement BuildFailureContent(Exception ex)
	{
		return new Border
		{
			Margin = new Thickness(10.0),
			Padding = new Thickness(12.0),
			BorderBrush = Brushes.OrangeRed,
			BorderThickness = new Thickness(1.0),
			Background = FrameworkApplication.ApplicationTheme == ApplicationTheme.Dark
				? new SolidColorBrush(Color.FromRgb(48, 30, 30))
				: new SolidColorBrush(Color.FromRgb(255, 244, 244)),
			Child = new TextBlock
			{
				Text = "Template Editor could not build its dockpane view.\n\n" + ex.Message,
				TextWrapping = TextWrapping.Wrap,
				Foreground = FrameworkApplication.ApplicationTheme == ApplicationTheme.Dark ? Brushes.White : Brushes.Black
			}
		};
	}

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		AddinConfiguration.SettingsChanged -= OnSettingsChanged;
		AddinConfiguration.SettingsChanged += OnSettingsChanged;
	}

	private void OnUnloaded(object sender, RoutedEventArgs e)
	{
		AddinConfiguration.SettingsChanged -= OnSettingsChanged;
	}

	private void OnSettingsChanged()
	{
		bool compact = IsCompactLayout;
		if (compact != _lastCompactLayout)
		{
			Dispatcher.BeginInvoke(new Action(InitializeComponent), DispatcherPriority.ContextIdle);
			return;
		}
		EditorDockpaneViewModel.RefreshSettingsStatus();
	}

	private UIElement CreateFilterPanel(bool compact)
	{
		Grid panel = new Grid
		{
			Margin = compact ? new Thickness(0.0, 0.0, 0.0, 4.0) : new Thickness(0.0, 0.0, 0.0, 8.0)
		};
		panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
		panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

		Grid searchContainer = new Grid
		{
			Margin = new Thickness(compact ? 8.0 : 12.0, 0.0, 0.0, 0.0),
			MinWidth = compact ? 200.0 : 240.0
		};
		TextBox searchBox = new TextBox
		{
			Padding = compact ? new Thickness(24.0, 3.0, 20.0, 3.0) : new Thickness(28.0, 5.0, 24.0, 5.0),
			BorderBrush = GetSubtleBorderBrush(),
			Background = GetPanelBackgroundBrush(),
			Foreground = GetPrimaryForegroundBrush()
		};
		searchBox.SetBinding(TextBox.TextProperty, new Binding("SearchText")
		{
			UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
		});
		searchContainer.Children.Add(searchBox);

		TextBlock searchIcon = new TextBlock
		{
			Text = "⌕",
			Foreground = GetMutedForegroundBrush(),
			Margin = new Thickness(compact ? 7.0 : 9.0, 0.0, 0.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center,
			IsHitTestVisible = false
		};
		searchContainer.Children.Add(searchIcon);

		TextBlock placeholder = new TextBlock
		{
			Text = "Search templates...",
			Foreground = GetMutedForegroundBrush(),
			Margin = new Thickness(compact ? 24.0 : 28.0, 0.0, 0.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center,
			IsHitTestVisible = false
		};
		placeholder.SetBinding(UIElement.VisibilityProperty, new Binding("HasSearchText")
		{
			Converter = new InverseBooleanToVisibilityConverter()
		});
		void UpdatePlaceholderVisibility()
		{
			placeholder.Visibility = string.IsNullOrWhiteSpace(searchBox.Text) && !searchBox.IsKeyboardFocusWithin
				? Visibility.Visible
				: Visibility.Collapsed;
		}
		searchBox.TextChanged += delegate { UpdatePlaceholderVisibility(); };
		searchBox.GotKeyboardFocus += delegate { UpdatePlaceholderVisibility(); };
		searchBox.LostKeyboardFocus += delegate { UpdatePlaceholderVisibility(); };
		searchBox.Loaded += delegate { UpdatePlaceholderVisibility(); };
		searchContainer.Children.Add(placeholder);

		Button clearSearchButton = new Button
		{
			Content = "×",
			Width = 18.0,
			Height = 18.0,
			Margin = new Thickness(0.0, 0.0, 4.0, 0.0),
			Padding = new Thickness(0.0),
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center,
			Foreground = GetMutedForegroundBrush(),
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

		WrapPanel templateTypePanel = new WrapPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(0.0)
		};
		templateTypePanel.Children.Add(CreateRadioButton("Groups", "ShowGroupTemplates", compact));
		templateTypePanel.Children.Add(CreateRadioButton("Simple", "ShowSimpleTemplates", compact));
		templateTypePanel.Children.Add(CreateRadioButton("All", "ShowAllTemplates", compact));
		templateTypePanel.Children.Add(CreateRadioButton("Favourites", "ShowFavouriteTemplates", compact));
		templateTypePanel.Children.Add(CreateRadioButton("Recent", "ShowRecentTemplates", compact));
		templateTypePanel.VerticalAlignment = VerticalAlignment.Center;

		DockPanel filterRow = new DockPanel
		{
			LastChildFill = true
		};
		DockPanel.SetDock(templateTypePanel, Dock.Left);
		filterRow.Children.Add(templateTypePanel);
		filterRow.Children.Add(searchContainer);
		panel.Children.Add(filterRow);

		return panel;
	}

	private static UIElement CreateStatusFooter(bool compact)
	{
		Grid footer = new Grid
		{
			Margin = compact ? new Thickness(0.0, 4.0, 0.0, 0.0) : new Thickness(0.0, 8.0, 0.0, 0.0)
		};
		footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
		footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

		Border statusChip = new Border
		{
			Background = GetPanelBackgroundBrush(),
			BorderBrush = GetSubtleBorderBrush(),
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(4.0),
			Padding = compact ? new Thickness(6.0, 2.0, 6.0, 2.0) : new Thickness(8.0, 3.0, 8.0, 3.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		TextBlock selected = new TextBlock
		{
			Foreground = GetPrimaryForegroundBrush(),
			TextTrimming = TextTrimming.CharacterEllipsis,
			VerticalAlignment = VerticalAlignment.Center
		};
		selected.SetBinding(TextBlock.TextProperty, new Binding("PlacementStatus"));
		statusChip.Child = selected;
		footer.Children.Add(statusChip);

		Border countChip = new Border
		{
			Background = GetPanelBackgroundBrush(),
			BorderBrush = GetSubtleBorderBrush(),
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(4.0),
			Padding = compact ? new Thickness(6.0, 2.0, 6.0, 2.0) : new Thickness(8.0, 3.0, 8.0, 3.0),
			Margin = new Thickness(compact ? 8.0 : 12.0, 0.0, 0.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		TextBlock count = new TextBlock
		{
			Foreground = GetMutedForegroundBrush(),
			VerticalAlignment = VerticalAlignment.Center
		};
		count.SetBinding(TextBlock.TextProperty, new Binding("TemplateCount"));
		countChip.Child = count;
		Grid.SetColumn(countChip, 1);
		footer.Children.Add(countChip);

		Border optionChip = new Border
		{
			Background = GetPanelBackgroundBrush(),
			BorderBrush = GetSubtleBorderBrush(),
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(4.0),
			Padding = compact ? new Thickness(6.0, 2.0, 6.0, 2.0) : new Thickness(8.0, 3.0, 8.0, 3.0),
			Margin = new Thickness(compact ? 8.0 : 12.0, 0.0, 0.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		TextBlock options = new TextBlock
		{
			Foreground = GetMutedForegroundBrush(),
			VerticalAlignment = VerticalAlignment.Center
		};
		options.SetBinding(TextBlock.TextProperty, new Binding("PlacementOptionsStatus"));
		optionChip.Child = options;
		Grid.SetColumn(optionChip, 2);
		footer.Children.Add(optionChip);
		return footer;
	}

	private ListView CreateTemplateList(bool compact)
	{
		listViewUnits = new ListView
		{
			MinHeight = compact ? 60.0 : 80.0,
			VerticalAlignment = VerticalAlignment.Stretch,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			Background = GetTableBackgroundBrush(),
			BorderBrush = GetSubtleBorderBrush(),
			Foreground = GetPrimaryForegroundBrush(),
			ItemContainerStyle = CreateTemplateListItemStyle(compact)
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
		listViewUnits.PreviewMouseRightButtonDown += OnTemplateListPreviewMouseRightButtonDown;
		_continuousPlacementMenuItem = new MenuItem
		{
			Header = "Place Continuously"
		};
		_continuousPlacementMenuItem.Click += OnContinuousPlacementMenuItemClick;
		_stopContinuousPlacementMenuItem = new MenuItem
		{
			Header = "Stop Continuous Placement"
		};
		_stopContinuousPlacementMenuItem.Click += OnStopContinuousPlacementMenuItemClick;
		_mirrorPlacementMenuItem = new MenuItem
		{
			Header = "Mirror Placement"
		};
		_normalMirrorMenuItem = CreateMirrorMenuItem("Normal", PlacementMirrorMode.None);
		_horizontalMirrorMenuItem = CreateMirrorMenuItem("Mirror Horizontal", PlacementMirrorMode.Horizontal);
		_verticalMirrorMenuItem = CreateMirrorMenuItem("Mirror Vertical", PlacementMirrorMode.Vertical);
		_bothMirrorMenuItem = CreateMirrorMenuItem("Mirror Both", PlacementMirrorMode.Both);
		_mirrorPlacementMenuItem.Items.Add(_normalMirrorMenuItem);
		_mirrorPlacementMenuItem.Items.Add(new Separator());
		_mirrorPlacementMenuItem.Items.Add(_horizontalMirrorMenuItem);
		_mirrorPlacementMenuItem.Items.Add(_verticalMirrorMenuItem);
		_mirrorPlacementMenuItem.Items.Add(_bothMirrorMenuItem);
		_favouriteMenuItem = new MenuItem();
		_favouriteMenuItem.Click += OnFavouriteMenuItemClick;
		_placeWithOverridesMenuItem = new MenuItem
		{
			Header = "Place With Overrides..."
		};
		_placeWithOverridesMenuItem.Click += OnPlaceWithOverridesMenuItemClick;
		_itemContextMenu = new ContextMenu();
		_itemContextMenu.Items.Add(_continuousPlacementMenuItem);
		_itemContextMenu.Items.Add(_stopContinuousPlacementMenuItem);
		_itemContextMenu.Items.Add(_mirrorPlacementMenuItem);
		_itemContextMenu.Items.Add(_placeWithOverridesMenuItem);
		_itemContextMenu.Items.Add(new Separator());
		_itemContextMenu.Items.Add(_favouriteMenuItem);
		listViewUnits.ContextMenu = _itemContextMenu;
		listViewUnits.ContextMenuOpening += OnContextMenuOpening;
		_nameColumn = new GridViewColumn { Header = CreateSortableHeader("Name", "Name"), CellTemplate = CreateNameCellTemplate(), Width = 220.0 };
		_typeColumn = new GridViewColumn { Header = CreateSortableHeader("Template Type", "TemplateType"), CellTemplate = CreateTextCellTemplate("TemplateType"), Width = 160.0 };
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

	private void OnTemplateListPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
	{
		ListViewItem item = ItemsControl.ContainerFromElement(listViewUnits, e.OriginalSource as DependencyObject) as ListViewItem;
		if (item?.DataContext is not DisplayTemplate template)
		{
			_contextMenuTarget = null;
			return;
		}
		_contextMenuTarget = template;
		UpdateFavouriteMenuItem(template);
		UpdateContinuousPlacementMenuItem();
		UpdateMirrorPlacementMenuItems();
		UpdatePlaceWithOverridesMenuItem();
		_itemContextMenu.PlacementTarget = item;
		_itemContextMenu.IsOpen = true;
		e.Handled = true;
	}

	private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
	{
		if (DataContext is not EditorDockpaneViewModel)
		{
			e.Handled = true;
			return;
		}
		DependencyObject source = e.OriginalSource as DependencyObject;
		while (source != null && source is not ListViewItem)
		{
			source = GetParentObject(source);
		}
		if (source is ListViewItem { DataContext: DisplayTemplate template })
		{
			_contextMenuTarget = template;
		}
		if (_contextMenuTarget == null)
		{
			e.Handled = true;
			return;
		}
		UpdateFavouriteMenuItem(_contextMenuTarget);
		UpdateContinuousPlacementMenuItem();
		UpdateMirrorPlacementMenuItems();
		UpdatePlaceWithOverridesMenuItem();
	}

	private MenuItem CreateMirrorMenuItem(string header, PlacementMirrorMode mirrorMode)
	{
		MenuItem item = new MenuItem
		{
			Header = header,
			Tag = mirrorMode
		};
		item.Click += OnMirrorPlacementMenuItemClick;
		return item;
	}

	private void UpdateContinuousPlacementMenuItem()
	{
		bool isEnabled = AddinConfiguration.Settings?.EnableContinuousPlacementMode == true;
		_continuousPlacementMenuItem.Header = isEnabled ? "Place Continuously (On)" : "Place Continuously";
		_continuousPlacementMenuItem.IsChecked = isEnabled;
		_stopContinuousPlacementMenuItem.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
	}

	private void UpdateMirrorPlacementMenuItems()
	{
		PlacementMirrorMode mirrorMode = AddinConfiguration.PlacementMirrorMode;
		_normalMirrorMenuItem.IsChecked = mirrorMode == PlacementMirrorMode.None;
		_horizontalMirrorMenuItem.IsChecked = mirrorMode == PlacementMirrorMode.Horizontal;
		_verticalMirrorMenuItem.IsChecked = mirrorMode == PlacementMirrorMode.Vertical;
		_bothMirrorMenuItem.IsChecked = mirrorMode == PlacementMirrorMode.Both;
	}

	private void UpdatePlaceWithOverridesMenuItem()
	{
		bool hasConfiguredOverrides = PlacementAttributeOverrideService.Definitions.Count > 0;
		_placeWithOverridesMenuItem.IsEnabled = hasConfiguredOverrides;
		_placeWithOverridesMenuItem.ToolTip = hasConfiguredOverrides
			? "Choose one-time attribute overrides for the next placement of this template."
			: "No packaged placement override fields are currently available.";
	}

	private void UpdateFavouriteMenuItem(DisplayTemplate template)
	{
		bool isFavourite = AddinConfiguration.Settings?.FavouriteTemplateKeys?
			.Exists(k => string.Equals(k, template.UniqueKey, StringComparison.OrdinalIgnoreCase)) ?? false;
		_favouriteMenuItem.Header = isFavourite ? "Remove from Favourites" : "Add to Favourites";
	}

	private void OnFavouriteMenuItemClick(object sender, RoutedEventArgs e)
	{
		if (DataContext is EditorDockpaneViewModel viewModel && _contextMenuTarget != null)
		{
			viewModel.ToggleFavouriteCommand.Execute(_contextMenuTarget);
		}
	}

	private void OnContinuousPlacementMenuItemClick(object sender, RoutedEventArgs e)
	{
		if (DataContext is EditorDockpaneViewModel viewModel && _contextMenuTarget != null)
		{
			viewModel.ActivateContinuousPlacementCommand.Execute(_contextMenuTarget);
		}
	}

	private void OnStopContinuousPlacementMenuItemClick(object sender, RoutedEventArgs e)
	{
		if (DataContext is EditorDockpaneViewModel viewModel)
		{
			viewModel.StopContinuousPlacementCommand.Execute(null);
		}
	}

	private void OnMirrorPlacementMenuItemClick(object sender, RoutedEventArgs e)
	{
		if (DataContext is EditorDockpaneViewModel viewModel && _contextMenuTarget != null && sender is MenuItem { Tag: PlacementMirrorMode mirrorMode })
		{
			viewModel.ActivateMirrorPlacementCommand.Execute(Tuple.Create(_contextMenuTarget, mirrorMode));
		}
	}

	private void OnPlaceWithOverridesMenuItemClick(object sender, RoutedEventArgs e)
	{
		if (DataContext is EditorDockpaneViewModel viewModel && _contextMenuTarget != null)
		{
			viewModel.PlaceWithOverridesCommand.Execute(_contextMenuTarget);
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

	private void OnTemplateViewChecked(object sender, RoutedEventArgs e)
	{
		if (sender is not RadioButton { Tag: string nextViewKey } || string.Equals(nextViewKey, _currentTemplateViewKey, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		SaveCurrentTemplateScrollOffset();
		_currentTemplateViewKey = nextViewKey;
		Dispatcher.BeginInvoke(new Action(RestoreCurrentTemplateScrollOffset), DispatcherPriority.ContextIdle);
	}

	private void SaveCurrentTemplateScrollOffset()
	{
		ScrollViewer scrollViewer = FindVisualChild<ScrollViewer>(listViewUnits);
		if (scrollViewer == null || string.IsNullOrWhiteSpace(_currentTemplateViewKey))
		{
			return;
		}
		_scrollOffsetsByView[_currentTemplateViewKey] = (scrollViewer.HorizontalOffset, scrollViewer.VerticalOffset);
	}

	private void RestoreCurrentTemplateScrollOffset()
	{
		ScrollViewer scrollViewer = FindVisualChild<ScrollViewer>(listViewUnits);
		if (scrollViewer == null)
		{
			return;
		}
		if (!_scrollOffsetsByView.TryGetValue(_currentTemplateViewKey, out (double HorizontalOffset, double VerticalOffset) offset))
		{
			offset = (0.0, 0.0);
		}
		scrollViewer.ScrollToHorizontalOffset(offset.HorizontalOffset);
		scrollViewer.ScrollToVerticalOffset(offset.VerticalOffset);
	}

	private static TChild FindVisualChild<TChild>(DependencyObject parent) where TChild : DependencyObject
	{
		if (parent == null)
		{
			return null;
		}
		int childCount = VisualTreeHelper.GetChildrenCount(parent);
		for (int i = 0; i < childCount; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, i);
			if (child is TChild typedChild)
			{
				return typedChild;
			}
			TChild descendant = FindVisualChild<TChild>(child);
			if (descendant != null)
			{
				return descendant;
			}
		}
		return null;
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
		simpleName.SetBinding(FrameworkElement.MarginProperty, new Binding("IsIndentedChild")
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
		double nameWidth = MeasureColumnWidth("Name", templates.SelectMany(GetNameColumnText), 0.0) + 36.0;
		double typeWidth = MeasureColumnWidth("Template Type", templates.Select((DisplayTemplate template) => template.TemplateType), 0.0);
		double descriptionWidth = MeasureColumnWidth("Description", templates.Select((DisplayTemplate template) => template.Description), 0.0);
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
			Foreground = GetTableHeaderForeground(),
			Focusable = false,
			Style = CreateHeaderButtonStyle()
		};
		button.SetBinding(Button.CommandProperty, new Binding("SortCommand"));
		return button;
	}

	private static Brush GetTableHeaderForeground()
	{
		return FrameworkApplication.ApplicationTheme == ApplicationTheme.Dark
			? Brushes.White
			: SystemColors.ControlTextBrush;
	}

	private static Brush GetPrimaryForegroundBrush()
	{
		return FrameworkApplication.ApplicationTheme == ApplicationTheme.Dark
			? new SolidColorBrush(Color.FromRgb(238, 238, 238))
			: new SolidColorBrush(Color.FromRgb(32, 32, 32));
	}

	private static Brush GetMutedForegroundBrush()
	{
		return FrameworkApplication.ApplicationTheme == ApplicationTheme.Dark
			? new SolidColorBrush(Color.FromRgb(174, 174, 174))
			: new SolidColorBrush(Color.FromRgb(96, 96, 96));
	}

	private static Brush GetPanelBackgroundBrush()
	{
		return FrameworkApplication.ApplicationTheme == ApplicationTheme.Dark
			? new SolidColorBrush(Color.FromRgb(45, 45, 48))
			: new SolidColorBrush(Color.FromRgb(247, 247, 247));
	}

	private static Brush GetTableBackgroundBrush()
	{
		return FrameworkApplication.ApplicationTheme == ApplicationTheme.Dark
			? new SolidColorBrush(Color.FromRgb(28, 28, 28))
			: Brushes.White;
	}

	private static Brush GetSubtleBorderBrush()
	{
		return FrameworkApplication.ApplicationTheme == ApplicationTheme.Dark
			? new SolidColorBrush(Color.FromRgb(86, 86, 86))
			: new SolidColorBrush(Color.FromRgb(196, 196, 196));
	}

	private static Style CreateTemplateListItemStyle(bool compact)
	{
		Style style = new Style(typeof(ListViewItem));
		style.Setters.Add(new Setter(Control.ForegroundProperty, GetPrimaryForegroundBrush()));
		style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
		style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
		style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0.0)));
		style.Setters.Add(new Setter(Control.PaddingProperty, compact ? new Thickness(3.0, 0.0, 3.0, 0.0) : new Thickness(4.0, 2.0, 4.0, 2.0)));
		style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));

		ControlTemplate template = new ControlTemplate(typeof(ListViewItem));
		FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
		border.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
		border.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
		border.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = RelativeSource.TemplatedParent });
		FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(GridViewRowPresenter));
		presenter.SetBinding(GridViewRowPresenter.ContentProperty, new Binding());
		presenter.SetBinding(GridViewRowPresenter.ColumnsProperty, new Binding("View.Columns")
		{
			RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ListView), 1)
		});
		presenter.SetBinding(FrameworkElement.MarginProperty, new Binding("Padding") { RelativeSource = RelativeSource.TemplatedParent });
		presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		border.AppendChild(presenter);
		template.VisualTree = border;
		style.Setters.Add(new Setter(Control.TemplateProperty, template));

		Trigger hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
		hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, FrameworkApplication.ApplicationTheme == ApplicationTheme.Dark
			? new SolidColorBrush(Color.FromRgb(40, 48, 56))
			: new SolidColorBrush(Color.FromRgb(232, 242, 252))));
		style.Triggers.Add(hoverTrigger);

		Trigger selectedTrigger = new Trigger { Property = ListViewItem.IsSelectedProperty, Value = true };
		selectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, FrameworkApplication.ApplicationTheme == ApplicationTheme.Dark
			? new SolidColorBrush(Color.FromRgb(24, 74, 116))
			: new SolidColorBrush(Color.FromRgb(210, 231, 250))));
		selectedTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(51, 153, 255))));
		selectedTrigger.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(3.0, 0.0, 0.0, 0.0)));
		selectedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, FrameworkApplication.ApplicationTheme == ApplicationTheme.Dark ? Brushes.White : Brushes.Black));
		style.Triggers.Add(selectedTrigger);
		MultiTrigger selectedHoverTrigger = new MultiTrigger
		{
			Conditions =
			{
				new Condition(ListViewItem.IsSelectedProperty, true),
				new Condition(UIElement.IsMouseOverProperty, true)
			}
		};
		selectedHoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, FrameworkApplication.ApplicationTheme == ApplicationTheme.Dark
			? new SolidColorBrush(Color.FromRgb(28, 86, 132))
			: new SolidColorBrush(Color.FromRgb(198, 224, 248))));
		style.Triggers.Add(selectedHoverTrigger);
		return style;
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

	private RadioButton CreateRadioButton(string text, string bindingPath, bool compact)
	{
		RadioButton radioButton = new RadioButton
		{
			Content = text,
			Tag = text,
			Margin = new Thickness(0.0, 0.0, compact ? 4.0 : 6.0, 0.0),
			Padding = compact ? new Thickness(8.0, 3.0, 8.0, 3.0) : new Thickness(10.0, 4.0, 10.0, 4.0),
			Foreground = GetPrimaryForegroundBrush(),
			Style = CreateSegmentedRadioButtonStyle()
		};
		radioButton.Checked += OnTemplateViewChecked;
		radioButton.SetBinding(ToggleButton.IsCheckedProperty, new Binding(bindingPath)
		{
			Mode = BindingMode.TwoWay
		});
		return radioButton;
	}

	private static Style CreateSegmentedRadioButtonStyle()
	{
		Style style = new Style(typeof(RadioButton));
		style.Setters.Add(new Setter(Control.BackgroundProperty, GetPanelBackgroundBrush()));
		style.Setters.Add(new Setter(Control.BorderBrushProperty, GetSubtleBorderBrush()));
		style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1.0)));
		style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));

		ControlTemplate template = new ControlTemplate(typeof(RadioButton));
		FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
		border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4.0));
		border.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
		border.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
		border.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = RelativeSource.TemplatedParent });
		FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
		presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
		presenter.SetBinding(ContentPresenter.MarginProperty, new Binding("Padding") { RelativeSource = RelativeSource.TemplatedParent });
		border.AppendChild(presenter);
		template.VisualTree = border;
		style.Setters.Add(new Setter(Control.TemplateProperty, template));

		Trigger checkedTrigger = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
		checkedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, FrameworkApplication.ApplicationTheme == ApplicationTheme.Dark
			? new SolidColorBrush(Color.FromRgb(35, 82, 130))
			: new SolidColorBrush(Color.FromRgb(214, 234, 252))));
		checkedTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(51, 153, 255))));
		style.Triggers.Add(checkedTrigger);
		return style;
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
		return value is bool isExpanded && isExpanded ? "⌄" : "›";
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Binding.DoNothing;
	}
}
