using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ArcGIS.Desktop.Framework;

namespace TemplateEditor;

internal sealed class PlacementAttributeOverrideWindow : Window
{
	private static readonly bool IsDarkTheme = FrameworkApplication.ApplicationTheme == ApplicationTheme.Dark;

	private static readonly Brush WindowBackgroundBrush = IsDarkTheme ? new SolidColorBrush(Color.FromRgb(45, 45, 48)) : new SolidColorBrush(Color.FromRgb(243, 243, 243));

	private static readonly Brush SurfaceBackgroundBrush = IsDarkTheme ? new SolidColorBrush(Color.FromRgb(31, 31, 31)) : Brushes.White;

	private static readonly Brush PanelBorderBrush = IsDarkTheme ? new SolidColorBrush(Color.FromRgb(72, 72, 72)) : new SolidColorBrush(Color.FromRgb(208, 208, 208));

	private static readonly Brush TextBrush = IsDarkTheme ? new SolidColorBrush(Color.FromRgb(238, 238, 238)) : new SolidColorBrush(Color.FromRgb(32, 32, 32));

	private static readonly Brush SecondaryTextBrush = IsDarkTheme ? new SolidColorBrush(Color.FromRgb(205, 205, 205)) : new SolidColorBrush(Color.FromRgb(96, 96, 96));

	private static readonly Brush AccentBrush = new SolidColorBrush(Color.FromRgb(51, 153, 255));

	private static readonly Brush HoverBrush = IsDarkTheme ? new SolidColorBrush(Color.FromRgb(58, 58, 62)) : new SolidColorBrush(Color.FromRgb(238, 244, 250));

	private static readonly Brush EditorBackgroundBrush = IsDarkTheme ? new SolidColorBrush(Color.FromRgb(36, 36, 38)) : Brushes.White;

	private static readonly Brush SelectedBrush = IsDarkTheme ? new SolidColorBrush(Color.FromRgb(35, 82, 130)) : new SolidColorBrush(Color.FromRgb(214, 234, 252));

	private static readonly Brush ComboGlyphBrush = IsDarkTheme ? new SolidColorBrush(Color.FromRgb(192, 192, 192)) : new SolidColorBrush(Color.FromRgb(90, 90, 90));

	private readonly ComboBox _favouriteComboBox;

	private readonly Border _partEditorHost;

	private readonly ListBox _partListBox;

	private ListBox _fieldListBox;

	private PlacementAttributeEditorPartState _selectedPart;

	public PlacementAttributeEditorModel EditorModel { get; }

	public PlacementAttributeOverrideWindow(PlacementAttributeEditorModel editorModel)
	{
		EditorModel = editorModel ?? new PlacementAttributeEditorModel();
		Title = "Placement Attribute Overrides";
		Width = 900.0;
		Height = 640.0;
		MinWidth = 720.0;
		MinHeight = 500.0;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		Background = WindowBackgroundBrush;
		Foreground = TextBrush;
		FontFamily = new FontFamily("Segoe UI");
		FontSize = 11.0;
		Resources[SystemColors.HighlightBrushKey] = HoverBrush;
		Resources[SystemColors.HighlightTextBrushKey] = TextBrush;
		Resources[SystemColors.ControlBrushKey] = EditorBackgroundBrush;
		Resources[SystemColors.WindowBrushKey] = EditorBackgroundBrush;
		Resources[SystemColors.ControlTextBrushKey] = TextBrush;
		Resources[SystemColors.GrayTextBrushKey] = SecondaryTextBrush;
		_partEditorHost = new Border
		{
			Background = SurfaceBackgroundBrush,
			BorderBrush = PanelBorderBrush,
			BorderThickness = new Thickness(1.0),
			Padding = new Thickness(5.0)
		};
		_partListBox = BuildPartListBox();
		_favouriteComboBox = BuildFavouriteComboBox();
		Content = BuildContent();
		SelectInitialPart();
		RefreshFavouriteChoices();
	}

