using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TemplateEditor;

internal sealed class TemplateSettingsWindow : Window
{
	private static readonly Brush WindowBackgroundBrush = new SolidColorBrush(Color.FromRgb(243, 243, 243));

	private static readonly Brush SurfaceBackgroundBrush = Brushes.White;

	private static readonly Brush SectionBorderBrush = new SolidColorBrush(Color.FromRgb(208, 208, 208));

	private static readonly Brush TabBackgroundBrush = new SolidColorBrush(Color.FromRgb(232, 232, 232));

	private static readonly Brush TabHoverBackgroundBrush = new SolidColorBrush(Color.FromRgb(222, 234, 246));

	private static readonly Brush TabSelectedBackgroundBrush = Brushes.White;

	private static readonly Brush PrimaryTextBrush = new SolidColorBrush(Color.FromRgb(32, 32, 32));

	private static readonly Brush AccentBorderBrush = new SolidColorBrush(Color.FromRgb(120, 160, 200));

	private readonly TextBox _templateConfigPathTextBox;

	private readonly CheckBox _validateConfigCheckBox;

	private readonly CheckBox _enableLineSplitPromptsCheckBox;

	private readonly CheckBox _enablePointPlacementSplitPromptCheckBox;

	private readonly CheckBox _enableLineEndpointSplitPromptCheckBox;

	private readonly CheckBox _enableParallelCopyPromptCheckBox;

	private readonly CheckBox _enableSplitAtLineStartPointCheckBox;

	private readonly CheckBox _enableSplitAtLineEndPointCheckBox;

	private readonly TextBox _splitSearchDistanceTextBox;

	private readonly TextBox _splitPointPlacementGroupsTextBox;

	private readonly TextBox _splitLinePlacementGroupsTextBox;

	private readonly TextBox _splitTargetLineGroupsTextBox;

	private readonly CheckBox _enableAssociationPromptsCheckBox;

	private readonly CheckBox _enableStructuralAttachmentPromptsCheckBox;

	private readonly CheckBox _enableContainmentPointPromptsCheckBox;

	private readonly CheckBox _enableContainmentBoundaryPromptsCheckBox;

	private readonly CheckBox _highlightAssociationCandidatesCheckBox;

	private readonly TextBox _associationSearchDistanceTextBox;

	private readonly TextBox _associationPlacementGroupsTextBox;

	private readonly TextBox _structuralAttachmentTargetGroupsTextBox;

	private readonly TextBox _containmentPointTargetGroupsTextBox;

	private readonly TextBox _containmentBoundaryTargetGroupsTextBox;

	public TemplateEditorSettings Settings { get; }

