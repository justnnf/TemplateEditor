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
	private static readonly bool IsDarkTheme;

	private static readonly Brush WindowBackgroundBrush;

	private static readonly Brush SurfaceBackgroundBrush;

	private static readonly Brush PanelBorderBrush;

	private static readonly Brush TextBrush;

	private static readonly Brush SecondaryTextBrush;

	private static readonly Brush AccentBrush;

	private static readonly Brush HoverBrush;

	private static readonly Brush EditorBackgroundBrush;

	private static readonly Brush SelectedBrush;

	private static readonly Brush ComboGlyphBrush;

	private readonly ComboBox _favouriteComboBox;

	private readonly Border _partEditorHost;

	private readonly ListBox _partListBox;

	private ListBox _fieldListBox;

	private PlacementAttributeEditorPartState _selectedPart;

	public PlacementAttributeEditorModel EditorModel { get; }

	public PlacementAttributeOverrideWindow(PlacementAttributeEditorModel editorModel)
	{
		EditorModel = editorModel ?? new PlacementAttributeEditorModel();
		base.Title = "Placement Attribute Overrides";
		base.Width = 900.0;
		base.Height = 640.0;
		base.MinWidth = 720.0;
		base.MinHeight = 500.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		base.Background = WindowBackgroundBrush;
		base.Foreground = TextBrush;
		base.FontFamily = new FontFamily("Segoe UI");
		base.FontSize = 11.0;
		base.Resources[SystemColors.HighlightBrushKey] = HoverBrush;
		base.Resources[SystemColors.HighlightTextBrushKey] = TextBrush;
		base.Resources[SystemColors.ControlBrushKey] = EditorBackgroundBrush;
		base.Resources[SystemColors.WindowBrushKey] = EditorBackgroundBrush;
		base.Resources[SystemColors.ControlTextBrushKey] = TextBrush;
		base.Resources[SystemColors.GrayTextBrushKey] = SecondaryTextBrush;
		_partEditorHost = new Border
		{
			Background = SurfaceBackgroundBrush,
			BorderBrush = PanelBorderBrush,
			BorderThickness = new Thickness(1.0),
			Padding = new Thickness(5.0)
		};
		_partListBox = BuildPartListBox();
		_favouriteComboBox = BuildFavouriteComboBox();
		base.Content = DialogAppearance.WithChrome(this, "Placement Attribute Overrides", BuildContent());
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
			FontSize = 10.0
		};
		grid.Children.Add(element3);
		Grid.SetRow(element3, 1);
		Grid grid2 = new Grid();
		grid2.VerticalAlignment = VerticalAlignment.Stretch;
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = ((EditorModel.Parts.Count > 1) ? new GridLength(136.0) : new GridLength(0.0))
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(10.0)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		if (EditorModel.Parts.Count > 1)
		{
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
				FontSize = 9.0,
				FontWeight = FontWeights.SemiBold
			});
			Border element4 = new Border
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
			grid3.Children.Add(element4);
			Grid.SetRow(element4, 1);
			grid2.Children.Add(grid3);
			Grid.SetColumn(grid3, 0);
		}
		grid2.Children.Add(_partEditorHost);
		Grid.SetColumn(_partEditorHost, 2);
		grid.Children.Add(grid2);
		Grid.SetRow(grid2, 2);
		dockPanel.Children.Add(grid);
		return dockPanel;
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
		StackPanel stackPanel = new StackPanel();
		stackPanel.Children.Add(new TextBlock
		{
			Text = part.DisplayName,
			FontWeight = FontWeights.SemiBold,
			TextWrapping = TextWrapping.Wrap,
			MaxWidth = 106.0,
			FontSize = 9.5
		});
		return stackPanel;
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
				FontSize = 9.0
			};
			grid.Children.Add(element2);
			Grid.SetRow(element2, 1);
		}
		ListBox listBox = new ListBox
		{
			Background = SurfaceBackgroundBrush,
			BorderThickness = new Thickness(0.0),
			Foreground = TextBrush,
			HorizontalContentAlignment = HorizontalAlignment.Stretch,
			ItemContainerStyle = CreateFieldListItemStyle()
		};
		((DependencyObject)listBox).SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, (object)ScrollBarVisibility.Auto);
		((DependencyObject)listBox).SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, (object)ScrollBarVisibility.Disabled);
		((DependencyObject)listBox).SetValue(ScrollViewer.CanContentScrollProperty, (object)true);
		listBox.PreviewMouseWheel += ForwardFieldListMouseWheel;
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
			FontSize = 10.5
		});
		stackPanel.Children.Add(new TextBlock
		{
			Text = field.FieldName + "  |  " + field.ConfiguredValueSummary.Replace("Configured default: ", "Default: "),
			Foreground = SecondaryTextBrush,
			Margin = new Thickness(0.0, 1.0, 0.0, 0.0),
			FontSize = 8.5,
			TextWrapping = TextWrapping.Wrap
		});
		grid.Children.Add(stackPanel);
		Button button = new Button
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
				Height = 22.0,
				VerticalAlignment = VerticalAlignment.Center,
				Padding = new Thickness(6.0, 1.0, 6.0, 1.0),
				Style = CreateComboBoxStyle(),
				ItemContainerStyle = CreateComboBoxItemStyle()
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
		Grid grid = new Grid();
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
			FontSize = 10.0
		});
		stackPanel.Children.Add(_favouriteComboBox);
		Button button = new Button
		{
			Content = "Apply",
			MinWidth = 52.0,
			Height = 24.0,
			Margin = new Thickness(0.0, 0.0, 6.0, 0.0),
			Style = CreateButtonStyle()
		};
		button.Click += ApplyFavouriteButton_Click;
		stackPanel.Children.Add(button);
		Button button2 = new Button
		{
			Content = "Save",
			MinWidth = 52.0,
			Height = 24.0,
			Margin = new Thickness(0.0, 0.0, 6.0, 0.0),
			Style = CreateButtonStyle()
		};
		button2.Click += SaveFavouriteButton_Click;
		stackPanel.Children.Add(button2);
		Button button3 = new Button
		{
			Content = "Delete",
			MinWidth = 56.0,
			Height = 24.0,
			Style = CreateSubtleButtonStyle()
		};
		button3.Click += DeleteFavouriteButton_Click;
		stackPanel.Children.Add(button3);
		grid.Children.Add(stackPanel);
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		Button element = new Button
		{
			Content = "Cancel",
			Width = 82.0,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
			IsCancel = true,
			Height = 28.0,
			Style = CreateButtonStyle()
		};
		Button button4 = new Button
		{
			Content = "Use These Values",
			Width = 122.0,
			IsDefault = true,
			Height = 28.0,
			Background = AccentBrush,
			Foreground = Brushes.White,
			Style = CreatePrimaryButtonStyle()
		};
		button4.Click += SaveButton_Click;
		stackPanel2.Children.Add(element);
		stackPanel2.Children.Add(button4);
		grid.Children.Add(stackPanel2);
		Grid.SetColumn(stackPanel2, 1);
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

	private void ForwardPartListMouseWheel(object sender, MouseWheelEventArgs e)
	{
		ScrollViewer scrollViewer = FindDescendantScrollViewer((DependencyObject)(object)_partListBox);
		if (scrollViewer != null)
		{
			scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - (double)e.Delta);
			e.Handled = true;
		}
	}

	private void ForwardFieldListMouseWheel(object sender, MouseWheelEventArgs e)
	{
		ScrollViewer scrollViewer = FindDescendantScrollViewer((DependencyObject)(object)_fieldListBox);
		if (scrollViewer != null)
		{
			scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - (double)e.Delta);
			e.Handled = true;
		}
	}

	private void ForwardFieldEditorMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (!(sender is ComboBox { IsDropDownOpen: not false }))
		{
			ScrollViewer scrollViewer = FindDescendantScrollViewer((DependencyObject)(object)_fieldListBox);
			if (scrollViewer != null)
			{
				scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - (double)e.Delta);
				e.Handled = true;
			}
		}
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

	private static Style CreateTextBoxStyle()
	{
		Style style = new Style(typeof(TextBox));
		style.Setters.Add(new Setter(Control.BackgroundProperty, EditorBackgroundBrush));
		style.Setters.Add(new Setter(Control.ForegroundProperty, TextBrush));
		style.Setters.Add(new Setter(Control.BorderBrushProperty, PanelBorderBrush));
		style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1.0)));
		style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
		Trigger trigger = new Trigger
		{
			Property = UIElement.IsKeyboardFocusedProperty,
			Value = true
		};
		trigger.Setters.Add(new Setter(Control.BorderBrushProperty, AccentBrush));
		style.Triggers.Add(trigger);
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
		Trigger trigger = new Trigger
		{
			Property = UIElement.IsKeyboardFocusedProperty,
			Value = true
		};
		trigger.Setters.Add(new Setter(Control.BorderBrushProperty, AccentBrush));
		style.Triggers.Add(trigger);
		return style;
	}

	private static ControlTemplate CreateComboBoxTemplate()
	{
		ControlTemplate controlTemplate = new ControlTemplate(typeof(ComboBox));
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Grid));
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(ToggleButton));
		frameworkElementFactory2.SetValue(FrameworkElement.FocusVisualStyleProperty, null);
		frameworkElementFactory2.SetValue(ButtonBase.ClickModeProperty, ClickMode.Press);
		frameworkElementFactory2.SetBinding(Control.BackgroundProperty, new Binding("Background")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		frameworkElementFactory2.SetBinding(Control.BorderBrushProperty, new Binding("BorderBrush")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		frameworkElementFactory2.SetBinding(Control.BorderThicknessProperty, new Binding("BorderThickness")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		frameworkElementFactory2.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsDropDownOpen")
		{
			RelativeSource = RelativeSource.TemplatedParent,
			Mode = BindingMode.TwoWay
		});
		frameworkElementFactory2.SetBinding(ContentControl.ContentProperty, new Binding("SelectionBoxItem")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		frameworkElementFactory2.SetBinding(Control.PaddingProperty, new Binding("Padding")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		frameworkElementFactory2.SetValue(Control.TemplateProperty, CreateComboBoxToggleTemplate());
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		FrameworkElementFactory frameworkElementFactory3 = new FrameworkElementFactory(typeof(Popup));
		frameworkElementFactory3.Name = "PART_Popup";
		frameworkElementFactory3.SetValue(Popup.PlacementProperty, PlacementMode.Bottom);
		frameworkElementFactory3.SetValue(Popup.AllowsTransparencyProperty, true);
		frameworkElementFactory3.SetValue(UIElement.FocusableProperty, false);
		frameworkElementFactory3.SetBinding(Popup.IsOpenProperty, new Binding("IsDropDownOpen")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		FrameworkElementFactory frameworkElementFactory4 = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory4.SetValue(Border.BackgroundProperty, EditorBackgroundBrush);
		frameworkElementFactory4.SetValue(Border.BorderBrushProperty, PanelBorderBrush);
		frameworkElementFactory4.SetValue(Border.BorderThicknessProperty, new Thickness(1.0));
		frameworkElementFactory4.SetBinding(FrameworkElement.MinWidthProperty, new Binding("ActualWidth")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		FrameworkElementFactory frameworkElementFactory5 = new FrameworkElementFactory(typeof(ScrollViewer));
		frameworkElementFactory5.SetValue(ScrollViewer.CanContentScrollProperty, true);
		frameworkElementFactory5.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
		FrameworkElementFactory child = new FrameworkElementFactory(typeof(ItemsPresenter));
		frameworkElementFactory5.AppendChild(child);
		frameworkElementFactory4.AppendChild(frameworkElementFactory5);
		frameworkElementFactory3.AppendChild(frameworkElementFactory4);
		frameworkElementFactory.AppendChild(frameworkElementFactory3);
		controlTemplate.VisualTree = frameworkElementFactory;
		return controlTemplate;
	}

	private static ControlTemplate CreateComboBoxToggleTemplate()
	{
		ControlTemplate controlTemplate = new ControlTemplate(typeof(ToggleButton));
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
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(Grid));
		frameworkElementFactory2.SetValue(FrameworkElement.MinHeightProperty, 22.0);
		FrameworkElementFactory frameworkElementFactory3 = new FrameworkElementFactory(typeof(ContentPresenter));
		frameworkElementFactory3.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		frameworkElementFactory3.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
		frameworkElementFactory3.SetBinding(ContentPresenter.ContentProperty, new Binding("Content")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		frameworkElementFactory3.SetValue(FrameworkElement.MarginProperty, new Thickness(6.0, 0.0, 24.0, 0.0));
		frameworkElementFactory3.SetValue(TextElement.ForegroundProperty, TextBrush);
		frameworkElementFactory2.AppendChild(frameworkElementFactory3);
		FrameworkElementFactory frameworkElementFactory4 = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory4.SetValue(Border.BackgroundProperty, PanelBorderBrush);
		frameworkElementFactory4.SetValue(FrameworkElement.WidthProperty, 1.0);
		frameworkElementFactory4.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
		frameworkElementFactory4.SetValue(FrameworkElement.MarginProperty, new Thickness(0.0, 4.0, 20.0, 4.0));
		frameworkElementFactory2.AppendChild(frameworkElementFactory4);
		FrameworkElementFactory frameworkElementFactory5 = new FrameworkElementFactory(typeof(TextBlock));
		frameworkElementFactory5.SetValue(TextBlock.TextProperty, "▾");
		frameworkElementFactory5.SetValue(TextBlock.ForegroundProperty, ComboGlyphBrush);
		frameworkElementFactory5.SetValue(TextBlock.FontSizeProperty, 10.0);
		frameworkElementFactory5.SetValue(FrameworkElement.MarginProperty, new Thickness(0.0, 0.0, 7.0, 0.0));
		frameworkElementFactory5.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
		frameworkElementFactory5.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		frameworkElementFactory2.AppendChild(frameworkElementFactory5);
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		controlTemplate.VisualTree = frameworkElementFactory;
		return controlTemplate;
	}

	private static Style CreateComboBoxItemStyle()
	{
		Style style = new Style(typeof(ComboBoxItem));
		style.Setters.Add(new Setter(Control.BackgroundProperty, EditorBackgroundBrush));
		style.Setters.Add(new Setter(Control.ForegroundProperty, TextBrush));
		style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6.0, 2.0, 6.0, 2.0)));
		Trigger trigger = new Trigger
		{
			Property = UIElement.IsMouseOverProperty,
			Value = true
		};
		trigger.Setters.Add(new Setter(Control.BackgroundProperty, HoverBrush));
		trigger.Setters.Add(new Setter(Control.ForegroundProperty, TextBrush));
		style.Triggers.Add(trigger);
		Trigger trigger2 = new Trigger
		{
			Property = ListBoxItem.IsSelectedProperty,
			Value = true
		};
		trigger2.Setters.Add(new Setter(Control.BackgroundProperty, AccentBrush));
		trigger2.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
		style.Triggers.Add(trigger2);
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
		Trigger trigger = new Trigger
		{
			Property = UIElement.IsMouseOverProperty,
			Value = true
		};
		trigger.Setters.Add(new Setter(Control.BackgroundProperty, HoverBrush));
		trigger.Setters.Add(new Setter(Control.BorderBrushProperty, AccentBrush));
		style.Triggers.Add(trigger);
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
		Trigger trigger = new Trigger
		{
			Property = UIElement.IsMouseOverProperty,
			Value = true
		};
		trigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(32, 128, 224))));
		trigger.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(32, 128, 224))));
		style.Triggers.Add(trigger);
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
		style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0.0, 0.0, 0.0, 1.0)));
		style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
		Trigger trigger = new Trigger
		{
			Property = ListBoxItem.IsSelectedProperty,
			Value = true
		};
		trigger.Setters.Add(new Setter(Control.BackgroundProperty, SelectedBrush));
		trigger.Setters.Add(new Setter(Control.ForegroundProperty, TextBrush));
		style.Triggers.Add(trigger);
		Trigger trigger2 = new Trigger
		{
			Property = UIElement.IsMouseOverProperty,
			Value = true
		};
		trigger2.Setters.Add(new Setter(Control.BackgroundProperty, HoverBrush));
		style.Triggers.Add(trigger2);
		return style;
	}

	private static Style CreateFieldListItemStyle()
	{
		Style style = new Style(typeof(ListBoxItem));
		style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
		style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0.0)));
		style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0.0)));
		style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0.0)));
		style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
		style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
		Trigger trigger = new Trigger
		{
			Property = ListBoxItem.IsSelectedProperty,
			Value = true
		};
		trigger.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
		trigger.Setters.Add(new Setter(Control.ForegroundProperty, TextBrush));
		style.Triggers.Add(trigger);
		return style;
	}

	static PlacementAttributeOverrideWindow()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Invalid comparison between Unknown and I4
		IsDarkTheme = (int)FrameworkApplication.ApplicationTheme == 1;
		WindowBackgroundBrush = (IsDarkTheme ? new SolidColorBrush(Color.FromRgb(45, 45, 48)) : new SolidColorBrush(Color.FromRgb(243, 243, 243)));
		SurfaceBackgroundBrush = (IsDarkTheme ? new SolidColorBrush(Color.FromRgb(31, 31, 31)) : Brushes.White);
		PanelBorderBrush = (IsDarkTheme ? new SolidColorBrush(Color.FromRgb(72, 72, 72)) : new SolidColorBrush(Color.FromRgb(208, 208, 208)));
		TextBrush = (IsDarkTheme ? new SolidColorBrush(Color.FromRgb(238, 238, 238)) : new SolidColorBrush(Color.FromRgb(32, 32, 32)));
		SecondaryTextBrush = (IsDarkTheme ? new SolidColorBrush(Color.FromRgb(205, 205, 205)) : new SolidColorBrush(Color.FromRgb(96, 96, 96)));
		AccentBrush = new SolidColorBrush(Color.FromRgb(51, 153, byte.MaxValue));
		HoverBrush = (IsDarkTheme ? new SolidColorBrush(Color.FromRgb(58, 58, 62)) : new SolidColorBrush(Color.FromRgb(238, 244, 250)));
		EditorBackgroundBrush = (IsDarkTheme ? new SolidColorBrush(Color.FromRgb(36, 36, 38)) : Brushes.White);
		SelectedBrush = (IsDarkTheme ? new SolidColorBrush(Color.FromRgb(35, 82, 130)) : new SolidColorBrush(Color.FromRgb(214, 234, 252)));
		ComboGlyphBrush = (IsDarkTheme ? new SolidColorBrush(Color.FromRgb(192, 192, 192)) : new SolidColorBrush(Color.FromRgb(90, 90, 90)));
	}
}