	private UIElement BuildContent()
	{
		DockPanel root = new DockPanel();
		Border footer = new Border
		{
			BorderBrush = PanelBorderBrush,
			BorderThickness = new Thickness(0.0, 1.0, 0.0, 0.0),
			Padding = new Thickness(10.0),
			Child = BuildFooter()
		};
		DockPanel.SetDock(footer, Dock.Bottom);
		root.Children.Add(footer);

		Grid content = new Grid
		{
			Margin = new Thickness(10.0)
		};
		content.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		content.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		content.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});

		TextBlock titleBlock = new TextBlock
		{
			Text = EditorModel.TemplateDisplayName,
			FontSize = 14.0,
			FontWeight = FontWeights.SemiBold,
			Foreground = TextBrush,
			Margin = new Thickness(0.0, 0.0, 0.0, 3.0)
		};
		content.Children.Add(titleBlock);

		TextBlock introBlock = new TextBlock
		{
			Text = "Review the fields for the next placement. Template defaults and session overrides are already reflected here; any edits below apply only once.",
			Foreground = SecondaryTextBrush,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 0.0, 0.0, 6.0),
			FontSize = 10.0
		};
		content.Children.Add(introBlock);
		Grid.SetRow(introBlock, 1);

		Grid editorGrid = new Grid();
		editorGrid.VerticalAlignment = VerticalAlignment.Stretch;
		editorGrid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = EditorModel.Parts.Count > 1 ? new GridLength(136.0) : new GridLength(0.0)
		});
		editorGrid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(10.0)
		});
		editorGrid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});

		if (EditorModel.Parts.Count > 1)
		{
			Grid leftPane = new Grid();
			leftPane.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			leftPane.RowDefinitions.Add(new RowDefinition
			{
				Height = new GridLength(1.0, GridUnitType.Star)
			});

			leftPane.Children.Add(new TextBlock
			{
				Text = "Placement Parts",
				Foreground = SecondaryTextBrush,
				Margin = new Thickness(2.0, 0.0, 0.0, 2.0),
				FontSize = 9.0,
				FontWeight = FontWeights.SemiBold
			});

			Border listBorder = new Border
			{
				Background = SurfaceBackgroundBrush,
				BorderBrush = PanelBorderBrush,
				BorderThickness = new Thickness(1.0),
				Padding = new Thickness(2.0),
				Child = new ScrollViewer
				{
					HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
					VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
					Content = _partListBox
				}
			};
			leftPane.Children.Add(listBorder);
			Grid.SetRow(listBorder, 1);

			editorGrid.Children.Add(leftPane);
			Grid.SetColumn(leftPane, 0);
		}

		editorGrid.Children.Add(_partEditorHost);
		Grid.SetColumn(_partEditorHost, 2);

		content.Children.Add(editorGrid);
		Grid.SetRow(editorGrid, 2);
		root.Children.Add(content);
		return root;
	}

	private ListBox BuildPartListBox()
	{
		ListBox listBox = new ListBox
		{
			Background = SurfaceBackgroundBrush,
			BorderThickness = new Thickness(0.0),
			Foreground = TextBrush,
			ItemContainerStyle = CreatePartListItemStyle()
		};
		if (EditorModel.Parts.Count > 1)
		{
			foreach (PlacementAttributeEditorPartState part in EditorModel.Parts)
			{
				listBox.Items.Add(new ListBoxItem
				{
					Content = BuildPartListItem(part),
					Tag = part
				});
			}
		}
		listBox.SelectionChanged += PartListBox_SelectionChanged;
		listBox.PreviewMouseWheel += ForwardPartListMouseWheel;
		return listBox;
	}

	private ComboBox BuildFavouriteComboBox()
	{
		return new ComboBox
		{
			Width = 220.0,
			Height = 24.0,
			Margin = new Thickness(6.0, 0.0, 6.0, 0.0),
			Style = CreateComboBoxStyle(),
			ItemContainerStyle = CreateComboBoxItemStyle(),
			DisplayMemberPath = "Name",
			SelectedValuePath = "Id"
		};
	}

	private static object BuildPartListItem(PlacementAttributeEditorPartState part)
	{
		StackPanel panel = new StackPanel();
		panel.Children.Add(new TextBlock
		{
			Text = part.DisplayName,
			FontWeight = FontWeights.SemiBold,
			TextWrapping = TextWrapping.Wrap,
			MaxWidth = 106.0,
			FontSize = 9.5
		});
		return panel;
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
		if (_partListBox.SelectedItem is ListBoxItem item && item.Tag is PlacementAttributeEditorPartState part)
		{
			_selectedPart = part;
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

		Grid panel = new Grid();
		panel.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		panel.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		panel.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});

		TextBlock titleBlock = new TextBlock
		{
			Text = _selectedPart.DisplayName,
			FontSize = 11.0,
			FontWeight = FontWeights.SemiBold,
			Foreground = TextBrush,
			TextWrapping = TextWrapping.Wrap
		};
		panel.Children.Add(titleBlock);
		if (!string.IsNullOrWhiteSpace(_selectedPart.DetailText))
		{
			TextBlock detailBlock = new TextBlock
			{
				Text = _selectedPart.DetailText,
				Foreground = SecondaryTextBrush,
				TextWrapping = TextWrapping.Wrap,
				Margin = new Thickness(0.0, 1.0, 0.0, 2.0),
				FontSize = 9.0
			};
			panel.Children.Add(detailBlock);
			Grid.SetRow(detailBlock, 1);
		}

		ListBox fieldList = new ListBox
		{
			Background = SurfaceBackgroundBrush,
			BorderThickness = new Thickness(0.0),
			Foreground = TextBrush,
			HorizontalContentAlignment = HorizontalAlignment.Stretch,
			ItemContainerStyle = CreateFieldListItemStyle()
		};
		fieldList.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
		fieldList.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
		fieldList.SetValue(ScrollViewer.CanContentScrollProperty, true);
		fieldList.PreviewMouseWheel += ForwardFieldListMouseWheel;
		_fieldListBox = fieldList;
		foreach (PlacementAttributeEditorFieldState field in _selectedPart.AttributeFields)
		{
			fieldList.Items.Add(BuildFieldRow(field));
		}
		panel.Children.Add(fieldList);
		Grid.SetRow(fieldList, 2);
		_partEditorHost.Child = panel;
	}

	private UIElement BuildFieldRow(PlacementAttributeEditorFieldState field)
	{
		Grid rowGrid = new Grid
		{
			Margin = new Thickness(0.0)
		};
		rowGrid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(240.0)
		});
		rowGrid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(10.0)
		});
		rowGrid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		rowGrid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});

		StackPanel metaPanel = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		metaPanel.Children.Add(new TextBlock
		{
			Text = string.IsNullOrWhiteSpace(field.Label) ? field.FieldName : field.Label,
			Foreground = TextBrush,
			FontWeight = FontWeights.SemiBold,
			TextWrapping = TextWrapping.Wrap,
			FontSize = 10.5
		});
		metaPanel.Children.Add(new TextBlock
		{
			Text = field.FieldName + "  |  " + field.ConfiguredValueSummary.Replace("Configured default: ", "Default: "),
			Foreground = SecondaryTextBrush,
			Margin = new Thickness(0.0, 1.0, 0.0, 0.0),
			FontSize = 8.5,
			TextWrapping = TextWrapping.Wrap
		});
		rowGrid.Children.Add(metaPanel);

		Button resetButton = new Button
		{
			Content = "Reset",
			MinWidth = 44.0,
			Height = 22.0,
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Right,
			Padding = new Thickness(6.0, 0.0, 6.0, 0.0),
			Style = CreateSubtleButtonStyle()
		};

		Control editor;
		if (field.HasDomainValues)
		{
			List<string> comboValues = field.AvailableValues.ToList();
			if (!comboValues.Contains(field.CurrentValue, StringComparer.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(field.CurrentValue))
			{
				comboValues.Insert(0, field.CurrentValue);
			}
			ComboBox comboBox = new ComboBox
			{
				ItemsSource = comboValues,
				SelectedItem = comboValues.FirstOrDefault(value => string.Equals(value, field.CurrentValue, StringComparison.OrdinalIgnoreCase)) ?? field.CurrentValue,
				MinWidth = 130.0,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Height = 22.0,
				VerticalAlignment = VerticalAlignment.Center,
				Padding = new Thickness(6.0, 1.0, 6.0, 1.0),
				Style = CreateComboBoxStyle(),
				ItemContainerStyle = CreateComboBoxItemStyle()
			};
			comboBox.SelectionChanged += delegate
			{
				field.CurrentValue = comboBox.SelectedItem as string ?? string.Empty;
			};
			AttachFieldEditorScrollProxy(comboBox);
			editor = comboBox;
		}
		else
		{
			TextBox textBox = new TextBox
			{
				Text = field.CurrentValue ?? string.Empty,
				MinWidth = 130.0,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Height = 22.0,
				VerticalAlignment = VerticalAlignment.Center,
				Padding = new Thickness(6.0, 1.0, 6.0, 1.0),
				Style = CreateTextBoxStyle()
			};
			textBox.TextChanged += delegate
			{
				field.CurrentValue = textBox.Text;
			};
			AttachFieldEditorScrollProxy(textBox);
			editor = textBox;
		}
		resetButton.Click += delegate
		{
			field.CurrentValue = field.ConfiguredValue ?? string.Empty;
			if (editor is ComboBox comboBox)
			{
				comboBox.SelectedItem = field.CurrentValue;
			}
			else if (editor is TextBox textBox)
			{
				textBox.Text = field.CurrentValue ?? string.Empty;
			}
		};
		rowGrid.Children.Add(editor);
		Grid.SetColumn(editor, 2);
		rowGrid.Children.Add(resetButton);
		Grid.SetColumn(resetButton, 3);

		return new Border
		{
			Background = SurfaceBackgroundBrush,
			BorderBrush = PanelBorderBrush,
			BorderThickness = new Thickness(1.0),
			Padding = new Thickness(6.0, 4.0, 6.0, 4.0),
			Margin = new Thickness(0.0, 0.0, 0.0, 1.0),
			Child = rowGrid
		};
	}

	private UIElement BuildFooter()
	{
		Grid footerGrid = new Grid();
		footerGrid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		footerGrid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});

		StackPanel favouritePanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center
		};
		favouritePanel.Children.Add(new TextBlock
		{
			Text = "Preset",
			Foreground = SecondaryTextBrush,
			VerticalAlignment = VerticalAlignment.Center,
			FontSize = 10.0
		});
		favouritePanel.Children.Add(_favouriteComboBox);
		Button applyFavouriteButton = new Button
		{
			Content = "Apply",
			MinWidth = 52.0,
			Height = 24.0,
			Margin = new Thickness(0.0, 0.0, 6.0, 0.0),
			Style = CreateButtonStyle()
		};
		applyFavouriteButton.Click += ApplyFavouriteButton_Click;
		favouritePanel.Children.Add(applyFavouriteButton);
		Button saveFavouriteButton = new Button
		{
			Content = "Save",
			MinWidth = 52.0,
			Height = 24.0,
			Margin = new Thickness(0.0, 0.0, 6.0, 0.0),
			Style = CreateButtonStyle()
		};
		saveFavouriteButton.Click += SaveFavouriteButton_Click;
		favouritePanel.Children.Add(saveFavouriteButton);
		Button deleteFavouriteButton = new Button
		{
			Content = "Delete",
			MinWidth = 56.0,
			Height = 24.0,
			Style = CreateSubtleButtonStyle()
		};
		deleteFavouriteButton.Click += DeleteFavouriteButton_Click;
		favouritePanel.Children.Add(deleteFavouriteButton);
		footerGrid.Children.Add(favouritePanel);

		StackPanel buttons = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		Button cancelButton = new Button
		{
			Content = "Cancel",
			Width = 82.0,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
			IsCancel = true,
			Height = 28.0,
			Style = CreateButtonStyle()
		};
		Button okButton = new Button
		{
			Content = "Use These Values",
			Width = 122.0,
			IsDefault = true,
			Height = 28.0,
			Background = AccentBrush,
			Foreground = Brushes.White,
			Style = CreatePrimaryButtonStyle()
		};
		okButton.Click += SaveButton_Click;
		buttons.Children.Add(cancelButton);
		buttons.Children.Add(okButton);
		footerGrid.Children.Add(buttons);
		Grid.SetColumn(buttons, 1);
		return footerGrid;
	}

	private void SaveButton_Click(object sender, RoutedEventArgs e)
	{
		DialogResult = true;
		Close();
	}

	private void ApplyFavouriteButton_Click(object sender, RoutedEventArgs e)
	{
		string favouriteId = _favouriteComboBox.SelectedValue as string;
		string message = PlacementAttributeOverrideService.ApplyFavouriteToEditorModel(EditorModel, favouriteId);
		RefreshPartEditor();
		if (!string.IsNullOrWhiteSpace(message))
		{
			DialogService.Show(message, "Template Editor");
		}
	}

	private void SaveFavouriteButton_Click(object sender, RoutedEventArgs e)
	{
		string defaultName = _favouriteComboBox.SelectedItem is PlacementAttributeOverrideFavouriteSummary summary && !string.IsNullOrWhiteSpace(summary.Name)
			? summary.Name
			: EditorModel.TemplateDisplayName + " favourite";
		string favouriteName = TextEntryPromptWindow.ShowPrompt(
			"Save Override Preset",
			"Enter a name for this placement override preset.",
			defaultName,
			Owner ?? Application.Current?.MainWindow);
		if (string.IsNullOrWhiteSpace(favouriteName))
		{
			return;
		}
		try
		{
			PlacementAttributeOverrideService.SavePlacementFavourite(EditorModel, favouriteName);
			RefreshFavouriteChoices(favouriteName);
		}
		catch (Exception ex)
		{
			DialogService.Show(ex.Message, "Template Editor");
		}
	}

	private void DeleteFavouriteButton_Click(object sender, RoutedEventArgs e)
	{
		if (_favouriteComboBox.SelectedItem is not PlacementAttributeOverrideFavouriteSummary summary)
		{
			DialogService.Show("Choose a favourite to delete.", "Template Editor");
			return;
		}
		if (DialogService.Show($"Delete the saved placement favourite '{summary.Name}'?", "Template Editor", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
		{
			return;
		}
		if (PlacementAttributeOverrideService.DeletePlacementFavourite(EditorModel.TemplateKey, summary.Id))
		{
			RefreshFavouriteChoices();
		}
	}

	private void RefreshFavouriteChoices(string selectFavouriteByName = null)
	{
		List<PlacementAttributeOverrideFavouriteSummary> favourites = PlacementAttributeOverrideService.GetPlacementFavourites(EditorModel.TemplateKey).ToList();
		EditorModel.AvailableFavourites = favourites;
		_favouriteComboBox.ItemsSource = favourites;
		if (!string.IsNullOrWhiteSpace(selectFavouriteByName))
		{
			_favouriteComboBox.SelectedItem = favourites.FirstOrDefault(favourite =>
				string.Equals(favourite.Name, selectFavouriteByName, StringComparison.OrdinalIgnoreCase));
		}
		else
		{
			_favouriteComboBox.SelectedIndex = favourites.Count > 0 ? 0 : -1;
		}
	}

	private void AttachFieldEditorScrollProxy(UIElement element)
	{
		if (element == null)
		{
			return;
		}
		element.PreviewMouseWheel -= ForwardFieldEditorMouseWheel;
		element.PreviewMouseWheel += ForwardFieldEditorMouseWheel;
	}

	private void ForwardPartListMouseWheel(object sender, MouseWheelEventArgs e)
	{
		ScrollViewer scrollViewer = FindDescendantScrollViewer(_partListBox);
		if (scrollViewer == null)
		{
			return;
		}
		scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
		e.Handled = true;
	}

	private void ForwardFieldListMouseWheel(object sender, MouseWheelEventArgs e)
	{
		ScrollViewer scrollViewer = FindDescendantScrollViewer(_fieldListBox);
		if (scrollViewer == null)
		{
			return;
		}
		scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
		e.Handled = true;
	}

	private void ForwardFieldEditorMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (sender is ComboBox comboBox && comboBox.IsDropDownOpen)
		{
			return;
		}
		ScrollViewer scrollViewer = FindDescendantScrollViewer(_fieldListBox);
		if (scrollViewer == null)
		{
			return;
		}
		scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
		e.Handled = true;
	}

	private static ScrollViewer FindDescendantScrollViewer(DependencyObject parent)
	{
		if (parent == null)
		{
			return null;
		}
		if (parent is ScrollViewer scrollViewer)
		{
			return scrollViewer;
		}
		int childCount = VisualTreeHelper.GetChildrenCount(parent);
		for (int i = 0; i < childCount; i++)
		{
			ScrollViewer match = FindDescendantScrollViewer(VisualTreeHelper.GetChild(parent, i));
			if (match != null)
			{
				return match;
			}
		}
		return null;
	}

	private static Style CreateTextBoxStyle()
	{
		Style style = new Style(typeof(TextBox));
		style.Setters.Add(new Setter(Control.BackgroundProperty, EditorBackgroundBrush));
		style.Setters.Add(new Setter(Control.ForegroundProperty, TextBrush));
		style.Setters.Add(new Setter(Control.BorderBrushProperty, PanelBorderBrush));
		style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1.0)));
		style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
		Trigger focusTrigger = new Trigger
		{
			Property = UIElement.IsKeyboardFocusedProperty,
			Value = true
		};
		focusTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, AccentBrush));
		style.Triggers.Add(focusTrigger);
		return style;
	}

	private static Style CreateComboBoxStyle()
	{
		Style style = new Style(typeof(ComboBox));
		style.Setters.Add(new Setter(Control.BackgroundProperty, EditorBackgroundBrush));
		style.Setters.Add(new Setter(Control.ForegroundProperty, TextBrush));
		style.Setters.Add(new Setter(Control.BorderBrushProperty, PanelBorderBrush));
		style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1.0)));
		style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6.0, 1.0, 6.0, 1.0)));
		style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
		style.Setters.Add(new Setter(Control.TemplateProperty, CreateComboBoxTemplate()));
		Trigger focusTrigger = new Trigger
		{
			Property = UIElement.IsKeyboardFocusedProperty,
			Value = true
		};
		focusTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, AccentBrush));
		style.Triggers.Add(focusTrigger);
		return style;
	}

	private static ControlTemplate CreateComboBoxTemplate()
	{
		ControlTemplate comboTemplate = new ControlTemplate(typeof(ComboBox));
		FrameworkElementFactory root = new FrameworkElementFactory(typeof(Grid));

		FrameworkElementFactory toggle = new FrameworkElementFactory(typeof(ToggleButton));
		toggle.SetValue(FrameworkElement.FocusVisualStyleProperty, null);
		toggle.SetValue(ButtonBase.ClickModeProperty, ClickMode.Press);
		toggle.SetBinding(Control.BackgroundProperty, new Binding("Background")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		toggle.SetBinding(Control.BorderBrushProperty, new Binding("BorderBrush")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		toggle.SetBinding(Control.BorderThicknessProperty, new Binding("BorderThickness")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		toggle.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsDropDownOpen")
		{
			RelativeSource = RelativeSource.TemplatedParent,
			Mode = BindingMode.TwoWay
		});
		toggle.SetBinding(ContentControl.ContentProperty, new Binding("SelectionBoxItem")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		toggle.SetBinding(Control.PaddingProperty, new Binding("Padding")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		toggle.SetValue(Control.TemplateProperty, CreateComboBoxToggleTemplate());
		root.AppendChild(toggle);

		FrameworkElementFactory popup = new FrameworkElementFactory(typeof(Popup));
		popup.Name = "PART_Popup";
		popup.SetValue(Popup.PlacementProperty, PlacementMode.Bottom);
		popup.SetValue(Popup.AllowsTransparencyProperty, true);
		popup.SetValue(Popup.FocusableProperty, false);
		popup.SetBinding(Popup.IsOpenProperty, new Binding("IsDropDownOpen")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		FrameworkElementFactory popupBorder = new FrameworkElementFactory(typeof(Border));
		popupBorder.SetValue(Border.BackgroundProperty, EditorBackgroundBrush);
		popupBorder.SetValue(Border.BorderBrushProperty, PanelBorderBrush);
		popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1.0));
		popupBorder.SetBinding(FrameworkElement.MinWidthProperty, new Binding("ActualWidth")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		FrameworkElementFactory scrollViewer = new FrameworkElementFactory(typeof(ScrollViewer));
		scrollViewer.SetValue(ScrollViewer.CanContentScrollProperty, true);
		scrollViewer.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
		FrameworkElementFactory itemsPresenter = new FrameworkElementFactory(typeof(ItemsPresenter));
		scrollViewer.AppendChild(itemsPresenter);
		popupBorder.AppendChild(scrollViewer);
		popup.AppendChild(popupBorder);
		root.AppendChild(popup);

		comboTemplate.VisualTree = root;
		return comboTemplate;
	}

	private static ControlTemplate CreateComboBoxToggleTemplate()
	{
		ControlTemplate toggleTemplate = new ControlTemplate(typeof(ToggleButton));
		FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
		border.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
		border.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
		border.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = RelativeSource.TemplatedParent });
		FrameworkElementFactory grid = new FrameworkElementFactory(typeof(Grid));
		grid.SetValue(FrameworkElement.MinHeightProperty, 22.0);
		FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
		content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
		content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
		content.SetBinding(ContentPresenter.ContentProperty, new Binding("Content") { RelativeSource = RelativeSource.TemplatedParent });
		content.SetValue(FrameworkElement.MarginProperty, new Thickness(6.0, 0.0, 24.0, 0.0));
		content.SetValue(TextElement.ForegroundProperty, TextBrush);
		grid.AppendChild(content);
		FrameworkElementFactory divider = new FrameworkElementFactory(typeof(Border));
		divider.SetValue(Border.BackgroundProperty, PanelBorderBrush);
		divider.SetValue(FrameworkElement.WidthProperty, 1.0);
		divider.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
		divider.SetValue(FrameworkElement.MarginProperty, new Thickness(0.0, 4.0, 20.0, 4.0));
		grid.AppendChild(divider);
		FrameworkElementFactory arrow = new FrameworkElementFactory(typeof(TextBlock));
		arrow.SetValue(TextBlock.TextProperty, "\u25BE");
		arrow.SetValue(TextBlock.ForegroundProperty, ComboGlyphBrush);
		arrow.SetValue(TextBlock.FontSizeProperty, 10.0);
		arrow.SetValue(FrameworkElement.MarginProperty, new Thickness(0.0, 0.0, 7.0, 0.0));
		arrow.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
		arrow.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		grid.AppendChild(arrow);
		border.AppendChild(grid);
		toggleTemplate.VisualTree = border;
		return toggleTemplate;
	}

	private static Style CreateComboBoxItemStyle()
	{
		Style style = new Style(typeof(ComboBoxItem));
		style.Setters.Add(new Setter(Control.BackgroundProperty, EditorBackgroundBrush));
		style.Setters.Add(new Setter(Control.ForegroundProperty, TextBrush));
		style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6.0, 2.0, 6.0, 2.0)));
		Trigger hoverTrigger = new Trigger
		{
			Property = UIElement.IsMouseOverProperty,
			Value = true
		};
		hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, HoverBrush));
		hoverTrigger.Setters.Add(new Setter(Control.ForegroundProperty, TextBrush));
		style.Triggers.Add(hoverTrigger);
		Trigger selectedTrigger = new Trigger
		{
			Property = ComboBoxItem.IsSelectedProperty,
			Value = true
		};
		selectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, AccentBrush));
		selectedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
		style.Triggers.Add(selectedTrigger);
		return style;
	}

	private static Style CreateButtonStyle()
	{
		Style style = new Style(typeof(Button));
		style.Setters.Add(new Setter(Control.BackgroundProperty, SurfaceBackgroundBrush));
		style.Setters.Add(new Setter(Control.ForegroundProperty, TextBrush));
		style.Setters.Add(new Setter(Control.BorderBrushProperty, PanelBorderBrush));
		style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1.0)));
		style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
		Trigger hoverTrigger = new Trigger
		{
			Property = UIElement.IsMouseOverProperty,
			Value = true
		};
		hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, HoverBrush));
		hoverTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, AccentBrush));
		style.Triggers.Add(hoverTrigger);
		return style;
	}

	private static Style CreateSubtleButtonStyle()
	{
		Style style = CreateButtonStyle();
		style.Setters.Add(new Setter(Control.ForegroundProperty, SecondaryTextBrush));
		return style;
	}

	private static Style CreatePrimaryButtonStyle()
	{
		Style style = CreateButtonStyle();
		style.Setters.Add(new Setter(Control.BackgroundProperty, AccentBrush));
		style.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
		style.Setters.Add(new Setter(Control.BorderBrushProperty, AccentBrush));
		Trigger hoverTrigger = new Trigger
		{
			Property = UIElement.IsMouseOverProperty,
			Value = true
		};
		hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(32, 128, 224))));
		hoverTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(32, 128, 224))));
		style.Triggers.Add(hoverTrigger);
		return style;
	}

	private static Style CreatePartListItemStyle()
	{
		Style style = new Style(typeof(ListBoxItem));
		style.Setters.Add(new Setter(Control.ForegroundProperty, TextBrush));
		style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
		style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
		style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0.0)));
		style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4.0)));
		style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
		style.Setters.Add(new Setter(Control.MarginProperty, new Thickness(0.0, 0.0, 0.0, 1.0)));
		style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
		Trigger selectedTrigger = new Trigger
		{
			Property = ListBoxItem.IsSelectedProperty,
			Value = true
		};
		selectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, SelectedBrush));
		selectedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, TextBrush));
		style.Triggers.Add(selectedTrigger);
		Trigger hoverTrigger = new Trigger
		{
			Property = UIElement.IsMouseOverProperty,
			Value = true
		};
		hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, HoverBrush));
		style.Triggers.Add(hoverTrigger);
		return style;
	}

	private static Style CreateFieldListItemStyle()
	{
		Style style = new Style(typeof(ListBoxItem));
		style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
		style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0.0)));
		style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0.0)));
		style.Setters.Add(new Setter(Control.MarginProperty, new Thickness(0.0)));
		style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
		style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
		Trigger selectedTrigger = new Trigger
		{
			Property = ListBoxItem.IsSelectedProperty,
			Value = true
		};
		selectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
		selectedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, TextBrush));
		style.Triggers.Add(selectedTrigger);
		return style;
	}
}