	public TemplateSettingsWindow(TemplateEditorSettings settings)
	{
		Settings = settings.Clone();
		Title = "Template Settings";
		Width = 760.0;
		Height = 640.0;
		MinWidth = 700.0;
		MinHeight = 560.0;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		ResizeMode = ResizeMode.CanResize;
		Background = WindowBackgroundBrush;
		Foreground = PrimaryTextBrush;
		FontFamily = new FontFamily("Segoe UI");
		FontSize = 12.0;
		_templateConfigPathTextBox = CreateTextBox(Settings.TemplateConfigFilePath ?? string.Empty);
		_validateConfigCheckBox = CreateCheckBox("Validate template configuration before opening the editor", Settings.ValidateConfig);
		_enableLineSplitPromptsCheckBox = CreateCheckBox("Enable line split prompts", Settings.EnableLineSplitPrompts);
		_enablePointPlacementSplitPromptCheckBox = CreateCheckBox("Prompt when eligible point features are placed on lines", Settings.EnablePointPlacementSplitPrompt);
		_enableLineEndpointSplitPromptCheckBox = CreateCheckBox("Prompt when eligible line feature endpoints land on lines", Settings.EnableLineEndpointSplitPrompt);
		_enableParallelCopyPromptCheckBox = CreateCheckBox("Prompt to create a parallel copy from a selected line", Settings.EnableParallelCopyPrompt);
		_enableSplitAtLineStartPointCheckBox = CreateCheckBox("Allow split prompts at line start points", Settings.EnableSplitAtLineStartPoint);
		_enableSplitAtLineEndPointCheckBox = CreateCheckBox("Allow split prompts at line end points", Settings.EnableSplitAtLineEndPoint);
		_splitSearchDistanceTextBox = CreateTextBox(Settings.SplitSearchDistance.ToString("0.###"));
		_splitPointPlacementGroupsTextBox = CreateTextBox(TemplateEditorSettings.FormatGroupNames(Settings.SplitPointPlacementGroups));
		_splitLinePlacementGroupsTextBox = CreateTextBox(TemplateEditorSettings.FormatGroupNames(Settings.SplitLinePlacementGroups));
		_splitTargetLineGroupsTextBox = CreateTextBox(TemplateEditorSettings.FormatGroupNames(Settings.SplitTargetLineGroups));
		_enableAssociationPromptsCheckBox = CreateCheckBox("Enable automatic association prompts", Settings.EnableAssociationPrompts);
		_enableStructuralAttachmentPromptsCheckBox = CreateCheckBox("Allow structural attachment prompts", Settings.EnableStructuralAttachmentPrompts);
		_enableContainmentPointPromptsCheckBox = CreateCheckBox("Allow containment prompts for structure points", Settings.EnableContainmentPointPrompts);
		_enableContainmentBoundaryPromptsCheckBox = CreateCheckBox("Allow containment prompts for structure boundaries", Settings.EnableContainmentBoundaryPrompts);
		_highlightAssociationCandidatesCheckBox = CreateCheckBox("Highlight association candidates on the map", Settings.HighlightAssociationCandidates);
		_associationSearchDistanceTextBox = CreateTextBox(Settings.AssociationSearchDistance.ToString("0.###"));
		_associationPlacementGroupsTextBox = CreateTextBox(TemplateEditorSettings.FormatGroupNames(Settings.AssociationPlacementGroups));
		_structuralAttachmentTargetGroupsTextBox = CreateTextBox(TemplateEditorSettings.FormatGroupNames(Settings.StructuralAttachmentTargetGroups));
		_containmentPointTargetGroupsTextBox = CreateTextBox(TemplateEditorSettings.FormatGroupNames(Settings.ContainmentPointTargetGroups));
		_containmentBoundaryTargetGroupsTextBox = CreateTextBox(TemplateEditorSettings.FormatGroupNames(Settings.ContainmentBoundaryTargetGroups));
		Content = BuildContent();
	}

	private UIElement BuildContent()
	{
		DockPanel root = new DockPanel
		{
			LastChildFill = true
		};
		Border header = new Border
		{
			Background = WindowBackgroundBrush,
			BorderBrush = SectionBorderBrush,
			BorderThickness = new Thickness(0.0, 0.0, 0.0, 1.0),
			Padding = new Thickness(16.0, 12.0, 16.0, 12.0),
			Child = new TextBlock
			{
				Text = "Choose the template config file and placement enhancement options.",
				Foreground = PrimaryTextBrush
			}
		};
		DockPanel.SetDock(header, Dock.Top);
		root.Children.Add(header);
		Border footer = new Border
		{
			Background = WindowBackgroundBrush,
			BorderBrush = SectionBorderBrush,
			BorderThickness = new Thickness(0.0, 1.0, 0.0, 0.0),
			Padding = new Thickness(12.0),
			Child = BuildButtonsRow()
		};
		DockPanel.SetDock(footer, Dock.Bottom);
		root.Children.Add(footer);
		TabControl tabControl = new TabControl
		{
			Margin = new Thickness(12.0),
			Background = WindowBackgroundBrush,
			BorderBrush = SectionBorderBrush
		};
		tabControl.Resources[typeof(TabItem)] = CreateTabItemStyle();
		tabControl.Items.Add(new TabItem
		{
			Header = "General",
			Content = BuildGeneralTab()
		});
		tabControl.Items.Add(new TabItem
		{
			Header = "Line Split",
			Content = BuildLineSplitTab()
		});
		tabControl.Items.Add(new TabItem
		{
			Header = "Associations",
			Content = BuildAssociationTab()
		});
		root.Children.Add(tabControl);
		return root;
	}

	private UIElement BuildGeneralTab()
	{
		StackPanel panel = CreateTabPanel();
		panel.Children.Add(CreateGroupBox("Template Configuration", BuildTemplateConfigSection()));
		return WrapTab(panel);
	}

