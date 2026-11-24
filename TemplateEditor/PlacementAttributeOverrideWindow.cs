using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ArcGIS.Desktop.Framework;

namespace TemplateEditor;

internal sealed class PlacementAttributeOverrideWindow : ArcGIS.Desktop.Framework.Controls.ProWindow
{
	private static Brush WindowBackgroundBrush => Application.Current?.TryFindResource("Esri_ControlBackgroundBrush") as Brush ?? DialogAppearance.Background;

	private static Brush SurfaceBackgroundBrush => Application.Current?.TryFindResource("Esri_ControlBackgroundBrush") as Brush ?? DialogAppearance.InputBackground;

	private static Brush PanelBorderBrush => Application.Current?.TryFindResource("Esri_BorderBrush") as Brush ?? DialogAppearance.SectionBorder;

	private static Brush TextBrush => Application.Current?.TryFindResource("Esri_TextControlBrush") as Brush ?? DialogAppearance.Foreground;

	private static Brush SecondaryTextBrush => Application.Current?.TryFindResource("Esri_TextStyleSubduedBrush") as Brush ?? DialogAppearance.SecondaryForeground;

	private readonly ComboBox _favouriteComboBox;

	private readonly Border _partEditorHost;

	private readonly ListBox _partListBox;

	private ListBox _fieldListBox;

	private PlacementAttributeEditorPartState _selectedPart;

	public PlacementAttributeEditorModel EditorModel { get; }

	public PlacementAttributeOverrideWindow(PlacementAttributeEditorModel editorModel)
	{
		EditorModel = editorModel ?? new PlacementAttributeEditorModel();
		if (TryFindResource("Esri_Button") is Style buttonStyle)
		{
			Resources[typeof(Button)] = new Style(typeof(Button), buttonStyle);
		}
		base.Title = "Placement Attribute Overrides";
		base.Width = 900.0;
		base.Height = 640.0;
		base.MinWidth = 720.0;
		base.MinHeight = 500.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		base.FontFamily = new FontFamily("Segoe UI");
		base.FontSize = 11.0;
		_partEditorHost = new Border
		{
			Background = SurfaceBackgroundBrush,
			BorderBrush = PanelBorderBrush,
			BorderThickness = new Thickness(1.0),
			Padding = new Thickness(5.0)
		};
		_partListBox = BuildPartListBox();
		_favouriteComboBox = BuildFavouriteComboBox();
		base.Content = BuildContent();
		SelectInitialPart();
		RefreshFavouriteChoices();
	}

