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

	private MenuItem _placeAtOffsetMenuItem;

	private ContextMenu _itemContextMenu;

	private readonly Dictionary<string, (double HorizontalOffset, double VerticalOffset)> _scrollOffsetsByView = new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase);

	private string _currentTemplateViewKey = "Groups";

	private bool _lastCompactLayout;

	private bool IsCompactLayout => AddinConfiguration.Settings?.UseCompactDockpaneLayout ?? false;

	public EditorDockpaneView()
	{
		try
		{
			LogService.Write("EditorDockpaneView constructor starting.");
			InitializeComponent();
			base.PreviewKeyDown += OnPreviewKeyDown;
			base.Loaded += OnLoaded;
			base.Unloaded += OnUnloaded;
			LogService.Write("EditorDockpaneView constructor completed.");
		}
		catch (Exception ex)
		{
			LogService.LogException("EditorDockpaneView constructor failed.", ex);
			base.Content = BuildFailureContent(ex);
		}
	}

	public void InitializeComponent()
	{
		try
		{
			LogService.Write("EditorDockpaneView.InitializeComponent starting.");
			bool flag = (_lastCompactLayout = IsCompactLayout);
			Grid grid = new Grid
			{
				Margin = (flag ? new Thickness(4.0, 3.0, 4.0, 3.0) : new Thickness(10.0, 8.0, 10.0, 8.0))
			};
			grid.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			grid.RowDefinitions.Add(new RowDefinition
			{
				Height = new GridLength(1.0, GridUnitType.Star)
			});
			grid.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			UIElement element = CreateFilterPanel(flag);
			Grid.SetRow(element, 0);
			grid.Children.Add(element);
			ListView element2 = CreateTemplateList(flag);
			Grid.SetRow(element2, 1);
			grid.Children.Add(element2);
			UIElement element3 = CreateStatusFooter(flag);
			Grid.SetRow(element3, 2);
			grid.Children.Add(element3);
			base.Content = grid;
			LogService.Write("EditorDockpaneView.InitializeComponent completed.");
		}
		catch (Exception ex)
		{
			LogService.LogException("EditorDockpaneView.InitializeComponent failed.", ex);
			base.Content = BuildFailureContent(ex);
		}
	}

	private static UIElement BuildFailureContent(Exception ex)
	{
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Invalid comparison between Unknown and I4
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Invalid comparison between Unknown and I4
		return new Border
		{
			Margin = new Thickness(10.0),
			Padding = new Thickness(12.0),
			BorderBrush = Brushes.OrangeRed,
			BorderThickness = new Thickness(1.0),
			Background = (((int)FrameworkApplication.ApplicationTheme == 1) ? new SolidColorBrush(Color.FromRgb(48, 30, 30)) : new SolidColorBrush(Color.FromRgb(byte.MaxValue, 244, 244))),
			Child = new TextBlock
			{
				Text = "Template Editor could not build its dockpane view.\n\n" + ex.Message,
				TextWrapping = TextWrapping.Wrap,
				Foreground = (((int)FrameworkApplication.ApplicationTheme == 1) ? Brushes.White : Brushes.Black)
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
		bool isCompactLayout = IsCompactLayout;
		if (isCompactLayout != _lastCompactLayout)
		{
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)new Action(InitializeComponent), (DispatcherPriority)3, Array.Empty<object>());
		}
		else
		{
			EditorDockpaneViewModel.RefreshSettingsStatus();
		}
	}

	private UIElement CreateFilterPanel(bool compact)
	{
		Grid grid = new Grid
		{
			Margin = (compact ? new Thickness(0.0, 0.0, 0.0, 4.0) : new Thickness(0.0, 0.0, 0.0, 8.0))
		};
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		Grid grid2 = new Grid
		{
			Margin = new Thickness(compact ? 8.0 : 12.0, 0.0, 0.0, 0.0),
			MinWidth = (compact ? 200.0 : 240.0)
		};
		TextBox searchBox = new TextBox
		{
			Padding = (compact ? new Thickness(24.0, 3.0, 20.0, 3.0) : new Thickness(28.0, 5.0, 24.0, 5.0)),
			BorderBrush = GetSubtleBorderBrush(),
			Background = GetPanelBackgroundBrush(),
			Foreground = GetPrimaryForegroundBrush()
		};
		searchBox.SetBinding(TextBox.TextProperty, new Binding("SearchText")
		{
			UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
		});
		grid2.Children.Add(searchBox);
		TextBlock element = new TextBlock
		{
			Text = "⌕",
			Foreground = GetMutedForegroundBrush(),
			Margin = new Thickness(compact ? 7.0 : 9.0, 0.0, 0.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center,
			IsHitTestVisible = false
		};
		grid2.Children.Add(element);
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
		searchBox.TextChanged += delegate
		{
			UpdatePlaceholderVisibility();
		};
		searchBox.GotKeyboardFocus += delegate
		{
			UpdatePlaceholderVisibility();
		};
		searchBox.LostKeyboardFocus += delegate
		{
			UpdatePlaceholderVisibility();
		};
		searchBox.Loaded += delegate
		{
			UpdatePlaceholderVisibility();
		};
		grid2.Children.Add(placeholder);
		Button button = new Button
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
		button.SetBinding(ButtonBase.CommandProperty, new Binding("ClearSearchCommand"));
		button.Style = CreateClearSearchButtonStyle();
		grid2.Children.Add(button);
		WrapPanel wrapPanel = new WrapPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(0.0)
		};
		wrapPanel.Children.Add(CreateRadioButton("Groups", "ShowGroupTemplates", compact));
		wrapPanel.Children.Add(CreateRadioButton("Simple", "ShowSimpleTemplates", compact));
		wrapPanel.Children.Add(CreateRadioButton("All", "ShowAllTemplates", compact));
		wrapPanel.Children.Add(CreateRadioButton("Favourites", "ShowFavouriteTemplates", compact));
		wrapPanel.Children.Add(CreateRadioButton("Recent", "ShowRecentTemplates", compact));
		wrapPanel.VerticalAlignment = VerticalAlignment.Center;
		DockPanel dockPanel = new DockPanel
		{
			LastChildFill = true
		};
		DockPanel.SetDock(wrapPanel, Dock.Left);
		dockPanel.Children.Add(wrapPanel);
		dockPanel.Children.Add(grid2);
		grid.Children.Add(dockPanel);
		return grid;
		void UpdatePlaceholderVisibility()
		{
			placeholder.Visibility = ((!string.IsNullOrWhiteSpace(searchBox.Text) || searchBox.IsKeyboardFocusWithin) ? Visibility.Collapsed : Visibility.Visible);
		}
	}

	private static UIElement CreateStatusFooter(bool compact)
	{
		Grid grid = new Grid
		{
			Margin = (compact ? new Thickness(0.0, 4.0, 0.0, 0.0) : new Thickness(0.0, 8.0, 0.0, 0.0))
		};
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		Border border = new Border
		{
			Background = GetPanelBackgroundBrush(),
			BorderBrush = GetSubtleBorderBrush(),
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(4.0),
			Padding = (compact ? new Thickness(6.0, 2.0, 6.0, 2.0) : new Thickness(8.0, 3.0, 8.0, 3.0)),
			VerticalAlignment = VerticalAlignment.Center
		};
		TextBlock textBlock = new TextBlock
		{
			Foreground = GetPrimaryForegroundBrush(),
			TextTrimming = TextTrimming.CharacterEllipsis,
			VerticalAlignment = VerticalAlignment.Center
		};
		textBlock.SetBinding(TextBlock.TextProperty, new Binding("PlacementStatus"));
		border.Child = textBlock;
		grid.Children.Add(border);
		Border border2 = new Border
		{
			Background = GetPanelBackgroundBrush(),
			BorderBrush = GetSubtleBorderBrush(),
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(4.0),
			Padding = (compact ? new Thickness(6.0, 2.0, 6.0, 2.0) : new Thickness(8.0, 3.0, 8.0, 3.0)),
			Margin = new Thickness(compact ? 8.0 : 12.0, 0.0, 0.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		TextBlock textBlock2 = new TextBlock
		{
			Foreground = GetMutedForegroundBrush(),
			VerticalAlignment = VerticalAlignment.Center,
			ToolTip = "Template configuration and association-rule availability"
		};
		textBlock2.SetBinding(TextBlock.TextProperty, new Binding("ConfigurationHealthStatus"));
		border2.Child = textBlock2;
		Grid.SetColumn(border2, 1);
		grid.Children.Add(border2);
		Border border3 = new Border
		{
			Background = GetPanelBackgroundBrush(),
			BorderBrush = GetSubtleBorderBrush(),
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(4.0),
			Padding = (compact ? new Thickness(6.0, 2.0, 6.0, 2.0) : new Thickness(8.0, 3.0, 8.0, 3.0)),
			Margin = new Thickness(compact ? 8.0 : 12.0, 0.0, 0.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		TextBlock textBlock3 = new TextBlock
		{
			Foreground = GetMutedForegroundBrush(),
			VerticalAlignment = VerticalAlignment.Center
		};
		textBlock3.SetBinding(TextBlock.TextProperty, new Binding("TemplateCount"));
		border3.Child = textBlock3;
		Grid.SetColumn(border3, 2);
		grid.Children.Add(border3);
		Border border4 = new Border
		{
			Background = GetPanelBackgroundBrush(),
			BorderBrush = GetSubtleBorderBrush(),
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(4.0),
			Padding = (compact ? new Thickness(6.0, 2.0, 6.0, 2.0) : new Thickness(8.0, 3.0, 8.0, 3.0)),
			Margin = new Thickness(compact ? 8.0 : 12.0, 0.0, 0.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		TextBlock textBlock4 = new TextBlock
		{
			Foreground = GetMutedForegroundBrush(),
			VerticalAlignment = VerticalAlignment.Center
		};
		textBlock4.SetBinding(TextBlock.TextProperty, new Binding("PlacementOptionsStatus"));
		border4.Child = textBlock4;
		Grid.SetColumn(border4, 3);
		grid.Children.Add(border4);
		return grid;
	}

	private ListView CreateTemplateList(bool compact)
	{
		listViewUnits = new ListView
		{
			MinHeight = (compact ? 60.0 : 80.0),
			VerticalAlignment = VerticalAlignment.Stretch,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			Background = GetTableBackgroundBrush(),
			BorderBrush = GetSubtleBorderBrush(),
			Foreground = GetPrimaryForegroundBrush(),
			ItemContainerStyle = CreateTemplateListItemStyle(compact)
		};
		ScrollViewer.SetHorizontalScrollBarVisibility((DependencyObject)(object)listViewUnits, ScrollBarVisibility.Auto);
		ScrollViewer.SetVerticalScrollBarVisibility((DependencyObject)(object)listViewUnits, ScrollBarVisibility.Auto);
		ScrollViewer.SetCanContentScroll((DependencyObject)(object)listViewUnits, canContentScroll: true);
		VirtualizingPanel.SetIsVirtualizing((DependencyObject)(object)listViewUnits, value: true);
		VirtualizingPanel.SetVirtualizationMode((DependencyObject)(object)listViewUnits, VirtualizationMode.Recycling);
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
		_placeAtOffsetMenuItem = new MenuItem
		{
			Header = "Place at Offset...",
			ToolTip = "Click the insert point, then move around the distance ring to choose the offset direction. The original insert point remains the line split location."
		};
		_placeAtOffsetMenuItem.Click += OnPlaceAtOffsetMenuItemClick;
		_itemContextMenu = new ContextMenu();
		_itemContextMenu.Items.Add(_continuousPlacementMenuItem);
		_itemContextMenu.Items.Add(_stopContinuousPlacementMenuItem);
		_itemContextMenu.Items.Add(_mirrorPlacementMenuItem);
		_itemContextMenu.Items.Add(_placeWithOverridesMenuItem);
		_itemContextMenu.Items.Add(_placeAtOffsetMenuItem);
		_itemContextMenu.Items.Add(new Separator());
		_itemContextMenu.Items.Add(_favouriteMenuItem);
		listViewUnits.ContextMenu = _itemContextMenu;
		listViewUnits.ContextMenuOpening += OnContextMenuOpening;
		_nameColumn = new GridViewColumn
		{
			Header = CreateSortableHeader("Name", "Name"),
			CellTemplate = CreateNameCellTemplate(),
			Width = 220.0
		};
		_typeColumn = new GridViewColumn
		{
			Header = CreateSortableHeader("Template Type", "TemplateType"),
			CellTemplate = CreateTextCellTemplate("TemplateType"),
			Width = 160.0
		};
		_descriptionColumn = new GridViewColumn
		{
			Header = CreateSortableHeader("Description", "Description"),
			CellTemplate = CreateTextCellTemplate("Description"),
			Width = 360.0
		};
		listViewUnits.View = new GridView
		{
			Columns = { _nameColumn, _typeColumn, _descriptionColumn }
		};
		listViewUnits.Loaded += delegate
		{
			QueueAutoSizeColumns();
		};
		listViewUnits.SizeChanged += delegate
		{
			QueueAutoSizeColumns();
		};
		listViewUnits.TargetUpdated += delegate
		{
			QueueAutoSizeColumns();
		};
		return listViewUnits;
	}

	private void OnTemplateListPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		object originalSource = e.OriginalSource;
		if (!IsFromGroupToggleButton((DependencyObject)((originalSource is DependencyObject) ? originalSource : null)))
		{
			ListView itemsControl = listViewUnits;
			object originalSource2 = e.OriginalSource;
			ListViewItem listViewItem = ItemsControl.ContainerFromElement(itemsControl, (DependencyObject)((originalSource2 is DependencyObject) ? originalSource2 : null)) as ListViewItem;
			if (listViewItem?.DataContext != null && object.Equals(listViewItem.DataContext, listViewUnits?.SelectedItem) && base.DataContext is EditorDockpaneViewModel editorDockpaneViewModel && editorDockpaneViewModel.ActivateSelectedTemplateCommand.CanExecute(null))
			{
				editorDockpaneViewModel.ActivateSelectedTemplateCommand.Execute(null);
			}
		}
	}

	private void OnTemplateListPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
	{
		ListView itemsControl = listViewUnits;
		object originalSource = e.OriginalSource;
		ListViewItem listViewItem = ItemsControl.ContainerFromElement(itemsControl, (DependencyObject)((originalSource is DependencyObject) ? originalSource : null)) as ListViewItem;
		if (!(listViewItem?.DataContext is DisplayTemplate displayTemplate))
		{
			_contextMenuTarget = null;
			return;
		}
		_contextMenuTarget = displayTemplate;
		UpdateFavouriteMenuItem(displayTemplate);
		UpdateContinuousPlacementMenuItem();
		UpdateMirrorPlacementMenuItems();
		UpdatePlaceWithOverridesMenuItem();
		UpdatePlaceAtOffsetMenuItem();
		_itemContextMenu.PlacementTarget = listViewItem;
		_itemContextMenu.IsOpen = true;
		e.Handled = true;
	}

	private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
	{
		if (!(base.DataContext is EditorDockpaneViewModel))
		{
			e.Handled = true;
			return;
		}
		object originalSource = e.OriginalSource;
		DependencyObject val = (DependencyObject)((originalSource is DependencyObject) ? originalSource : null);
		while (val != null && !(val is ListViewItem))
		{
			val = GetParentObject(val);
		}
		if (val is ListViewItem { DataContext: DisplayTemplate dataContext })
		{
			_contextMenuTarget = dataContext;
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
		UpdatePlaceAtOffsetMenuItem();
	}

	private MenuItem CreateMirrorMenuItem(string header, PlacementMirrorMode mirrorMode)
	{
		MenuItem menuItem = new MenuItem
		{
			Header = header,
			Tag = mirrorMode
		};
		menuItem.Click += OnMirrorPlacementMenuItemClick;
		return menuItem;
	}

	private void UpdateContinuousPlacementMenuItem()
	{
		bool flag = AddinConfiguration.Settings?.EnableContinuousPlacementMode ?? false;
		_continuousPlacementMenuItem.Header = (flag ? "Place Continuously (On)" : "Place Continuously");
		_continuousPlacementMenuItem.IsChecked = flag;
		_stopContinuousPlacementMenuItem.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
	}

	private void UpdateMirrorPlacementMenuItems()
	{
		PlacementMirrorMode placementMirrorMode = AddinConfiguration.PlacementMirrorMode;
		_normalMirrorMenuItem.IsChecked = placementMirrorMode == PlacementMirrorMode.None;
		_horizontalMirrorMenuItem.IsChecked = placementMirrorMode == PlacementMirrorMode.Horizontal;
		_verticalMirrorMenuItem.IsChecked = placementMirrorMode == PlacementMirrorMode.Vertical;
		_bothMirrorMenuItem.IsChecked = placementMirrorMode == PlacementMirrorMode.Both;
	}

	private void UpdatePlaceWithOverridesMenuItem()
	{
		bool flag = PlacementAttributeOverrideService.Definitions.Count > 0;
		_placeWithOverridesMenuItem.IsEnabled = flag;
		_placeWithOverridesMenuItem.ToolTip = (flag ? "Choose one-time attribute overrides for the next placement of this template." : "No packaged placement override fields are currently available.");
	}

	private void UpdatePlaceAtOffsetMenuItem()
	{
		_placeAtOffsetMenuItem.IsEnabled = _contextMenuTarget != null;
		_placeAtOffsetMenuItem.ToolTip = "Available for point templates. Click the insert point, then move around the distance ring to choose the offset direction. The original insert point remains the line split location.";
	}

	private void UpdateFavouriteMenuItem(DisplayTemplate template)
	{
		bool valueOrDefault = AddinConfiguration.Settings?.FavouriteTemplateKeys?.Exists((string k) => string.Equals(k, template.UniqueKey, StringComparison.OrdinalIgnoreCase)) == true;
		_favouriteMenuItem.Header = (valueOrDefault ? "Remove from Favourites" : "Add to Favourites");
	}

	private void OnFavouriteMenuItemClick(object sender, RoutedEventArgs e)
	{
		if (base.DataContext is EditorDockpaneViewModel editorDockpaneViewModel && _contextMenuTarget != null)
		{
			editorDockpaneViewModel.ToggleFavouriteCommand.Execute(_contextMenuTarget);
		}
	}

	private void OnContinuousPlacementMenuItemClick(object sender, RoutedEventArgs e)
	{
		if (base.DataContext is EditorDockpaneViewModel editorDockpaneViewModel && _contextMenuTarget != null)
		{
			editorDockpaneViewModel.ActivateContinuousPlacementCommand.Execute(_contextMenuTarget);
		}
	}

	private void OnStopContinuousPlacementMenuItemClick(object sender, RoutedEventArgs e)
	{
		if (base.DataContext is EditorDockpaneViewModel editorDockpaneViewModel)
		{
			editorDockpaneViewModel.StopContinuousPlacementCommand.Execute(null);
		}
	}

	private void OnMirrorPlacementMenuItemClick(object sender, RoutedEventArgs e)
	{
		if (base.DataContext is EditorDockpaneViewModel editorDockpaneViewModel && _contextMenuTarget != null && sender is MenuItem { Tag: var tag } && tag is PlacementMirrorMode item)
		{
			editorDockpaneViewModel.ActivateMirrorPlacementCommand.Execute(Tuple.Create(_contextMenuTarget, item));
		}
	}

	private void OnPlaceWithOverridesMenuItemClick(object sender, RoutedEventArgs e)
	{
		if (base.DataContext is EditorDockpaneViewModel editorDockpaneViewModel && _contextMenuTarget != null)
		{
			editorDockpaneViewModel.PlaceWithOverridesCommand.Execute(_contextMenuTarget);
		}
	}

	private void OnPlaceAtOffsetMenuItemClick(object sender, RoutedEventArgs e)
	{
		if (base.DataContext is EditorDockpaneViewModel editorDockpaneViewModel && _contextMenuTarget != null)
		{
			editorDockpaneViewModel.PlaceAtOffsetCommand.Execute(_contextMenuTarget);
		}
	}

	private static bool IsFromGroupToggleButton(DependencyObject source)
	{
		while (source != null)
		{
			if (source is Button { Tag: string tag } && tag == "GroupToggle")
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
		if (!(source is FrameworkContentElement { Parent: var parent }))
		{
			return LogicalTreeHelper.GetParent(source);
		}
		return parent;
	}

	private void OnTemplateViewChecked(object sender, RoutedEventArgs e)
	{
		if (sender is RadioButton { Tag: string tag } && !string.Equals(tag, _currentTemplateViewKey, StringComparison.OrdinalIgnoreCase))
		{
			SaveCurrentTemplateScrollOffset();
			_currentTemplateViewKey = tag;
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)new Action(RestoreCurrentTemplateScrollOffset), (DispatcherPriority)3, Array.Empty<object>());
		}
	}

	private void SaveCurrentTemplateScrollOffset()
	{
		ScrollViewer scrollViewer = FindVisualChild<ScrollViewer>((DependencyObject)(object)listViewUnits);
		if (scrollViewer != null && !string.IsNullOrWhiteSpace(_currentTemplateViewKey))
		{
			_scrollOffsetsByView[_currentTemplateViewKey] = (scrollViewer.HorizontalOffset, scrollViewer.VerticalOffset);
		}
	}

	private void RestoreCurrentTemplateScrollOffset()
	{
		ScrollViewer scrollViewer = FindVisualChild<ScrollViewer>((DependencyObject)(object)listViewUnits);
		if (scrollViewer != null)
		{
			if (!_scrollOffsetsByView.TryGetValue(_currentTemplateViewKey, out (double, double) value))
			{
				value = (0.0, 0.0);
			}
			scrollViewer.ScrollToHorizontalOffset(value.Item1);
			scrollViewer.ScrollToVerticalOffset(value.Item2);
		}
	}

	private static TChild FindVisualChild<TChild>(DependencyObject parent) where TChild : DependencyObject
	{
		if (parent == null)
		{
			return default(TChild);
		}
		int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
		for (int i = 0; i < childrenCount; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, i);
			TChild val = (TChild)(object)((child is TChild) ? child : null);
			if (val != null)
			{
				return val;
			}
			TChild val2 = FindVisualChild<TChild>(child);
			if (val2 != null)
			{
				return val2;
			}
		}
		return default(TChild);
	}

	private void OnPreviewKeyDown(object sender, KeyEventArgs e)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Invalid comparison between Unknown and I4
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Invalid comparison between Unknown and I4
		if (base.DataContext is EditorDockpaneViewModel editorDockpaneViewModel)
		{
			if ((int)e.Key == 6 && editorDockpaneViewModel.ActivateSelectedTemplateCommand.CanExecute(null))
			{
				editorDockpaneViewModel.ActivateSelectedTemplateCommand.Execute(null);
				e.Handled = true;
			}
			else if ((int)e.Key == 13 && editorDockpaneViewModel.DeactivateTemplateCommand.CanExecute(null))
			{
				editorDockpaneViewModel.DeactivateTemplateCommand.Execute(null);
				e.Handled = true;
			}
		}
	}

	private static DataTemplate CreateTextCellTemplate(string bindingPath)
	{
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(SearchHighlightTextBlock));
		frameworkElementFactory.SetBinding(SearchHighlightTextBlock.HighlightTextProperty, new Binding(bindingPath));
		frameworkElementFactory.SetBinding(SearchHighlightTextBlock.SearchTextProperty, new Binding("DataContext.SearchText")
		{
			RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(EditorDockpaneView), 1)
		});
		frameworkElementFactory.SetBinding(FrameworkElement.ToolTipProperty, new Binding(bindingPath));
		frameworkElementFactory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.NoWrap);
		frameworkElementFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.None);
		frameworkElementFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0.0, 0.0, 12.0, 0.0));
		return new DataTemplate
		{
			VisualTree = frameworkElementFactory
		};
	}

	private DataTemplate CreateNameCellTemplate()
	{
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(DockPanel));
		frameworkElementFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0.0, 0.0, 12.0, 0.0));
		frameworkElementFactory.SetValue(DockPanel.LastChildFillProperty, true);
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(Button));
		frameworkElementFactory2.SetValue(FrameworkElement.WidthProperty, 18.0);
		frameworkElementFactory2.SetValue(FrameworkElement.HeightProperty, 18.0);
		frameworkElementFactory2.SetValue(FrameworkElement.MarginProperty, new Thickness(0.0, 0.0, 4.0, 0.0));
		frameworkElementFactory2.SetValue(Control.PaddingProperty, new Thickness(0.0));
		frameworkElementFactory2.SetValue(Control.BackgroundProperty, Brushes.Transparent);
		frameworkElementFactory2.SetValue(Control.BorderBrushProperty, Brushes.Transparent);
		frameworkElementFactory2.SetValue(Control.BorderThicknessProperty, new Thickness(0.0));
		frameworkElementFactory2.SetValue(FrameworkElement.StyleProperty, CreateGroupToggleButtonStyle());
		frameworkElementFactory2.SetBinding(Control.ForegroundProperty, new Binding("Foreground")
		{
			RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ListViewItem), 1),
			FallbackValue = SystemColors.ControlTextBrush
		});
		frameworkElementFactory2.SetValue(FrameworkElement.FocusVisualStyleProperty, null);
		frameworkElementFactory2.SetValue(FrameworkElement.TagProperty, "GroupToggle");
		frameworkElementFactory2.SetValue(FrameworkElement.ToolTipProperty, "Expand group");
		frameworkElementFactory2.SetBinding(ContentControl.ContentProperty, new Binding("IsExpanded")
		{
			Converter = new GroupExpansionGlyphConverter()
		});
		frameworkElementFactory2.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnGroupToggleButtonClick));
		frameworkElementFactory2.SetBinding(UIElement.VisibilityProperty, new Binding("HasChildTemplates")
		{
			Converter = new BooleanToVisibilityConverter()
		});
		frameworkElementFactory2.SetValue(DockPanel.DockProperty, Dock.Left);
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		FrameworkElementFactory frameworkElementFactory3 = CreateHighlightedTextFactory("DisplayName");
		frameworkElementFactory3.SetValue(TextBlock.TextWrappingProperty, TextWrapping.NoWrap);
		frameworkElementFactory3.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.None);
		frameworkElementFactory3.SetBinding(FrameworkElement.MarginProperty, new Binding("IsIndentedChild")
		{
			Converter = new ChildRowIndentConverter()
		});
		frameworkElementFactory.AppendChild(frameworkElementFactory3);
		return new DataTemplate
		{
			VisualTree = frameworkElementFactory
		};
	}

	private void OnGroupToggleButtonClick(object sender, RoutedEventArgs e)
	{
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		ListView itemsControl = listViewUnits;
		object originalSource = e.OriginalSource;
		ListViewItem listViewItem = ItemsControl.ContainerFromElement(itemsControl, (DependencyObject)((originalSource is DependencyObject) ? originalSource : null)) as ListViewItem;
		object obj = listViewItem?.DataContext;
		DisplayTemplate template = obj as DisplayTemplate;
		if (template != null && base.DataContext is EditorDockpaneViewModel editorDockpaneViewModel)
		{
			ScrollViewer scrollViewer = FindVisualChild<ScrollViewer>((DependencyObject)(object)listViewUnits);
			double originalVerticalOffset = scrollViewer?.VerticalOffset ?? 0.0;
			double num;
			if (scrollViewer != null)
			{
				Point val = listViewItem.TransformToAncestor(scrollViewer).Transform(new Point(0.0, 0.0));
				num = val.Y;
			}
			else
			{
				num = 0.0;
			}
			double originalGroupY = num;
			editorDockpaneViewModel.ToggleGroupExpansion(template);
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
			{
				RestoreExpandedGroupPosition(template, originalVerticalOffset, originalGroupY);
			}, (DispatcherPriority)3, Array.Empty<object>());
			e.Handled = true;
		}
	}

	private void RestoreExpandedGroupPosition(DisplayTemplate template, double originalVerticalOffset, double originalGroupY)
	{
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		ScrollViewer scrollViewer = FindVisualChild<ScrollViewer>((DependencyObject)(object)listViewUnits);
		ListViewItem listViewItem = listViewUnits.ItemContainerGenerator.ContainerFromItem(template) as ListViewItem;
		if (scrollViewer != null && listViewItem != null)
		{
			listViewUnits.UpdateLayout();
			Point val = listViewItem.TransformToAncestor(scrollViewer).Transform(new Point(0.0, 0.0));
			double y = val.Y;
			scrollViewer.ScrollToVerticalOffset(Math.Max(0.0, originalVerticalOffset + y - originalGroupY));
		}
	}

	private static Style CreateGroupToggleButtonStyle()
	{
		Style style = new Style(typeof(Button));
		style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
		style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
		style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0.0)));
		style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0.0)));
		style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
		ControlTemplate controlTemplate = new ControlTemplate(typeof(Button));
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(ContentPresenter));
		frameworkElementFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		frameworkElementFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		controlTemplate.VisualTree = frameworkElementFactory;
		style.Setters.Add(new Setter(Control.TemplateProperty, controlTemplate));
		Trigger trigger = new Trigger
		{
			Property = UIElement.IsMouseOverProperty,
			Value = true
		};
		trigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(byte.MaxValue, 232, 232))));
		style.Triggers.Add(trigger);
		return style;
	}

	private static FrameworkElementFactory CreateHighlightedTextFactory(string bindingPath)
	{
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(SearchHighlightTextBlock));
		frameworkElementFactory.SetBinding(SearchHighlightTextBlock.HighlightTextProperty, new Binding(bindingPath));
		frameworkElementFactory.SetBinding(SearchHighlightTextBlock.SearchTextProperty, new Binding("DataContext.SearchText")
		{
			RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(EditorDockpaneView), 1)
		});
		frameworkElementFactory.SetBinding(FrameworkElement.ToolTipProperty, new Binding(bindingPath));
		return frameworkElementFactory;
	}

	private void QueueAutoSizeColumns()
	{
		if (listViewUnits != null)
		{
			((DispatcherObject)listViewUnits).Dispatcher.BeginInvoke((Delegate)new Action(AutoSizeColumns), (DispatcherPriority)4, Array.Empty<object>());
		}
	}

	private void AutoSizeColumns()
	{
		if (listViewUnits?.Items != null && _nameColumn != null && _typeColumn != null && _descriptionColumn != null)
		{
			DisplayTemplate[] source = listViewUnits.Items.OfType<DisplayTemplate>().ToArray();
			double width = MeasureColumnWidth("Name", source.SelectMany(GetNameColumnText), 0.0) + 36.0;
			double width2 = MeasureColumnWidth("Template Type", source.Select((DisplayTemplate template) => template.TemplateType), 0.0);
			double width3 = MeasureColumnWidth("Description", source.Select((DisplayTemplate template) => template.Description), 0.0);
			_nameColumn.Width = width;
			_typeColumn.Width = width2;
			_descriptionColumn.Width = width3;
		}
	}

	private double MeasureColumnWidth(string header, IEnumerable<string> values, double minimumWidth)
	{
		double num = MeasureText(header);
		foreach (string value in values)
		{
			num = Math.Max(num, MeasureText(value));
		}
		return Math.Ceiling(Math.Max(minimumWidth, num + 28.0));
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
		FormattedText formattedText = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(listViewUnits.FontFamily, listViewUnits.FontStyle, listViewUnits.FontWeight, listViewUnits.FontStretch), listViewUnits.FontSize, listViewUnits.Foreground, dpi.PixelsPerDip);
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
		button.SetBinding(ButtonBase.CommandProperty, new Binding("SortCommand"));
		return button;
	}

	private static Brush GetTableHeaderForeground()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Invalid comparison between Unknown and I4
		return ((int)FrameworkApplication.ApplicationTheme == 1) ? Brushes.White : SystemColors.ControlTextBrush;
	}

	private static Brush GetPrimaryForegroundBrush()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Invalid comparison between Unknown and I4
		return ((int)FrameworkApplication.ApplicationTheme == 1) ? new SolidColorBrush(Color.FromRgb(238, 238, 238)) : new SolidColorBrush(Color.FromRgb(32, 32, 32));
	}

	private static Brush GetMutedForegroundBrush()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Invalid comparison between Unknown and I4
		return ((int)FrameworkApplication.ApplicationTheme == 1) ? new SolidColorBrush(Color.FromRgb(174, 174, 174)) : new SolidColorBrush(Color.FromRgb(96, 96, 96));
	}

	private static Brush GetPanelBackgroundBrush()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Invalid comparison between Unknown and I4
		return ((int)FrameworkApplication.ApplicationTheme == 1) ? new SolidColorBrush(Color.FromRgb(45, 45, 48)) : new SolidColorBrush(Color.FromRgb(247, 247, 247));
	}

	private static Brush GetTableBackgroundBrush()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Invalid comparison between Unknown and I4
		return ((int)FrameworkApplication.ApplicationTheme == 1) ? new SolidColorBrush(Color.FromRgb(28, 28, 28)) : Brushes.White;
	}

	private static Brush GetSubtleBorderBrush()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Invalid comparison between Unknown and I4
		return ((int)FrameworkApplication.ApplicationTheme == 1) ? new SolidColorBrush(Color.FromRgb(86, 86, 86)) : new SolidColorBrush(Color.FromRgb(196, 196, 196));
	}

	private static Style CreateTemplateListItemStyle(bool compact)
	{
		//IL_0275: Unknown result type (might be due to invalid IL or missing references)
		//IL_027b: Invalid comparison between Unknown and I4
		//IL_02ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f3: Invalid comparison between Unknown and I4
		//IL_03a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03af: Invalid comparison between Unknown and I4
		//IL_0421: Unknown result type (might be due to invalid IL or missing references)
		//IL_0427: Invalid comparison between Unknown and I4
		Style style = new Style(typeof(ListViewItem));
		style.Setters.Add(new Setter(Control.ForegroundProperty, GetPrimaryForegroundBrush()));
		style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
		style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
		style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0.0)));
		style.Setters.Add(new Setter(Control.PaddingProperty, compact ? new Thickness(3.0, 0.0, 3.0, 0.0) : new Thickness(4.0, 2.0, 4.0, 2.0)));
		style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
		ControlTemplate controlTemplate = new ControlTemplate(typeof(ListViewItem));
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory.SetBinding(Border.BackgroundProperty, new Binding("Background")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		frameworkElementFactory.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		frameworkElementFactory.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(GridViewRowPresenter));
		frameworkElementFactory2.SetBinding(GridViewRowPresenter.ContentProperty, new Binding());
		frameworkElementFactory2.SetBinding(GridViewRowPresenterBase.ColumnsProperty, new Binding("View.Columns")
		{
			RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ListView), 1)
		});
		frameworkElementFactory2.SetBinding(FrameworkElement.MarginProperty, new Binding("Padding")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		frameworkElementFactory2.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		controlTemplate.VisualTree = frameworkElementFactory;
		style.Setters.Add(new Setter(Control.TemplateProperty, controlTemplate));
		Trigger trigger = new Trigger
		{
			Property = UIElement.IsMouseOverProperty,
			Value = true
		};
		trigger.Setters.Add(new Setter(Control.BackgroundProperty, ((int)FrameworkApplication.ApplicationTheme == 1) ? new SolidColorBrush(Color.FromRgb(40, 48, 56)) : new SolidColorBrush(Color.FromRgb(232, 242, 252))));
		style.Triggers.Add(trigger);
		Trigger trigger2 = new Trigger
		{
			Property = ListBoxItem.IsSelectedProperty,
			Value = true
		};
		trigger2.Setters.Add(new Setter(Control.BackgroundProperty, ((int)FrameworkApplication.ApplicationTheme == 1) ? new SolidColorBrush(Color.FromRgb(24, 74, 116)) : new SolidColorBrush(Color.FromRgb(210, 231, 250))));
		trigger2.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(51, 153, byte.MaxValue))));
		trigger2.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(3.0, 0.0, 0.0, 0.0)));
		trigger2.Setters.Add(new Setter(Control.ForegroundProperty, ((int)FrameworkApplication.ApplicationTheme == 1) ? Brushes.White : Brushes.Black));
		style.Triggers.Add(trigger2);
		MultiTrigger multiTrigger = new MultiTrigger
		{
			Conditions = 
			{
				new Condition(ListBoxItem.IsSelectedProperty, true),
				new Condition(UIElement.IsMouseOverProperty, true)
			}
		};
		multiTrigger.Setters.Add(new Setter(Control.BackgroundProperty, ((int)FrameworkApplication.ApplicationTheme == 1) ? new SolidColorBrush(Color.FromRgb(28, 86, 132)) : new SolidColorBrush(Color.FromRgb(198, 224, 248))));
		style.Triggers.Add(multiTrigger);
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
		ControlTemplate controlTemplate = new ControlTemplate(typeof(Button));
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(ContentPresenter));
		frameworkElementFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
		frameworkElementFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		controlTemplate.VisualTree = frameworkElementFactory;
		style.Setters.Add(new Setter(Control.TemplateProperty, controlTemplate));
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
		ControlTemplate controlTemplate = new ControlTemplate(typeof(Button));
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(ContentPresenter));
		frameworkElementFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		frameworkElementFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		controlTemplate.VisualTree = frameworkElementFactory;
		style.Setters.Add(new Setter(Control.TemplateProperty, controlTemplate));
		style.Triggers.Add(new DataTrigger
		{
			Binding = new Binding("SearchText"),
			Value = string.Empty,
			Setters = { (SetterBase)new Setter(UIElement.VisibilityProperty, Visibility.Collapsed) }
		});
		Trigger trigger = new Trigger
		{
			Property = UIElement.IsMouseOverProperty,
			Value = true
		};
		trigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(byte.MaxValue, 232, 232))));
		style.Triggers.Add(trigger);
		return style;
	}

	private RadioButton CreateRadioButton(string text, string bindingPath, bool compact)
	{
		RadioButton radioButton = new RadioButton
		{
			Content = text,
			Tag = text,
			Margin = new Thickness(0.0, 0.0, compact ? 4.0 : 6.0, 0.0),
			Padding = (compact ? new Thickness(8.0, 3.0, 8.0, 3.0) : new Thickness(10.0, 4.0, 10.0, 4.0)),
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
		//IL_01d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01db: Invalid comparison between Unknown and I4
		Style style = new Style(typeof(RadioButton));
		style.Setters.Add(new Setter(Control.BackgroundProperty, GetPanelBackgroundBrush()));
		style.Setters.Add(new Setter(Control.BorderBrushProperty, GetSubtleBorderBrush()));
		style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1.0)));
		style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
		ControlTemplate controlTemplate = new ControlTemplate(typeof(RadioButton));
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4.0));
		frameworkElementFactory.SetBinding(Border.BackgroundProperty, new Binding("Background")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		frameworkElementFactory.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		frameworkElementFactory.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(ContentPresenter));
		frameworkElementFactory2.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		frameworkElementFactory2.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		frameworkElementFactory2.SetBinding(FrameworkElement.MarginProperty, new Binding("Padding")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		controlTemplate.VisualTree = frameworkElementFactory;
		style.Setters.Add(new Setter(Control.TemplateProperty, controlTemplate));
		Trigger trigger = new Trigger
		{
			Property = ToggleButton.IsCheckedProperty,
			Value = true
		};
		trigger.Setters.Add(new Setter(Control.BackgroundProperty, ((int)FrameworkApplication.ApplicationTheme == 1) ? new SolidColorBrush(Color.FromRgb(35, 82, 130)) : new SolidColorBrush(Color.FromRgb(214, 234, 252))));
		trigger.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(51, 153, byte.MaxValue))));
		style.Triggers.Add(trigger);
		return style;
	}
}