	private UIElement BuildLineSplitTab()
	{
		StackPanel panel = CreateTabPanel();
		panel.Children.Add(CreateGroupBox("Behavior", CreateCheckBoxPanel(_enableLineSplitPromptsCheckBox, _enablePointPlacementSplitPromptCheckBox, _enableLineEndpointSplitPromptCheckBox, _enableParallelCopyPromptCheckBox, _enableSplitAtLineStartPointCheckBox, _enableSplitAtLineEndPointCheckBox)));
		panel.Children.Add(CreateGroupBox("Eligible Groups", CreateFormGrid(("Split search distance (map units)", _splitSearchDistanceTextBox), ("Eligible point placement groups", _splitPointPlacementGroupsTextBox), ("Eligible line placement groups", _splitLinePlacementGroupsTextBox), ("Underlying target line groups", _splitTargetLineGroupsTextBox))));
		return WrapTab(panel);
	}

	private UIElement BuildAssociationTab()
	{
		StackPanel panel = CreateTabPanel();
		panel.Children.Add(CreateGroupBox("Behavior", CreateCheckBoxPanel(_enableAssociationPromptsCheckBox, _enableStructuralAttachmentPromptsCheckBox, _enableContainmentPointPromptsCheckBox, _enableContainmentBoundaryPromptsCheckBox, _highlightAssociationCandidatesCheckBox)));
		panel.Children.Add(CreateGroupBox("Eligible Groups", CreateFormGrid(("Association search distance (map units)", _associationSearchDistanceTextBox), ("Eligible placement groups", _associationPlacementGroupsTextBox), ("Structural attachment target groups", _structuralAttachmentTargetGroupsTextBox), ("Containment target point groups", _containmentPointTargetGroupsTextBox), ("Containment target boundary groups", _containmentBoundaryTargetGroupsTextBox))));
		return WrapTab(panel);
	}

