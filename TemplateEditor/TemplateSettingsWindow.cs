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

	private static readonly Brush ControlBorderBrush = new SolidColorBrush(Color.FromRgb(150, 150, 150));

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

	private readonly CheckBox _enableConfiguredLinePartSplitsCheckBox;

	private readonly CheckBox _suppressDuplicateSplitPromptsCheckBox;

	private readonly CheckBox _splitOnlyInteriorCandidatesCheckBox;

	private readonly TextBox _maxSplitCandidatesToReviewTextBox;

	private readonly ComboBox _splitPromptModeComboBox;

	private readonly TextBox _splitSearchDistanceTextBox;

	private readonly TextBox _splitPointPlacementGroupsTextBox;

	private readonly TextBox _splitLinePlacementGroupsTextBox;

	private readonly TextBox _splitTargetLineGroupsTextBox;

	private readonly TextBox _splitTargetLayerNamesTextBox;

	private readonly CheckBox _enableMultiSegmentParallelCopyCheckBox;

	private readonly CheckBox _requireConnectedParallelCopySpanCheckBox;

	private readonly TextBox _parallelCopyEndpointMatchToleranceTextBox;

	private readonly TextBox _defaultParallelCopyOffsetDistanceTextBox;

	private readonly CheckBox _defaultParallelCopyLeftSideCheckBox;

	private readonly CheckBox _rememberLastParallelCopyOptionsCheckBox;

	private readonly CheckBox _autoCreateParallelCopyWhenSelectedLineExistsCheckBox;

	private readonly CheckBox _enableAssociationPromptsCheckBox;

	private readonly CheckBox _enableStructuralAttachmentPromptsCheckBox;

	private readonly CheckBox _enableContainmentPointPromptsCheckBox;

	private readonly CheckBox _enableContainmentBoundaryPromptsCheckBox;

	private readonly CheckBox _enableLineAssociationPromptsCheckBox;

	private readonly CheckBox _enableLineStructuralAttachmentPromptsCheckBox;

	private readonly CheckBox _enableLineContainmentPointPromptsCheckBox;

	private readonly CheckBox _enableLineContainmentBoundaryPromptsCheckBox;

	private readonly ComboBox _associationPromptModeComboBox;

	private readonly CheckBox _stopAfterFirstSuccessfulAssociationCheckBox;

	private readonly CheckBox _highlightAssociationCandidatesCheckBox;

	private readonly CheckBox _highlightSplitCandidatesCheckBox;

	private readonly CheckBox _showAutomaticStepDiagnosticsCheckBox;

	private readonly TextBox _associationSearchDistanceTextBox;

	private readonly TextBox _structuralAttachmentSearchDistanceTextBox;

	private readonly TextBox _containmentPointSearchDistanceTextBox;

	private readonly TextBox _containmentBoundarySearchDistanceTextBox;

	private readonly TextBox _associationPlacementGroupsTextBox;

	private readonly TextBox _structuralAttachmentTargetGroupsTextBox;

	private readonly TextBox _structuralAttachmentTargetLayerNamesTextBox;

	private readonly TextBox _containmentPointTargetGroupsTextBox;

	private readonly TextBox _containmentPointTargetLayerNamesTextBox;

	private readonly TextBox _containmentBoundaryTargetGroupsTextBox;

	private readonly TextBox _containmentBoundaryTargetLayerNamesTextBox;

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
		Resources[SystemColors.ControlTextBrushKey] = PrimaryTextBrush;
		Resources[SystemColors.WindowTextBrushKey] = PrimaryTextBrush;
		Resources[SystemColors.GrayTextBrushKey] = new SolidColorBrush(Color.FromRgb(96, 96, 96));
		Resources[SystemColors.ControlBrushKey] = WindowBackgroundBrush;
		Resources[SystemColors.WindowBrushKey] = SurfaceBackgroundBrush;
		Resources[SystemColors.HighlightBrushKey] = TabHoverBackgroundBrush;
		Resources[SystemColors.HighlightTextBrushKey] = PrimaryTextBrush;
		_templateConfigPathTextBox = CreateTextBox(Settings.TemplateConfigFilePath ?? string.Empty);
		_validateConfigCheckBox = CreateCheckBox("Validate template configuration before opening the editor", Settings.ValidateConfig);
		_enableLineSplitPromptsCheckBox = CreateCheckBox("Enable line split prompts", Settings.EnableLineSplitPrompts);
		_enablePointPlacementSplitPromptCheckBox = CreateCheckBox("Prompt when eligible point features are placed on lines", Settings.EnablePointPlacementSplitPrompt);
		_enableLineEndpointSplitPromptCheckBox = CreateCheckBox("Prompt when eligible line feature endpoints land on lines", Settings.EnableLineEndpointSplitPrompt);
		_enableParallelCopyPromptCheckBox = CreateCheckBox("Prompt to create a parallel copy from a selected line", Settings.EnableParallelCopyPrompt);
		_enableSplitAtLineStartPointCheckBox = CreateCheckBox("Allow split prompts at line start points", Settings.EnableSplitAtLineStartPoint);
		_enableSplitAtLineEndPointCheckBox = CreateCheckBox("Allow split prompts at line end points", Settings.EnableSplitAtLineEndPoint);
		_enableConfiguredLinePartSplitsCheckBox = CreateCheckBox("Allow configured group-template line parts to split underlying lines", Settings.EnableConfiguredLinePartSplits);
		_suppressDuplicateSplitPromptsCheckBox = CreateCheckBox("Suppress duplicate split prompts at the same point", Settings.SuppressDuplicateSplitPrompts);
		_splitOnlyInteriorCandidatesCheckBox = CreateCheckBox("Split only when the candidate point is inside the underlying line", Settings.SplitOnlyInteriorCandidates);
		_maxSplitCandidatesToReviewTextBox = CreateTextBox(Settings.MaxSplitCandidatesToReview.ToString());
		_splitPromptModeComboBox = CreateComboBox(Settings.SplitPromptMode, ("Always ask", "AlwaysAsk"), ("Auto split when one candidate", "AutoWhenOne"), ("Never split", "Never"));
		_splitSearchDistanceTextBox = CreateTextBox(Settings.SplitSearchDistance.ToString("0.###"));
		_splitPointPlacementGroupsTextBox = CreateTextBox(TemplateEditorSettings.FormatGroupNames(Settings.SplitPointPlacementGroups));
		_splitLinePlacementGroupsTextBox = CreateTextBox(TemplateEditorSettings.FormatGroupNames(Settings.SplitLinePlacementGroups));
		_splitTargetLineGroupsTextBox = CreateTextBox(TemplateEditorSettings.FormatGroupNames(Settings.SplitTargetLineGroups));
		_splitTargetLayerNamesTextBox = CreateTextBox(TemplateEditorSettings.FormatGroupNames(Settings.SplitTargetLayerNames));
		_enableMultiSegmentParallelCopyCheckBox = CreateCheckBox("Enable multi-segment parallel copy", Settings.EnableMultiSegmentParallelCopy);
		_requireConnectedParallelCopySpanCheckBox = CreateCheckBox("Require selected line segments to form a connected span", Settings.RequireConnectedParallelCopySpan);
		_parallelCopyEndpointMatchToleranceTextBox = CreateTextBox(Settings.ParallelCopyEndpointMatchTolerance.ToString("0.###"));
		_defaultParallelCopyOffsetDistanceTextBox = CreateTextBox(Settings.DefaultParallelCopyOffsetDistance.ToString("0.###"));
		_defaultParallelCopyLeftSideCheckBox = CreateCheckBox("Default parallel copy side is left", Settings.DefaultParallelCopyLeftSide);
		_rememberLastParallelCopyOptionsCheckBox = CreateCheckBox("Remember last parallel copy distance and side", Settings.RememberLastParallelCopyOptions);
		_autoCreateParallelCopyWhenSelectedLineExistsCheckBox = CreateCheckBox("Auto-create parallel copy when selected lines exist", Settings.AutoCreateParallelCopyWhenSelectedLineExists);
		_enableAssociationPromptsCheckBox = CreateCheckBox("Enable automatic association prompts", Settings.EnableAssociationPrompts);
		_enableStructuralAttachmentPromptsCheckBox = CreateCheckBox("Allow structural attachment prompts", Settings.EnableStructuralAttachmentPrompts);
		_enableContainmentPointPromptsCheckBox = CreateCheckBox("Allow containment prompts for structure points", Settings.EnableContainmentPointPrompts);
		_enableContainmentBoundaryPromptsCheckBox = CreateCheckBox("Allow containment prompts for structure boundaries", Settings.EnableContainmentBoundaryPrompts);
		_enableLineAssociationPromptsCheckBox = CreateCheckBox("Allow association prompts for line features", Settings.EnableLineAssociationPrompts);
		_enableLineStructuralAttachmentPromptsCheckBox = CreateCheckBox("Allow line structural attachment prompts", Settings.EnableLineStructuralAttachmentPrompts);
		_enableLineContainmentPointPromptsCheckBox = CreateCheckBox("Allow line containment prompts for structure points", Settings.EnableLineContainmentPointPrompts);
		_enableLineContainmentBoundaryPromptsCheckBox = CreateCheckBox("Allow line containment prompts for structure boundaries", Settings.EnableLineContainmentBoundaryPrompts);
		_associationPromptModeComboBox = CreateComboBox(Settings.AssociationPromptMode, ("Always ask", "AlwaysAsk"), ("Auto-create when one candidate", "AutoWhenOne"), ("Review multiple only", "ReviewMultipleOnly"), ("Never create", "Never"));
		_stopAfterFirstSuccessfulAssociationCheckBox = CreateCheckBox("Stop association prompts after first successful association", Settings.StopAfterFirstSuccessfulAssociation);
		_highlightAssociationCandidatesCheckBox = CreateCheckBox("Highlight association candidates on the map", Settings.HighlightAssociationCandidates);
		_highlightSplitCandidatesCheckBox = CreateCheckBox("Highlight split candidates on the map", Settings.HighlightSplitCandidates);
		_showAutomaticStepDiagnosticsCheckBox = CreateCheckBox("Show diagnostics when automatic placement steps fail", Settings.ShowAutomaticStepDiagnostics);
		_associationSearchDistanceTextBox = CreateTextBox(Settings.AssociationSearchDistance.ToString("0.###"));
		_structuralAttachmentSearchDistanceTextBox = CreateTextBox(Settings.StructuralAttachmentSearchDistance.ToString("0.###"));
		_containmentPointSearchDistanceTextBox = CreateTextBox(Settings.ContainmentPointSearchDistance.ToString("0.###"));
		_containmentBoundarySearchDistanceTextBox = CreateTextBox(Settings.ContainmentBoundarySearchDistance.ToString("0.###"));
		_associationPlacementGroupsTextBox = CreateTextBox(TemplateEditorSettings.FormatGroupNames(Settings.AssociationPlacementGroups));
		_structuralAttachmentTargetGroupsTextBox = CreateTextBox(TemplateEditorSettings.FormatGroupNames(Settings.StructuralAttachmentTargetGroups));
		_structuralAttachmentTargetLayerNamesTextBox = CreateTextBox(TemplateEditorSettings.FormatGroupNames(Settings.StructuralAttachmentTargetLayerNames));
		_containmentPointTargetGroupsTextBox = CreateTextBox(TemplateEditorSettings.FormatGroupNames(Settings.ContainmentPointTargetGroups));
		_containmentPointTargetLayerNamesTextBox = CreateTextBox(TemplateEditorSettings.FormatGroupNames(Settings.ContainmentPointTargetLayerNames));
		_containmentBoundaryTargetGroupsTextBox = CreateTextBox(TemplateEditorSettings.FormatGroupNames(Settings.ContainmentBoundaryTargetGroups));
		_containmentBoundaryTargetLayerNamesTextBox = CreateTextBox(TemplateEditorSettings.FormatGroupNames(Settings.ContainmentBoundaryTargetLayerNames));
		ApplyControlToolTips();
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
			Header = "Parallel Copy",
			Content = BuildParallelCopyTab()
		});
		tabControl.Items.Add(new TabItem
		{
			Header = "Associations",
			Content = BuildAssociationTab()
		});
		tabControl.Items.Add(new TabItem
		{
			Header = "Interface",
			Content = BuildInterfaceTab()
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
		panel.Children.Add(CreateGroupBox("Behavior", CreateCheckBoxPanel(_enableLineSplitPromptsCheckBox, _enablePointPlacementSplitPromptCheckBox, _enableLineEndpointSplitPromptCheckBox, _enableSplitAtLineStartPointCheckBox, _enableSplitAtLineEndPointCheckBox, _enableConfiguredLinePartSplitsCheckBox, _suppressDuplicateSplitPromptsCheckBox, _splitOnlyInteriorCandidatesCheckBox)));
		panel.Children.Add(CreateGroupBox("Prompting", CreateFormGrid(("Split prompt mode", _splitPromptModeComboBox), ("Maximum split candidates to review", _maxSplitCandidatesToReviewTextBox))));
		panel.Children.Add(CreateGroupBox("Eligible Groups", CreateFormGrid(("Split search distance (map units)", _splitSearchDistanceTextBox), ("Eligible point placement groups", _splitPointPlacementGroupsTextBox), ("Eligible line placement groups", _splitLinePlacementGroupsTextBox), ("Underlying target line groups", _splitTargetLineGroupsTextBox), ("Underlying target subtype/layer names", _splitTargetLayerNamesTextBox))));
		return WrapTab(panel);
	}

	private UIElement BuildParallelCopyTab()
	{
		StackPanel panel = CreateTabPanel();
		panel.Children.Add(CreateGroupBox("Behavior", CreateCheckBoxPanel(_enableParallelCopyPromptCheckBox, _enableMultiSegmentParallelCopyCheckBox, _requireConnectedParallelCopySpanCheckBox, _defaultParallelCopyLeftSideCheckBox, _rememberLastParallelCopyOptionsCheckBox, _autoCreateParallelCopyWhenSelectedLineExistsCheckBox)));
		panel.Children.Add(CreateGroupBox("Defaults", CreateFormGrid(("Endpoint match tolerance (map units)", _parallelCopyEndpointMatchToleranceTextBox), ("Default offset distance", _defaultParallelCopyOffsetDistanceTextBox))));
		return WrapTab(panel);
	}

	private UIElement BuildAssociationTab()
	{
		StackPanel panel = CreateTabPanel();
		panel.Children.Add(CreateGroupBox("Behavior", CreateCheckBoxPanel(_enableAssociationPromptsCheckBox, _enableStructuralAttachmentPromptsCheckBox, _enableContainmentPointPromptsCheckBox, _enableContainmentBoundaryPromptsCheckBox, _enableLineAssociationPromptsCheckBox, _enableLineStructuralAttachmentPromptsCheckBox, _enableLineContainmentPointPromptsCheckBox, _enableLineContainmentBoundaryPromptsCheckBox, _stopAfterFirstSuccessfulAssociationCheckBox)));
		panel.Children.Add(CreateGroupBox("Prompting", CreateFormGrid(("Association prompt mode", _associationPromptModeComboBox))));
		panel.Children.Add(CreateGroupBox("Search Distances", CreateFormGrid(("Fallback association search distance", _associationSearchDistanceTextBox), ("Structural attachment search distance", _structuralAttachmentSearchDistanceTextBox), ("Containment point search distance", _containmentPointSearchDistanceTextBox), ("Containment boundary search distance", _containmentBoundarySearchDistanceTextBox))));
		panel.Children.Add(CreateGroupBox("Eligible Groups", CreateFormGrid(("Eligible placement groups", _associationPlacementGroupsTextBox), ("Structural attachment target groups", _structuralAttachmentTargetGroupsTextBox), ("Structural attachment subtype/layer names", _structuralAttachmentTargetLayerNamesTextBox), ("Containment target point groups", _containmentPointTargetGroupsTextBox), ("Containment target point subtype/layer names", _containmentPointTargetLayerNamesTextBox), ("Containment target boundary groups", _containmentBoundaryTargetGroupsTextBox), ("Containment target boundary subtype/layer names", _containmentBoundaryTargetLayerNamesTextBox))));
		return WrapTab(panel);
	}

	private UIElement BuildInterfaceTab()
	{
		StackPanel panel = CreateTabPanel();
		panel.Children.Add(CreateGroupBox("Map Feedback", CreateCheckBoxPanel(_highlightSplitCandidatesCheckBox, _highlightAssociationCandidatesCheckBox, _showAutomaticStepDiagnosticsCheckBox)));
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
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0),
			Style = CreateButtonStyle()
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
			IsCancel = true,
			Style = CreateButtonStyle()
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
			IsDefault = true,
			Style = CreateButtonStyle()
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
			Settings.EnableConfiguredLinePartSplits = _enableConfiguredLinePartSplitsCheckBox.IsChecked == true;
			Settings.SuppressDuplicateSplitPrompts = _suppressDuplicateSplitPromptsCheckBox.IsChecked == true;
			Settings.SplitOnlyInteriorCandidates = _splitOnlyInteriorCandidatesCheckBox.IsChecked == true;
			Settings.MaxSplitCandidatesToReview = ParsePositiveInteger(_maxSplitCandidatesToReviewTextBox.Text, "maximum split candidates to review");
			Settings.SplitPromptMode = GetSelectedComboValue(_splitPromptModeComboBox);
			Settings.SplitSearchDistance = ParseDistance(_splitSearchDistanceTextBox.Text, "split search distance");
			Settings.SplitPointPlacementGroups = TemplateEditorSettings.ParseGroupNames(_splitPointPlacementGroupsTextBox.Text);
			Settings.SplitLinePlacementGroups = TemplateEditorSettings.ParseGroupNames(_splitLinePlacementGroupsTextBox.Text);
			Settings.SplitTargetLineGroups = TemplateEditorSettings.ParseGroupNames(_splitTargetLineGroupsTextBox.Text);
			Settings.SplitTargetLayerNames = TemplateEditorSettings.ParseGroupNames(_splitTargetLayerNamesTextBox.Text);
			Settings.EnableMultiSegmentParallelCopy = _enableMultiSegmentParallelCopyCheckBox.IsChecked == true;
			Settings.RequireConnectedParallelCopySpan = _requireConnectedParallelCopySpanCheckBox.IsChecked == true;
			Settings.ParallelCopyEndpointMatchTolerance = ParseDistance(_parallelCopyEndpointMatchToleranceTextBox.Text, "parallel copy endpoint match tolerance");
			Settings.DefaultParallelCopyOffsetDistance = ParsePositiveDistance(_defaultParallelCopyOffsetDistanceTextBox.Text, "default parallel copy offset distance");
			Settings.DefaultParallelCopyLeftSide = _defaultParallelCopyLeftSideCheckBox.IsChecked == true;
			Settings.RememberLastParallelCopyOptions = _rememberLastParallelCopyOptionsCheckBox.IsChecked == true;
			Settings.AutoCreateParallelCopyWhenSelectedLineExists = _autoCreateParallelCopyWhenSelectedLineExistsCheckBox.IsChecked == true;
			Settings.EnableAssociationPrompts = _enableAssociationPromptsCheckBox.IsChecked == true;
			Settings.EnableStructuralAttachmentPrompts = _enableStructuralAttachmentPromptsCheckBox.IsChecked == true;
			Settings.EnableContainmentPointPrompts = _enableContainmentPointPromptsCheckBox.IsChecked == true;
			Settings.EnableContainmentBoundaryPrompts = _enableContainmentBoundaryPromptsCheckBox.IsChecked == true;
			Settings.EnableLineAssociationPrompts = _enableLineAssociationPromptsCheckBox.IsChecked == true;
			Settings.EnableLineStructuralAttachmentPrompts = _enableLineStructuralAttachmentPromptsCheckBox.IsChecked == true;
			Settings.EnableLineContainmentPointPrompts = _enableLineContainmentPointPromptsCheckBox.IsChecked == true;
			Settings.EnableLineContainmentBoundaryPrompts = _enableLineContainmentBoundaryPromptsCheckBox.IsChecked == true;
			Settings.AssociationPromptMode = GetSelectedComboValue(_associationPromptModeComboBox);
			Settings.StopAfterFirstSuccessfulAssociation = _stopAfterFirstSuccessfulAssociationCheckBox.IsChecked == true;
			Settings.HighlightAssociationCandidates = _highlightAssociationCandidatesCheckBox.IsChecked == true;
			Settings.HighlightSplitCandidates = _highlightSplitCandidatesCheckBox.IsChecked == true;
			Settings.ShowAutomaticStepDiagnostics = _showAutomaticStepDiagnosticsCheckBox.IsChecked == true;
			Settings.AssociationSearchDistance = ParseDistance(_associationSearchDistanceTextBox.Text, "association search distance");
			Settings.StructuralAttachmentSearchDistance = ParseDistance(_structuralAttachmentSearchDistanceTextBox.Text, "structural attachment search distance");
			Settings.ContainmentPointSearchDistance = ParseDistance(_containmentPointSearchDistanceTextBox.Text, "containment point search distance");
			Settings.ContainmentBoundarySearchDistance = ParseDistance(_containmentBoundarySearchDistanceTextBox.Text, "containment boundary search distance");
			Settings.AssociationPlacementGroups = TemplateEditorSettings.ParseGroupNames(_associationPlacementGroupsTextBox.Text);
			Settings.StructuralAttachmentTargetGroups = TemplateEditorSettings.ParseGroupNames(_structuralAttachmentTargetGroupsTextBox.Text);
			Settings.StructuralAttachmentTargetLayerNames = TemplateEditorSettings.ParseGroupNames(_structuralAttachmentTargetLayerNamesTextBox.Text);
			Settings.ContainmentPointTargetGroups = TemplateEditorSettings.ParseGroupNames(_containmentPointTargetGroupsTextBox.Text);
			Settings.ContainmentPointTargetLayerNames = TemplateEditorSettings.ParseGroupNames(_containmentPointTargetLayerNamesTextBox.Text);
			Settings.ContainmentBoundaryTargetGroups = TemplateEditorSettings.ParseGroupNames(_containmentBoundaryTargetGroupsTextBox.Text);
			Settings.ContainmentBoundaryTargetLayerNames = TemplateEditorSettings.ParseGroupNames(_containmentBoundaryTargetLayerNamesTextBox.Text);
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

	private static double ParsePositiveDistance(string text, string label)
	{
		if (!double.TryParse(text, out double result) || result <= 0.0)
		{
			throw new InvalidOperationException("Enter a valid positive number for " + label + ".");
		}
		return result;
	}

	private static int ParsePositiveInteger(string text, string label)
	{
		if (!int.TryParse(text, out int result) || result <= 0)
		{
			throw new InvalidOperationException("Enter a valid positive whole number for " + label + ".");
		}
		return result;
	}

	private static int ParseNonNegativeInteger(string text, string label)
	{
		if (!int.TryParse(text, out int result) || result < 0)
		{
			throw new InvalidOperationException("Enter a valid non-negative whole number for " + label + ".");
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
			SelectionTextBrush = PrimaryTextBrush,
			Style = CreateTextBoxStyle()
		};
	}

	private static ComboBox CreateComboBox(string selectedValue, params (string Label, string Value)[] items)
	{
		ComboBox comboBox = new ComboBox
		{
			MinWidth = 180.0,
			Background = SurfaceBackgroundBrush,
			Foreground = PrimaryTextBrush,
			BorderBrush = ControlBorderBrush,
			Style = CreateComboBoxStyle(),
			ItemContainerStyle = CreateComboBoxItemStyle()
		};
		foreach ((string label, string value) in items)
		{
			comboBox.Items.Add(new ComboBoxItem
			{
				Content = label,
				Tag = value
			});
		}
		foreach (ComboBoxItem item in comboBox.Items)
		{
			if (string.Equals(Convert.ToString(item.Tag), selectedValue, StringComparison.OrdinalIgnoreCase))
			{
				comboBox.SelectedItem = item;
				break;
			}
		}
		comboBox.SelectedIndex = comboBox.SelectedIndex < 0 && comboBox.Items.Count > 0 ? 0 : comboBox.SelectedIndex;
		return comboBox;
	}

	private static string GetSelectedComboValue(ComboBox comboBox)
	{
		return Convert.ToString((comboBox?.SelectedItem as ComboBoxItem)?.Tag) ?? string.Empty;
	}

	private void ApplyControlToolTips()
	{
		SetToolTip(_validateConfigCheckBox, "Checks the template JSON and referenced layers before opening the editor.");
		SetToolTip(_enableLineSplitPromptsCheckBox, "Controls whether placed point and line templates can prompt to split an underlying line.");
		SetToolTip(_enablePointPlacementSplitPromptCheckBox, "Allows point placements, such as switches or devices, to split nearby target lines.");
		SetToolTip(_enableLineEndpointSplitPromptCheckBox, "Allows line start and end points to split nearby target lines.");
		SetToolTip(_enableSplitAtLineStartPointCheckBox, "Includes the first vertex of a placed line as a possible split point.");
		SetToolTip(_enableSplitAtLineEndPointCheckBox, "Includes the last vertex of a placed line as a possible split point.");
		SetToolTip(_enableConfiguredLinePartSplitsCheckBox, "Lets group-template connector lines use their configured start/end points for split prompts.");
		SetToolTip(_suppressDuplicateSplitPromptsCheckBox, "Avoids asking twice for the same split point during one template placement.");
		SetToolTip(_splitOnlyInteriorCandidatesCheckBox, "Skips candidate lines when the split point is already at that line's endpoint.");
		SetToolTip(_splitPromptModeComboBox, "Choose whether to ask before splitting, auto-split when only one candidate exists, or disable splitting.");
		SetToolTip(_maxSplitCandidatesToReviewTextBox, "Limits how many nearby line candidates can be reviewed for one split point.");
		SetToolTip(_splitSearchDistanceTextBox, "Buffer distance, in map units, used to find underlying lines near the split point.");
		SetToolTip(_splitPointPlacementGroupsTextBox, "Comma-separated group layer names for point templates that can trigger split prompts.");
		SetToolTip(_splitLinePlacementGroupsTextBox, "Comma-separated group layer names for line templates that can trigger endpoint split prompts.");
		SetToolTip(_splitTargetLineGroupsTextBox, "Comma-separated group layer names for underlying lines that may be split.");
		SetToolTip(_splitTargetLayerNamesTextBox, "Optional comma-separated subtype or layer names within the target line groups.");
		SetToolTip(_enableParallelCopyPromptCheckBox, "Shows the parallel-copy prompt when a line template is selected and existing lines are selected.");
		SetToolTip(_enableMultiSegmentParallelCopyCheckBox, "Allows several selected line segments to be stitched into one copied span.");
		SetToolTip(_requireConnectedParallelCopySpanCheckBox, "Requires selected segments to touch end-to-end before creating one offset line.");
		SetToolTip(_parallelCopyEndpointMatchToleranceTextBox, "Maximum endpoint gap, in map units, treated as connected for multi-segment copy.");
		SetToolTip(_defaultParallelCopyOffsetDistanceTextBox, "Initial offset distance shown in the parallel-copy prompt.");
		SetToolTip(_defaultParallelCopyLeftSideCheckBox, "Uses left side as the default direction in the parallel-copy prompt.");
		SetToolTip(_rememberLastParallelCopyOptionsCheckBox, "Saves the last entered offset distance and side as the new defaults.");
		SetToolTip(_autoCreateParallelCopyWhenSelectedLineExistsCheckBox, "Creates the offset immediately using defaults when selected lines exist.");
		SetToolTip(_enableAssociationPromptsCheckBox, "Controls all automatic utility-network association prompts after placement.");
		SetToolTip(_enableStructuralAttachmentPromptsCheckBox, "Allows prompts to attach placed features to structural junction targets.");
		SetToolTip(_enableContainmentPointPromptsCheckBox, "Allows prompts to contain placed features in structure point targets.");
		SetToolTip(_enableContainmentBoundaryPromptsCheckBox, "Allows prompts to contain placed features in structure boundary targets.");
		SetToolTip(_enableLineAssociationPromptsCheckBox, "Broad override for line association prompts. Keep off unless line targets are known valid.");
		SetToolTip(_enableLineStructuralAttachmentPromptsCheckBox, "Allows line features to try structural attachment prompts.");
		SetToolTip(_enableLineContainmentPointPromptsCheckBox, "Allows line features to try containment in structure point targets.");
		SetToolTip(_enableLineContainmentBoundaryPromptsCheckBox, "Allows line features to be contained by structure boundary targets.");
		SetToolTip(_associationPromptModeComboBox, "Choose whether to ask, auto-create single candidates, review only multiples, or disable associations.");
		SetToolTip(_stopAfterFirstSuccessfulAssociationCheckBox, "Stops looking for more automatic associations after one association succeeds.");
		SetToolTip(_associationSearchDistanceTextBox, "Fallback association search distance retained for older saved settings.");
		SetToolTip(_structuralAttachmentSearchDistanceTextBox, "Search distance for structural attachment candidates.");
		SetToolTip(_containmentPointSearchDistanceTextBox, "Search distance for structure point containment candidates.");
		SetToolTip(_containmentBoundarySearchDistanceTextBox, "Search distance for structure boundary containment candidates.");
		SetToolTip(_associationPlacementGroupsTextBox, "Comma-separated group layer names for placed features that can run association prompts.");
		SetToolTip(_structuralAttachmentTargetGroupsTextBox, "Comma-separated group layer names for structural attachment targets.");
		SetToolTip(_structuralAttachmentTargetLayerNamesTextBox, "Optional comma-separated subtype or layer names for structural attachment targets.");
		SetToolTip(_containmentPointTargetGroupsTextBox, "Comma-separated group layer names for structure point containment targets.");
		SetToolTip(_containmentPointTargetLayerNamesTextBox, "Optional comma-separated subtype or layer names for structure point containment targets.");
		SetToolTip(_containmentBoundaryTargetGroupsTextBox, "Comma-separated group layer names for structure boundary containment targets.");
		SetToolTip(_containmentBoundaryTargetLayerNamesTextBox, "Optional comma-separated subtype or layer names for structure boundary containment targets.");
		SetToolTip(_highlightSplitCandidatesCheckBox, "Draws a temporary overlay on split candidates while the prompt is visible.");
		SetToolTip(_highlightAssociationCandidatesCheckBox, "Draws a temporary overlay on association candidates while the prompt is visible.");
		SetToolTip(_showAutomaticStepDiagnosticsCheckBox, "Shows popup details when an automatic split or association step fails.");
	}

	private static void SetToolTip(FrameworkElement element, string text)
	{
		if (element != null)
		{
			element.ToolTip = new ToolTip
			{
				Content = text,
				Background = SurfaceBackgroundBrush,
				Foreground = PrimaryTextBrush,
				BorderBrush = ControlBorderBrush,
				Padding = new Thickness(8.0, 4.0, 8.0, 4.0)
			};
		}
	}

	private static CheckBox CreateCheckBox(string content, bool value)
	{
		return new CheckBox
		{
			Content = content,
			IsChecked = value,
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0),
			Foreground = PrimaryTextBrush,
			Style = CreateCheckBoxStyle()
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
		style.Setters.Add(new Setter(Control.BorderBrushProperty, ControlBorderBrush));
		style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1.0)));
		style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
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

	private static Style CreateComboBoxStyle()
	{
		Style style = new Style(typeof(ComboBox));
		style.Setters.Add(new Setter(Control.BackgroundProperty, SurfaceBackgroundBrush));
		style.Setters.Add(new Setter(Control.ForegroundProperty, PrimaryTextBrush));
		style.Setters.Add(new Setter(Control.BorderBrushProperty, ControlBorderBrush));
		style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1.0)));
		style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
		return style;
	}

	private static Style CreateComboBoxItemStyle()
	{
		Style style = new Style(typeof(ComboBoxItem));
		style.Setters.Add(new Setter(Control.BackgroundProperty, SurfaceBackgroundBrush));
		style.Setters.Add(new Setter(Control.ForegroundProperty, PrimaryTextBrush));
		style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8.0, 4.0, 8.0, 4.0)));
		Trigger selectedTrigger = new Trigger
		{
			Property = ComboBoxItem.IsSelectedProperty,
			Value = true
		};
		selectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, TabHoverBackgroundBrush));
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

	private static Style CreateCheckBoxStyle()
	{
		Style style = new Style(typeof(CheckBox));
		style.Setters.Add(new Setter(Control.ForegroundProperty, PrimaryTextBrush));
		style.Setters.Add(new Setter(Control.BackgroundProperty, WindowBackgroundBrush));
		style.Setters.Add(new Setter(Control.BorderBrushProperty, ControlBorderBrush));
		style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
		style.Triggers.Add(new Trigger
		{
			Property = UIElement.IsMouseOverProperty,
			Value = true,
			Setters =
			{
				new Setter(Control.ForegroundProperty, PrimaryTextBrush),
				new Setter(Control.BorderBrushProperty, AccentBorderBrush)
			}
		});
		return style;
	}

	private static Style CreateButtonStyle()
	{
		Style style = new Style(typeof(Button));
		style.Setters.Add(new Setter(Control.BackgroundProperty, TabBackgroundBrush));
		style.Setters.Add(new Setter(Control.ForegroundProperty, PrimaryTextBrush));
		style.Setters.Add(new Setter(Control.BorderBrushProperty, ControlBorderBrush));
		style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1.0)));
		style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
		Trigger hoverTrigger = new Trigger
		{
			Property = UIElement.IsMouseOverProperty,
			Value = true
		};
		hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, TabHoverBackgroundBrush));
		hoverTrigger.Setters.Add(new Setter(Control.ForegroundProperty, PrimaryTextBrush));
		hoverTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, AccentBorderBrush));
		style.Triggers.Add(hoverTrigger);
		Trigger disabledTrigger = new Trigger
		{
			Property = UIElement.IsEnabledProperty,
			Value = false
		};
		disabledTrigger.Setters.Add(new Setter(Control.ForegroundProperty, SystemColors.GrayTextBrush));
		style.Triggers.Add(disabledTrigger);
		return style;
	}
}