	private UIElement BuildContent()
	{
		DockPanel dockPanel = new DockPanel();
		Border element = new Border
		{
			BorderBrush = PanelBorderBrush,
			BorderThickness = new Thickness(0.0, 1.0, 0.0, 0.0),
			Padding = new Thickness(10.0),
			Child = BuildFooter()
		};
		DockPanel.SetDock(element, Dock.Bottom);
		dockPanel.Children.Add(element);
		Grid grid = new Grid
		{
			Margin = new Thickness(10.0)
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
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		TextBlock element2 = new TextBlock
		{
			Text = EditorModel.TemplateDisplayName,
			FontSize = 14.0,
			FontWeight = FontWeights.SemiBold,
			Foreground = TextBrush,
			Margin = new Thickness(0.0, 0.0, 0.0, 3.0)
		};
		grid.Children.Add(element2);
		TextBlock element3 = new TextBlock
		{
			Text = "Review the fields for the next placement. Template defaults and session overrides are already reflected here; any edits below apply only once.",
			Foreground = SecondaryTextBrush,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 0.0, 0.0, 6.0),
			FontSize = 11
		};
		grid.Children.Add(element3);
		Grid.SetRow(element3, 1);
		Grid grid2 = new Grid();
		grid2.VerticalAlignment = VerticalAlignment.Stretch;
		const double dividerWidth = 10;
		const double minimumPartsWidth = 180;
		const double minimumEditorWidth = 450;
		bool hasPartsList = EditorModel.Parts.Count > 1;
		var partsColumn = new ColumnDefinition
		{
			Width = new GridLength(hasPartsList ? MeasurePartsWidth() : 0),
			MinWidth = hasPartsList ? minimumPartsWidth : 0
		};
		grid2.ColumnDefinitions.Add(partsColumn);
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(hasPartsList ? dividerWidth : 0)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1, GridUnitType.Star),
			MinWidth = hasPartsList ? minimumEditorWidth : 0
		});
		if (hasPartsList)
		{
			grid2.SizeChanged += (_, args) =>
				partsColumn.MaxWidth = Math.Max(minimumPartsWidth, args.NewSize.Width - dividerWidth - minimumEditorWidth);

			Grid grid3 = new Grid();
			grid3.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			grid3.RowDefinitions.Add(new RowDefinition
			{
				Height = new GridLength(1.0, GridUnitType.Star)
			});
			grid3.Children.Add(new TextBlock
			{
				Text = "Placement Parts",
				Foreground = SecondaryTextBrush,
				Margin = new Thickness(2.0, 0.0, 0.0, 2.0),
				FontSize = 11,
				FontWeight = FontWeights.SemiBold
			});
			Border element4 = new Border
			{
				Background = SurfaceBackgroundBrush,
				BorderBrush = PanelBorderBrush,
				BorderThickness = new Thickness(1.0),
				Padding = new Thickness(2.0),
				Child = _partListBox
			};
			grid3.Children.Add(element4);
			Grid.SetRow(element4, 1);
			grid2.Children.Add(grid3);
			Grid.SetColumn(grid3, 0);

			var splitter = new GridSplitter
			{
				Width = 6,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Stretch,
				ResizeDirection = GridResizeDirection.Columns,
				ResizeBehavior = GridResizeBehavior.PreviousAndNext,
				ToolTip = "Drag to resize Placement Parts"
			};
			splitter.SetResourceReference(Control.BackgroundProperty, "Esri_BorderBrush");
			System.Windows.Automation.AutomationProperties.SetName(splitter, "Resize Placement Parts");
			Grid.SetColumn(splitter, 1);
			grid2.Children.Add(splitter);
		}
		grid2.Children.Add(_partEditorHost);
		Grid.SetColumn(_partEditorHost, 2);
		grid.Children.Add(grid2);
		Grid.SetRow(grid2, 2);
		dockPanel.Children.Add(grid);
		return dockPanel;
	}

	private double MeasurePartsWidth()
	{
		var available = new Size(double.PositiveInfinity, double.PositiveInfinity);
		double longest = 0;
		foreach (ListBoxItem item in _partListBox.Items)
		{
			item.Measure(available);
			longest = Math.Max(longest, item.DesiredSize.Width);
		}

		const double frameAndItemInsets = 14;
		return Math.Ceiling(longest + frameAndItemInsets + SystemParameters.VerticalScrollBarWidth);
	}

	private ListBox BuildPartListBox()
	{
		ListBox listBox = new ListBox
		{
			BorderThickness = new Thickness(0),
		};
		if (EditorModel.Parts.Count > 1)
		{
			foreach (PlacementAttributeEditorPartState part in EditorModel.Parts)
			{
				listBox.Items.Add(new ListBoxItem
				{
					Content = BuildPartListItem(part),
					HorizontalContentAlignment = HorizontalAlignment.Stretch,
					Tag = part
				});
			}
		}
		ScrollViewer.SetHorizontalScrollBarVisibility(listBox, ScrollBarVisibility.Disabled);
		ScrollViewer.SetVerticalScrollBarVisibility(listBox, ScrollBarVisibility.Auto);
		listBox.SelectionChanged += PartListBox_SelectionChanged;
		return listBox;
	}

	private ComboBox BuildFavouriteComboBox()
	{
		var favourites = new ComboBox
		{
			Width = 220.0,
			MinHeight = 26,
			Margin = new Thickness(6.0, 0.0, 6.0, 0.0),
			MaxDropDownHeight = 260,
			IsTextSearchEnabled = true,
			DisplayMemberPath = "Name",
			SelectedValuePath = "Id"
		};
		TextSearch.SetTextPath(favourites, "Name");
		System.Windows.Automation.AutomationProperties.SetName(favourites, "Favourites");
		return favourites;
	}

	private static Border BuildPartListItem(PlacementAttributeEditorPartState part)
	{
		var row = new Border
		{
			BorderThickness = new Thickness(0, 0, 0, 1),
			Padding = new Thickness(6, 5, 6, 5),
			Child = new TextBlock
			{
				Text = part.DisplayName,
				FontWeight = FontWeights.SemiBold,
				TextWrapping = TextWrapping.Wrap,
				FontSize = 11
			}
		};
		row.SetResourceReference(Border.BorderBrushProperty, "Esri_BorderBrush");
		return row;
	}

	private void SelectInitialPart()
	{
		_selectedPart = EditorModel.Parts.FirstOrDefault();
		if (_partListBox.Items.Count > 0)
		{
			_partListBox.SelectedIndex = 0;
		}
		RefreshPartEditor();
	}

	private void PartListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_partListBox.SelectedItem is ListBoxItem { Tag: PlacementAttributeEditorPartState tag })
		{
			_selectedPart = tag;
			RefreshPartEditor();
		}
	}

	private void RefreshPartEditor()
	{
		if (_selectedPart == null)
		{
			_partEditorHost.Child = new TextBlock
			{
				Text = "No placement parts are available for editing.",
				Foreground = SecondaryTextBrush,
				TextWrapping = TextWrapping.Wrap
			};
			return;
		}
		Grid grid = new Grid();
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
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		TextBlock element = new TextBlock
		{
			Text = _selectedPart.DisplayName,
			FontSize = 11.0,
			FontWeight = FontWeights.SemiBold,
			Foreground = TextBrush,
			TextWrapping = TextWrapping.Wrap
		};
		grid.Children.Add(element);
		if (!string.IsNullOrWhiteSpace(_selectedPart.DetailText))
		{
			TextBlock element2 = new TextBlock
			{
				Text = _selectedPart.DetailText,
				Foreground = SecondaryTextBrush,
				TextWrapping = TextWrapping.Wrap,
				Margin = new Thickness(0.0, 1.0, 0.0, 2.0),
				FontSize = 11
			};
			grid.Children.Add(element2);
			Grid.SetRow(element2, 1);
		}
		ListBox listBox = new ListBox
		{
			BorderThickness = new Thickness(0),
			HorizontalContentAlignment = HorizontalAlignment.Stretch,
		};
		((DependencyObject)listBox).SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, (object)ScrollBarVisibility.Auto);
		((DependencyObject)listBox).SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, (object)ScrollBarVisibility.Disabled);
		((DependencyObject)listBox).SetValue(ScrollViewer.CanContentScrollProperty, (object)true);
		_fieldListBox = listBox;
		foreach (PlacementAttributeEditorFieldState attributeField in _selectedPart.AttributeFields)
		{
			listBox.Items.Add(BuildFieldRow(attributeField));
		}
		grid.Children.Add(listBox);
		Grid.SetRow(listBox, 2);
		_partEditorHost.Child = grid;
	}

	private UIElement BuildFieldRow(PlacementAttributeEditorFieldState field)
	{
		Grid grid = new Grid
		{
			Margin = new Thickness(0.0)
		};
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(240.0)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(10.0)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		StackPanel stackPanel = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = (string.IsNullOrWhiteSpace(field.Label) ? field.FieldName : field.Label),
			Foreground = TextBrush,
			FontWeight = FontWeights.SemiBold,
			TextWrapping = TextWrapping.Wrap,
			FontSize = 11
		});
		stackPanel.Children.Add(new TextBlock
		{
			Text = field.FieldName + "  |  " + field.ConfiguredValueSummary.Replace("Configured default: ", "Default: "),
			Foreground = SecondaryTextBrush,
			Margin = new Thickness(0.0, 1.0, 0.0, 0.0),
			FontSize = 11,
			TextWrapping = TextWrapping.Wrap
		});
		grid.Children.Add(stackPanel);
		Button button = new Button
		{
			Content = "Reset",
			MinWidth = 44.0,
			MinHeight = 26,
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Right,
			Padding = new Thickness(6.0, 0.0, 6.0, 0.0),
		};
		Control editor;
		if (field.HasDomainValues)
		{
			List<string> list = field.AvailableValues.ToList();
			if (!list.Contains<string>(field.CurrentValue, StringComparer.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(field.CurrentValue))
			{
				list.Insert(0, field.CurrentValue);
			}
			ComboBox comboBox = new ComboBox
			{
				ItemsSource = list,
				SelectedItem = (list.FirstOrDefault((string value) => string.Equals(value, field.CurrentValue, StringComparison.OrdinalIgnoreCase)) ?? field.CurrentValue),
				MinWidth = 130.0,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				MinHeight = 26,
				VerticalAlignment = VerticalAlignment.Center,
				Padding = new Thickness(6.0, 1.0, 6.0, 1.0),
				MaxDropDownHeight = 260,
				IsTextSearchEnabled = true,
			};
			comboBox.SelectionChanged += delegate
			{
				field.CurrentValue = (comboBox.SelectedItem as string) ?? string.Empty;
			};
			AttachFieldEditorScrollProxy(comboBox);
			editor = comboBox;
		}
		else
		{
			TextBox textBox = new TextBox
			{
				Text = (field.CurrentValue ?? string.Empty),
				MinWidth = 130.0,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				MinHeight = 26,
				VerticalAlignment = VerticalAlignment.Center,
				Padding = new Thickness(6.0, 1.0, 6.0, 1.0),
			};
			textBox.TextChanged += delegate
			{
				field.CurrentValue = textBox.Text;
			};
			editor = textBox;
		}
		button.Click += delegate
		{
			field.CurrentValue = field.ConfiguredValue ?? string.Empty;
			if (editor is ComboBox comboBox2)
			{
				comboBox2.SelectedItem = field.CurrentValue;
			}
			else if (editor is TextBox textBox2)
			{
				textBox2.Text = field.CurrentValue ?? string.Empty;
			}
		};
		System.Windows.Automation.AutomationProperties.SetLabeledBy(editor, (TextBlock)stackPanel.Children[0]);
		grid.Children.Add(editor);
		Grid.SetColumn(editor, 2);
		grid.Children.Add(button);
		Grid.SetColumn(button, 3);
		return new Border
		{
			Background = SurfaceBackgroundBrush,
			BorderBrush = PanelBorderBrush,
			BorderThickness = new Thickness(1.0),
			Padding = new Thickness(6.0, 4.0, 6.0, 4.0),
			Margin = new Thickness(0.0, 0.0, 0.0, 1.0),
			Child = grid
		};
	}

	private UIElement BuildFooter()
	{
		const double buttonHeight = 32;
		const double presetButtonWidth = 60;
		const double confirmationButtonWidth = 112;
		Grid grid = new Grid();
		grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = "Preset",
			Foreground = SecondaryTextBrush,
			VerticalAlignment = VerticalAlignment.Center,
			FontSize = 11
		});
		stackPanel.Children.Add(_favouriteComboBox);
		Button button = new Button
		{
			Content = "Apply",
			Width = presetButtonWidth,
			Height = buttonHeight,
			Margin = new Thickness(0.0, 0.0, 6.0, 0.0),
		};
		button.Click += ApplyFavouriteButton_Click;
		stackPanel.Children.Add(button);
		Button button2 = new Button
		{
			Content = "Save",
			Width = presetButtonWidth,
			Height = buttonHeight,
			Margin = new Thickness(0.0, 0.0, 6.0, 0.0),
		};
		button2.Click += SaveFavouriteButton_Click;
		stackPanel.Children.Add(button2);
		Button button3 = new Button
		{
			Content = "Delete",
			Width = presetButtonWidth,
			Height = buttonHeight,
		};
		button3.Click += DeleteFavouriteButton_Click;
		stackPanel.Children.Add(button3);
		grid.Children.Add(stackPanel);
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center
		};
		Button element = new Button
		{
			Content = "Cancel",
			Width = confirmationButtonWidth,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
			IsCancel = true,
			Height = buttonHeight,
		};
		Button button4 = new Button
		{
			Content = "Use These Values",
			Width = confirmationButtonWidth,
			IsDefault = true,
			Height = buttonHeight,
		};
		button4.Click += SaveButton_Click;
		stackPanel2.Children.Add(element);
		stackPanel2.Children.Add(button4);
		grid.Children.Add(stackPanel2);
		Grid.SetColumn(stackPanel2, 1);
		grid.SizeChanged += (_, args) =>
		{
			var unconstrained = new Size(double.PositiveInfinity, double.PositiveInfinity);
			stackPanel.Measure(unconstrained);
			stackPanel2.Measure(unconstrained);
			bool wrap = args.NewSize.Width < stackPanel.DesiredSize.Width + stackPanel2.DesiredSize.Width + 12;
			Grid.SetRow(stackPanel2, wrap ? 1 : 0);
			Grid.SetColumn(stackPanel2, wrap ? 0 : 1);
			Grid.SetColumnSpan(stackPanel2, wrap ? 2 : 1);
			stackPanel2.Margin = wrap ? new Thickness(0, 8, 0, 0) : new Thickness(0);
		};
		return grid;
	}

	private void SaveButton_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = true;
		Close();
	}

	private void ApplyFavouriteButton_Click(object sender, RoutedEventArgs e)
	{
		string favouriteId = _favouriteComboBox.SelectedValue as string;
		string text = PlacementAttributeOverrideService.ApplyFavouriteToEditorModel(EditorModel, favouriteId);
		RefreshPartEditor();
		if (!string.IsNullOrWhiteSpace(text))
		{
			DialogService.Show(text, "Template Editor");
		}
	}

	private void SaveFavouriteButton_Click(object sender, RoutedEventArgs e)
	{
		string initialValue = ((_favouriteComboBox.SelectedItem is PlacementAttributeOverrideFavouriteSummary placementAttributeOverrideFavouriteSummary && !string.IsNullOrWhiteSpace(placementAttributeOverrideFavouriteSummary.Name)) ? placementAttributeOverrideFavouriteSummary.Name : (EditorModel.TemplateDisplayName + " favourite"));
		string text = TextEntryPromptWindow.ShowPrompt("Save Override Preset", "Enter a name for this placement override preset.", initialValue, base.Owner ?? Application.Current?.MainWindow);
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		try
		{
			PlacementAttributeOverrideService.SavePlacementFavourite(EditorModel, text);
			RefreshFavouriteChoices(text);
		}
		catch (Exception ex)
		{
			DialogService.Show(ex.Message, "Template Editor");
		}
	}

	private void DeleteFavouriteButton_Click(object sender, RoutedEventArgs e)
	{
		if (!(_favouriteComboBox.SelectedItem is PlacementAttributeOverrideFavouriteSummary placementAttributeOverrideFavouriteSummary))
		{
			DialogService.Show("Choose a favourite to delete.", "Template Editor");
		}
		else if (DialogService.Show("Delete the saved placement favourite '" + placementAttributeOverrideFavouriteSummary.Name + "'?", "Template Editor", MessageBoxButton.YesNo) == MessageBoxResult.Yes && PlacementAttributeOverrideService.DeletePlacementFavourite(EditorModel.TemplateKey, placementAttributeOverrideFavouriteSummary.Id))
		{
			RefreshFavouriteChoices();
		}
	}

	private void RefreshFavouriteChoices(string selectFavouriteByName = null)
	{
		List<PlacementAttributeOverrideFavouriteSummary> list = PlacementAttributeOverrideService.GetPlacementFavourites(EditorModel.TemplateKey).ToList();
		EditorModel.AvailableFavourites = list;
		_favouriteComboBox.ItemsSource = list;
		if (!string.IsNullOrWhiteSpace(selectFavouriteByName))
		{
			_favouriteComboBox.SelectedItem = list.FirstOrDefault((PlacementAttributeOverrideFavouriteSummary favourite) => string.Equals(favourite.Name, selectFavouriteByName, StringComparison.OrdinalIgnoreCase));
		}
		else
		{
			_favouriteComboBox.SelectedIndex = ((list.Count <= 0) ? (-1) : 0);
		}
	}

	private void AttachFieldEditorScrollProxy(UIElement element)
	{
		if (element != null)
		{
			element.PreviewMouseWheel -= ForwardFieldEditorMouseWheel;
			element.PreviewMouseWheel += ForwardFieldEditorMouseWheel;
		}
	}

	private void ForwardFieldEditorMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (sender is not ComboBox { IsDropDownOpen: false } ||
			FindDescendantScrollViewer(_fieldListBox) is not ScrollViewer listScroll)
		{
			return;
		}

		e.Handled = true;
		if (e.Delta > 0)
			listScroll.LineUp();
		else if (e.Delta < 0)
			listScroll.LineDown();
	}

	private static ScrollViewer FindDescendantScrollViewer(DependencyObject parent)
	{
		if (parent == null)
		{
			return null;
		}
		if (parent is ScrollViewer result)
		{
			return result;
		}
		int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
		for (int i = 0; i < childrenCount; i++)
		{
			ScrollViewer scrollViewer = FindDescendantScrollViewer(VisualTreeHelper.GetChild(parent, i));
			if (scrollViewer != null)
			{
				return scrollViewer;
			}
		}
		return null;
	}

}