	private UIElement BuildTemplateConfigSection()
	{
		Grid grid = new Grid
		{
			Margin = new Thickness(0.0, 4.0, 0.0, 0.0)
		};
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		TextBlock label = CreateLabel("Template config file");
		Grid.SetColumnSpan(label, 2);
		grid.Children.Add(label);
		Grid.SetRow(_templateConfigPathTextBox, 1);
		grid.Children.Add(_templateConfigPathTextBox);
		Button browseButton = new Button
		{
			Content = "Browse...",
			MinWidth = 90.0,
			Margin = new Thickness(8.0, 22.0, 0.0, 0.0),
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0)
		};
		browseButton.Click += delegate
		{
			string selectedPath = AddinConfiguration.PromptForTemplateConfigFilePath(_templateConfigPathTextBox.Text);
			if (!string.IsNullOrWhiteSpace(selectedPath))
			{
				_templateConfigPathTextBox.Text = selectedPath;
			}
		};
		Grid.SetRow(browseButton, 1);
		Grid.SetColumn(browseButton, 1);
		grid.Children.Add(browseButton);
		StackPanel section = new StackPanel();
		section.Children.Add(grid);
		section.Children.Add(_validateConfigCheckBox);
		return section;
	}

	private static StackPanel CreateCheckBoxPanel(params CheckBox[] checkBoxes)
	{
		StackPanel panel = new StackPanel
		{
			Margin = new Thickness(0.0, 4.0, 0.0, 0.0)
		};
		foreach (CheckBox checkBox in checkBoxes)
		{
			panel.Children.Add(checkBox);
		}
		return panel;
	}

	private static Grid CreateFormGrid(params (string Label, Control Control)[] rows)
	{
		Grid grid = new Grid
		{
			Margin = new Thickness(0.0, 4.0, 0.0, 0.0)
		};
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(240.0)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		for (int i = 0; i < rows.Length; i++)
		{
			grid.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			TextBlock label = CreateLabel(rows[i].Label);
			label.VerticalAlignment = VerticalAlignment.Center;
			label.Margin = new Thickness(0.0, 0.0, 12.0, 8.0);
			Grid.SetRow(label, i);
			grid.Children.Add(label);
			Control control = rows[i].Control;
			control.Margin = new Thickness(0.0, 0.0, 0.0, 8.0);
			Grid.SetRow(control, i);
			Grid.SetColumn(control, 1);
			grid.Children.Add(control);
		}
		return grid;
	}

	private static GroupBox CreateGroupBox(string header, UIElement content)
	{
		DataTemplate headerTemplate = new DataTemplate();
		FrameworkElementFactory headerText = new FrameworkElementFactory(typeof(TextBlock));
		headerText.SetValue(TextBlock.ForegroundProperty, PrimaryTextBrush);
		headerText.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
		headerText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding());
		headerTemplate.VisualTree = headerText;
		return new GroupBox
		{
			Header = header,
			HeaderTemplate = headerTemplate,
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0),
			Padding = new Thickness(12.0, 8.0, 12.0, 12.0),
			Background = SurfaceBackgroundBrush,
			BorderBrush = SectionBorderBrush,
			Foreground = PrimaryTextBrush,
			Content = content
		};
	}

	private static StackPanel CreateTabPanel()
	{
		return new StackPanel
		{
			Margin = new Thickness(4.0)
		};
	}

	private static ScrollViewer WrapTab(UIElement content)
	{
		return new ScrollViewer
		{
			Content = content,
			Background = WindowBackgroundBrush,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
		};
	}

	private UIElement BuildButtonsRow()
	{
		StackPanel buttons = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		Button cancelButton = new Button
		{
			Content = "Cancel",
			MinWidth = 88.0,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0),
			IsCancel = true
		};
		cancelButton.Click += delegate
		{
			DialogResult = false;
			Close();
		};
		Button saveButton = new Button
		{
			Content = "OK",
			MinWidth = 88.0,
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0),
			IsDefault = true
		};
		saveButton.Click += SaveButton_Click;
		buttons.Children.Add(cancelButton);
		buttons.Children.Add(saveButton);
		return buttons;
	}

	private void SaveButton_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			Settings.TemplateConfigFilePath = string.IsNullOrWhiteSpace(_templateConfigPathTextBox.Text) ? null : _templateConfigPathTextBox.Text.Trim();
			if (!string.IsNullOrWhiteSpace(Settings.TemplateConfigFilePath) && !System.IO.File.Exists(Settings.TemplateConfigFilePath))
			{
				throw new InvalidOperationException("The selected template configuration file could not be found.");
			}
			Settings.ValidateConfig = _validateConfigCheckBox.IsChecked == true;
			Settings.EnableLineSplitPrompts = _enableLineSplitPromptsCheckBox.IsChecked == true;
			Settings.EnablePointPlacementSplitPrompt = _enablePointPlacementSplitPromptCheckBox.IsChecked == true;
			Settings.EnableLineEndpointSplitPrompt = _enableLineEndpointSplitPromptCheckBox.IsChecked == true;
			Settings.EnableParallelCopyPrompt = _enableParallelCopyPromptCheckBox.IsChecked == true;
			Settings.EnableSplitAtLineStartPoint = _enableSplitAtLineStartPointCheckBox.IsChecked == true;
			Settings.EnableSplitAtLineEndPoint = _enableSplitAtLineEndPointCheckBox.IsChecked == true;
			Settings.SplitSearchDistance = ParseDistance(_splitSearchDistanceTextBox.Text, "split search distance");
			Settings.SplitPointPlacementGroups = TemplateEditorSettings.ParseGroupNames(_splitPointPlacementGroupsTextBox.Text);
			Settings.SplitLinePlacementGroups = TemplateEditorSettings.ParseGroupNames(_splitLinePlacementGroupsTextBox.Text);
			Settings.SplitTargetLineGroups = TemplateEditorSettings.ParseGroupNames(_splitTargetLineGroupsTextBox.Text);
			Settings.EnableAssociationPrompts = _enableAssociationPromptsCheckBox.IsChecked == true;
			Settings.EnableStructuralAttachmentPrompts = _enableStructuralAttachmentPromptsCheckBox.IsChecked == true;
			Settings.EnableContainmentPointPrompts = _enableContainmentPointPromptsCheckBox.IsChecked == true;
			Settings.EnableContainmentBoundaryPrompts = _enableContainmentBoundaryPromptsCheckBox.IsChecked == true;
			Settings.HighlightAssociationCandidates = _highlightAssociationCandidatesCheckBox.IsChecked == true;
			Settings.AssociationSearchDistance = ParseDistance(_associationSearchDistanceTextBox.Text, "association search distance");
			Settings.AssociationPlacementGroups = TemplateEditorSettings.ParseGroupNames(_associationPlacementGroupsTextBox.Text);
			Settings.StructuralAttachmentTargetGroups = TemplateEditorSettings.ParseGroupNames(_structuralAttachmentTargetGroupsTextBox.Text);
			Settings.ContainmentPointTargetGroups = TemplateEditorSettings.ParseGroupNames(_containmentPointTargetGroupsTextBox.Text);
			Settings.ContainmentBoundaryTargetGroups = TemplateEditorSettings.ParseGroupNames(_containmentBoundaryTargetGroupsTextBox.Text);
			Settings.Normalize();
			DialogResult = true;
			Close();
		}
		catch (Exception ex)
		{
			DialogService.Show(ex.Message, "Template Settings");
		}
	}

	private static double ParseDistance(string text, string label)
	{
		if (!double.TryParse(text, out double result) || result < 0.0)
		{
			throw new InvalidOperationException("Enter a valid non-negative number for " + label + ".");
		}
		return result;
	}

	private static TextBox CreateTextBox(string text)
	{
		return new TextBox
		{
			Text = text ?? string.Empty,
			MinWidth = 180.0,
			Padding = new Thickness(6.0, 4.0, 6.0, 4.0),
			Background = SurfaceBackgroundBrush,
			Foreground = PrimaryTextBrush,
			BorderBrush = SectionBorderBrush,
			CaretBrush = PrimaryTextBrush,
			SelectionBrush = new SolidColorBrush(Color.FromRgb(204, 228, 247)),
			Style = CreateTextBoxStyle()
		};
	}

	private static CheckBox CreateCheckBox(string content, bool value)
	{
		return new CheckBox
		{
			Content = content,
			IsChecked = value,
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0),
			Foreground = PrimaryTextBrush
		};
	}

	private static TextBlock CreateLabel(string text)
	{
		return new TextBlock
		{
			Text = text,
			Foreground = PrimaryTextBrush,
			Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
		};
	}

	private static Style CreateTabItemStyle()
	{
		Style style = new Style(typeof(TabItem));
		style.Setters.Add(new Setter(Control.BackgroundProperty, TabBackgroundBrush));
		style.Setters.Add(new Setter(Control.ForegroundProperty, PrimaryTextBrush));
		style.Setters.Add(new Setter(Control.BorderBrushProperty, SectionBorderBrush));
		style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(14.0, 6.0, 14.0, 6.0)));
		Trigger selectedTrigger = new Trigger
		{
			Property = TabItem.IsSelectedProperty,
			Value = true
		};
		selectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, TabSelectedBackgroundBrush));
		selectedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, PrimaryTextBrush));
		style.Triggers.Add(selectedTrigger);
		Trigger hoverTrigger = new Trigger
		{
			Property = UIElement.IsMouseOverProperty,
			Value = true
		};
		hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, TabHoverBackgroundBrush));
		hoverTrigger.Setters.Add(new Setter(Control.ForegroundProperty, PrimaryTextBrush));
		style.Triggers.Add(hoverTrigger);
		return style;
	}

	private static Style CreateTextBoxStyle()
	{
		Style style = new Style(typeof(TextBox));
		style.Setters.Add(new Setter(Control.BackgroundProperty, SurfaceBackgroundBrush));
		style.Setters.Add(new Setter(Control.ForegroundProperty, PrimaryTextBrush));
		style.Setters.Add(new Setter(Control.BorderBrushProperty, SectionBorderBrush));
		style.Triggers.Add(new Trigger
		{
			Property = UIElement.IsMouseOverProperty,
			Value = true,
			Setters =
			{
				new Setter(Control.BackgroundProperty, SurfaceBackgroundBrush),
				new Setter(Control.ForegroundProperty, PrimaryTextBrush),
				new Setter(Control.BorderBrushProperty, AccentBorderBrush)
			}
		});
		style.Triggers.Add(new Trigger
		{
			Property = UIElement.IsKeyboardFocusedProperty,
			Value = true,
			Setters =
			{
				new Setter(Control.BackgroundProperty, SurfaceBackgroundBrush),
				new Setter(Control.ForegroundProperty, PrimaryTextBrush),
				new Setter(Control.BorderBrushProperty, AccentBorderBrush)
			}
		});
		return style;
	}
}
