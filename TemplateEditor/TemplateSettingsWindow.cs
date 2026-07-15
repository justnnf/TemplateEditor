using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using ArcGIS.Desktop.Framework;
using Microsoft.Win32;

namespace TemplateEditor;

internal sealed class TemplateSettingsWindow : Window
{
	private static readonly bool IsDarkTheme = FrameworkApplication.ApplicationTheme == ApplicationTheme.Dark;

	private static readonly Brush WindowBackgroundBrush = IsDarkTheme ? new SolidColorBrush(Color.FromRgb(45, 45, 48)) : new SolidColorBrush(Color.FromRgb(243, 243, 243));

	private static readonly Brush SurfaceBackgroundBrush = IsDarkTheme ? new SolidColorBrush(Color.FromRgb(31, 31, 31)) : Brushes.White;

	private static readonly Brush SectionBorderBrush = IsDarkTheme ? new SolidColorBrush(Color.FromRgb(72, 72, 72)) : new SolidColorBrush(Color.FromRgb(208, 208, 208));

	private static readonly Brush ControlBorderBrush = IsDarkTheme ? new SolidColorBrush(Color.FromRgb(104, 104, 104)) : new SolidColorBrush(Color.FromRgb(150, 150, 150));

	private static readonly Brush TabBackgroundBrush = IsDarkTheme ? new SolidColorBrush(Color.FromRgb(52, 52, 56)) : new SolidColorBrush(Color.FromRgb(232, 232, 232));

	private static readonly Brush TabHoverBackgroundBrush = IsDarkTheme ? new SolidColorBrush(Color.FromRgb(58, 58, 62)) : new SolidColorBrush(Color.FromRgb(238, 244, 250));

	private static readonly Brush TabSelectedBackgroundBrush = SurfaceBackgroundBrush;

	private static readonly Brush PrimaryTextBrush = IsDarkTheme ? new SolidColorBrush(Color.FromRgb(238, 238, 238)) : new SolidColorBrush(Color.FromRgb(32, 32, 32));

	private static readonly Brush SecondaryTextBrush = IsDarkTheme ? new SolidColorBrush(Color.FromRgb(178, 178, 178)) : new SolidColorBrush(Color.FromRgb(96, 96, 96));

	private static readonly Brush AccentBorderBrush = new SolidColorBrush(Color.FromRgb(51, 153, 255));

	private static readonly Brush AccentButtonHoverBrush = new SolidColorBrush(Color.FromRgb(32, 128, 224));

	private readonly TextBox _templateConfigPathTextBox;

	private readonly CheckBox _validateConfigCheckBox;

	private readonly CheckBox _preventDefaultVersionPlacementCheckBox;

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

	private readonly CheckBox _enableJunctionJunctionConnectivityPromptsCheckBox;

	private readonly CheckBox _enableLineAssociationPromptsCheckBox;

	private readonly CheckBox _enableLineStructuralAttachmentPromptsCheckBox;

	private readonly CheckBox _enableLineContainmentPointPromptsCheckBox;

	private readonly CheckBox _enableLineContainmentBoundaryPromptsCheckBox;

	private readonly ComboBox _associationPromptModeComboBox;

	private readonly ComboBox _configuredAssociationPlacementModeComboBox;

	private readonly CheckBox _stopAfterFirstSuccessfulAssociationCheckBox;

	private readonly CheckBox _highlightAssociationCandidatesCheckBox;

	private readonly CheckBox _highlightSplitCandidatesCheckBox;

	private readonly CheckBox _showAutomaticStepDiagnosticsCheckBox;

	private readonly CheckBox _useCompactDockpaneLayoutCheckBox;

	private readonly TextBox _maxRecentTemplatesTextBox;

	private readonly TextBox _hintSourceColorHexTextBox;

	private readonly TextBox _hintAssociationTargetColorHexTextBox;

	private readonly TextBox _hintSplitCandidateColorHexTextBox;

	private readonly TextBox _structuralAttachmentSearchDistanceTextBox;

	private readonly TextBox _junctionJunctionConnectivitySearchDistanceTextBox;

	private readonly TextBox _containmentPointSearchDistanceTextBox;

	private readonly TextBox _containmentBoundarySearchDistanceTextBox;

	private readonly TextBox _associationPlacementGroupsTextBox;

	private readonly TextBox _structuralAttachmentTargetGroupsTextBox;

	private readonly TextBox _structuralAttachmentTargetLayerNamesTextBox;

	private readonly TextBox _junctionJunctionConnectivityTargetGroupsTextBox;

	private readonly TextBox _junctionJunctionConnectivityTargetLayerNamesTextBox;

	private readonly TextBox _containmentPointTargetGroupsTextBox;

	private readonly TextBox _containmentPointTargetLayerNamesTextBox;

	private readonly TextBox _containmentBoundaryTargetGroupsTextBox;

	private readonly TextBox _containmentBoundaryTargetLayerNamesTextBox;

	private readonly TextBox _associationRulesJsonPathTextBox;

	private readonly Button _regenerateAssociationRulesButton;

	private readonly List<PlacementOverrideEditorRow> _sessionOverrideRows = new List<PlacementOverrideEditorRow>();

	public TemplateEditorSettings Settings { get; }

	public TemplateSettingsWindow(TemplateEditorSettings settings)
	{
		Settings = settings.Clone();
		Title = "Template Settings";
		Width = 900.0;
		Height = 680.0;
		MinWidth = 760.0;
		MinHeight = 560.0;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		ResizeMode = ResizeMode.CanResize;
		Background = WindowBackgroundBrush;
		Foreground = PrimaryTextBrush;
		FontFamily = new FontFamily("Segoe UI");
		FontSize = 12.0;
		Resources[SystemColors.ControlTextBrushKey] = PrimaryTextBrush;
		Resources[SystemColors.WindowTextBrushKey] = PrimaryTextBrush;
		Resources[SystemColors.GrayTextBrushKey] = SecondaryTextBrush;
		Resources[SystemColors.ControlBrushKey] = WindowBackgroundBrush;
		Resources[SystemColors.WindowBrushKey] = SurfaceBackgroundBrush;
		Resources[SystemColors.HighlightBrushKey] = TabHoverBackgroundBrush;
		Resources[SystemColors.HighlightTextBrushKey] = PrimaryTextBrush;
		Resources[SystemColors.ControlLightBrushKey] = SurfaceBackgroundBrush;
		Resources[SystemColors.ControlDarkBrushKey] = SectionBorderBrush;
		_templateConfigPathTextBox = CreateTextBox(Settings.TemplateConfigFilePath ?? string.Empty);
		_validateConfigCheckBox = CreateCheckBox("Validate template configuration before opening the editor", Settings.ValidateConfig);
		_preventDefaultVersionPlacementCheckBox = CreateCheckBox("Prevent template placement into DEFAULT versions", Settings.PreventDefaultVersionPlacement);
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
		_enableContainmentBoundaryPromptsCheckBox = CreateCheckBox("Allow containment prompts for structure containers", Settings.EnableContainmentBoundaryPrompts);
		_enableJunctionJunctionConnectivityPromptsCheckBox = CreateCheckBox("Allow junction-junction connectivity prompts", Settings.EnableJunctionJunctionConnectivityPrompts);
		_enableLineAssociationPromptsCheckBox = CreateCheckBox("Allow association prompts for line features", Settings.EnableLineAssociationPrompts);
		_enableLineStructuralAttachmentPromptsCheckBox = CreateCheckBox("Allow line structural attachment prompts", Settings.EnableLineStructuralAttachmentPrompts);
		_enableLineContainmentPointPromptsCheckBox = CreateCheckBox("Allow line containment prompts for structure points", Settings.EnableLineContainmentPointPrompts);
		_enableLineContainmentBoundaryPromptsCheckBox = CreateCheckBox("Allow line features to be contained by structure containers", Settings.EnableLineContainmentBoundaryPrompts);
		_associationPromptModeComboBox = CreateComboBox(Settings.AssociationPromptMode, ("Always ask", "AlwaysAsk"), ("Auto-create when one candidate", "AutoWhenOne"), ("Review multiple only", "ReviewMultipleOnly"), ("Never create", "Never"));
		_configuredAssociationPlacementModeComboBox = CreateComboBox(Settings.ConfiguredAssociationPlacementMode, ("Fast - batch configured associations", "Fast"), ("Debug - isolate configured association failures", "Debug"));
		_stopAfterFirstSuccessfulAssociationCheckBox = CreateCheckBox("Stop association prompts after first successful association", Settings.StopAfterFirstSuccessfulAssociation);
		_highlightAssociationCandidatesCheckBox = CreateCheckBox("Highlight association candidates on the map", Settings.HighlightAssociationCandidates);
		_highlightSplitCandidatesCheckBox = CreateCheckBox("Highlight split candidates on the map", Settings.HighlightSplitCandidates);
		_showAutomaticStepDiagnosticsCheckBox = CreateCheckBox("Show diagnostics when automatic placement steps fail", Settings.ShowAutomaticStepDiagnostics);
		_useCompactDockpaneLayoutCheckBox = CreateCheckBox("Use compact dockpane layout", Settings.UseCompactDockpaneLayout);
		_maxRecentTemplatesTextBox = CreateTextBox(Settings.MaxRecentTemplates.ToString());
		_hintSourceColorHexTextBox = CreateTextBox(Settings.HintSourceColorHex);
		_hintAssociationTargetColorHexTextBox = CreateTextBox(Settings.HintAssociationTargetColorHex);
		_hintSplitCandidateColorHexTextBox = CreateTextBox(Settings.HintSplitCandidateColorHex);
		_structuralAttachmentSearchDistanceTextBox = CreateTextBox(Settings.StructuralAttachmentSearchDistance.ToString("0.###"));
		_junctionJunctionConnectivitySearchDistanceTextBox = CreateTextBox(Settings.JunctionJunctionConnectivitySearchDistance.ToString("0.###"));
		_containmentPointSearchDistanceTextBox = CreateTextBox(Settings.ContainmentPointSearchDistance.ToString("0.###"));
		_containmentBoundarySearchDistanceTextBox = CreateTextBox(Settings.ContainmentBoundarySearchDistance.ToString("0.###"));
		_associationPlacementGroupsTextBox = CreateTextBox(TemplateEditorSettings.FormatGroupNames(Settings.AssociationPlacementGroups));
		_structuralAttachmentTargetGroupsTextBox = CreateTextBox(TemplateEditorSettings.FormatGroupNames(Settings.StructuralAttachmentTargetGroups));
		_structuralAttachmentTargetLayerNamesTextBox = CreateTextBox(TemplateEditorSettings.FormatGroupNames(Settings.StructuralAttachmentTargetLayerNames));
		_junctionJunctionConnectivityTargetGroupsTextBox = CreateTextBox(TemplateEditorSettings.FormatGroupNames(Settings.JunctionJunctionConnectivityTargetGroups));
		_junctionJunctionConnectivityTargetLayerNamesTextBox = CreateTextBox(TemplateEditorSettings.FormatGroupNames(Settings.JunctionJunctionConnectivityTargetLayerNames));
		_containmentPointTargetGroupsTextBox = CreateTextBox(TemplateEditorSettings.FormatGroupNames(Settings.ContainmentPointTargetGroups));
		_containmentPointTargetLayerNamesTextBox = CreateTextBox(TemplateEditorSettings.FormatGroupNames(Settings.ContainmentPointTargetLayerNames));
		_containmentBoundaryTargetGroupsTextBox = CreateTextBox(TemplateEditorSettings.FormatGroupNames(Settings.ContainmentBoundaryTargetGroups));
		_containmentBoundaryTargetLayerNamesTextBox = CreateTextBox(TemplateEditorSettings.FormatGroupNames(Settings.ContainmentBoundaryTargetLayerNames));
		_associationRulesJsonPathTextBox = CreateTextBox(Settings.AssociationRulesJsonPath ?? AssociationRuleCatalog.RuleFilePath);
		_regenerateAssociationRulesButton = new Button
		{
			Content = "Regenerate association rules JSON",
			HorizontalAlignment = HorizontalAlignment.Left,
			MinWidth = 220.0,
			Margin = new Thickness(0.0, 4.0, 0.0, 0.0),
			Padding = new Thickness(12.0, 5.0, 12.0, 5.0),
			Style = CreateButtonStyle()
		};
		_regenerateAssociationRulesButton.Click += RegenerateAssociationRulesButton_Click;
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
		root.Children.Add(BuildSettingsTabs());
		return root;
	}

	private UIElement BuildSettingsTabs()
	{
		DockPanel container = new DockPanel
		{
			LastChildFill = true,
			Margin = new Thickness(12.0)
		};
		Border contentFrame = new Border
		{
			Background = SurfaceBackgroundBrush,
			BorderBrush = SectionBorderBrush,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(0.0, 0.0, 4.0, 4.0),
			MinHeight = 360.0
		};
		StackPanel tabRow = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(0.0, 0.0, 0.0, -1.0)
		};
		DockPanel.SetDock(tabRow, Dock.Top);
		container.Children.Add(tabRow);
		container.Children.Add(contentFrame);

		List<Button> buttons = new List<Button>();
		Button selectedTabButton = null;
		UIElement generalContent = BuildGeneralTab();
		UIElement lineSplitContent = BuildLineSplitTab();
		UIElement parallelCopyContent = BuildParallelCopyTab();
		UIElement associationsContent = BuildAssociationTab();
		UIElement attributeOverridesContent = BuildAttributeOverridesTab();
		UIElement interfaceContent = BuildInterfaceTab();
		void SelectTab(Button selectedButton, UIElement content)
		{
			if (ReferenceEquals(selectedTabButton, selectedButton))
			{
				return;
			}
			contentFrame.Child = null;
			foreach (Button button in buttons)
			{
				button.Background = TabBackgroundBrush;
				button.BorderBrush = SectionBorderBrush;
				button.FontWeight = FontWeights.Normal;
			}
			selectedButton.Background = SurfaceBackgroundBrush;
			selectedButton.BorderBrush = SectionBorderBrush;
			selectedButton.FontWeight = FontWeights.SemiBold;
			selectedTabButton = selectedButton;
			contentFrame.Child = content;
		}
		Button generalButton = CreateSettingsTabButton("General");
		Button lineSplitButton = CreateSettingsTabButton("Line Split");
		Button parallelCopyButton = CreateSettingsTabButton("Parallel Copy");
		Button associationsButton = CreateSettingsTabButton("Associations");
		Button attributeOverridesButton = CreateSettingsTabButton("Attribute Overrides");
		Button interfaceButton = CreateSettingsTabButton("Interface");
		buttons.Add(generalButton);
		buttons.Add(lineSplitButton);
		buttons.Add(parallelCopyButton);
		buttons.Add(associationsButton);
		buttons.Add(attributeOverridesButton);
		buttons.Add(interfaceButton);
		generalButton.Click += delegate { SelectTab(generalButton, generalContent); };
		lineSplitButton.Click += delegate { SelectTab(lineSplitButton, lineSplitContent); };
		parallelCopyButton.Click += delegate { SelectTab(parallelCopyButton, parallelCopyContent); };
		associationsButton.Click += delegate { SelectTab(associationsButton, associationsContent); };
		attributeOverridesButton.Click += delegate { SelectTab(attributeOverridesButton, attributeOverridesContent); };
		interfaceButton.Click += delegate { SelectTab(interfaceButton, interfaceContent); };
		foreach (Button button in buttons)
		{
			tabRow.Children.Add(button);
		}
		SelectTab(generalButton, generalContent);
		return container;
	}

	private static Button CreateSettingsTabButton(string text)
	{
		return new Button
		{
			Content = text,
			MinWidth = 124.0,
			Padding = new Thickness(22.0, 10.0, 22.0, 10.0),
			Margin = new Thickness(0.0, 0.0, -1.0, 0.0),
			Background = TabBackgroundBrush,
			Foreground = PrimaryTextBrush,
			BorderBrush = SectionBorderBrush,
			BorderThickness = new Thickness(1.0),
			Style = CreateSettingsTabButtonStyle()
		};
	}

	private UIElement BuildGeneralTab()
	{
		StackPanel panel = CreateTabPanel();
		panel.Children.Add(CreateGroupBox("Template Configuration", BuildTemplateConfigSection()));
		panel.Children.Add(CreateGroupBox("Placement Safety", CreateCheckBoxPanel(_preventDefaultVersionPlacementCheckBox)));
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
		panel.Children.Add(CreateGroupBox("Behavior", CreateCheckBoxPanel(_enableAssociationPromptsCheckBox, _enableStructuralAttachmentPromptsCheckBox, _enableJunctionJunctionConnectivityPromptsCheckBox, _enableContainmentPointPromptsCheckBox, _enableContainmentBoundaryPromptsCheckBox, _enableLineAssociationPromptsCheckBox, _enableLineStructuralAttachmentPromptsCheckBox, _enableLineContainmentPointPromptsCheckBox, _enableLineContainmentBoundaryPromptsCheckBox, _stopAfterFirstSuccessfulAssociationCheckBox)));
		panel.Children.Add(CreateGroupBox("Prompting", CreateFormGrid(("Association prompt mode", _associationPromptModeComboBox), ("Configured association mode", _configuredAssociationPlacementModeComboBox))));
		panel.Children.Add(CreateGroupBox("Search Distances", CreateFormGrid(("Structural attachment search distance", _structuralAttachmentSearchDistanceTextBox), ("Junction-junction connectivity search distance", _junctionJunctionConnectivitySearchDistanceTextBox), ("Containment point search distance", _containmentPointSearchDistanceTextBox), ("Structure container search distance", _containmentBoundarySearchDistanceTextBox))));
		panel.Children.Add(CreateGroupBox("Fallback Eligible Groups", CreateFormGrid(("Eligible placement groups", _associationPlacementGroupsTextBox), ("Structural attachment target groups", _structuralAttachmentTargetGroupsTextBox), ("Structural attachment subtype/layer names", _structuralAttachmentTargetLayerNamesTextBox), ("Junction-junction connectivity target groups", _junctionJunctionConnectivityTargetGroupsTextBox), ("Junction-junction connectivity subtype/layer names", _junctionJunctionConnectivityTargetLayerNamesTextBox), ("Containment target point groups", _containmentPointTargetGroupsTextBox), ("Containment target point subtype/layer names", _containmentPointTargetLayerNamesTextBox), ("Structure container target groups", _containmentBoundaryTargetGroupsTextBox), ("Structure container subtype/layer names", _containmentBoundaryTargetLayerNamesTextBox))));
		panel.Children.Add(CreateGroupBox("Rule Catalog", BuildAssociationRuleCatalogSection()));
		return WrapTab(panel);
	}

	private UIElement BuildAssociationRuleCatalogSection()
	{
		StackPanel panel = new StackPanel();
		Grid pathGrid = new Grid
		{
			Margin = new Thickness(0.0, 4.0, 0.0, 8.0)
		};
		pathGrid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		pathGrid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		pathGrid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		pathGrid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		TextBlock label = CreateLabel("Association rules JSON path");
		Grid.SetColumnSpan(label, 2);
		pathGrid.Children.Add(label);
		Grid.SetRow(_associationRulesJsonPathTextBox, 1);
		pathGrid.Children.Add(_associationRulesJsonPathTextBox);
		Button browseButton = new Button
		{
			Content = "Browse...",
			MinWidth = 90.0,
			Margin = new Thickness(8.0, 22.0, 0.0, 0.0),
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0),
			Style = CreateButtonStyle()
		};
		browseButton.Click += BrowseAssociationRulesJsonPath_Click;
		Grid.SetRow(browseButton, 1);
		Grid.SetColumn(browseButton, 1);
		pathGrid.Children.Add(browseButton);
		panel.Children.Add(pathGrid);
		panel.Children.Add(_regenerateAssociationRulesButton);
		return panel;
	}

	private UIElement BuildInterfaceTab()
	{
		StackPanel panel = CreateTabPanel();
		panel.Children.Add(CreateGroupBox("Dockpane Layout", CreateFormGrid(("Use compact dockpane layout", _useCompactDockpaneLayoutCheckBox), ("Maximum recent templates", _maxRecentTemplatesTextBox))));
		panel.Children.Add(CreateGroupBox("Map Feedback", CreateCheckBoxPanel(_highlightSplitCandidatesCheckBox, _highlightAssociationCandidatesCheckBox, _showAutomaticStepDiagnosticsCheckBox)));
		panel.Children.Add(CreateGroupBox("Hint Colors", CreateFormGrid(("Placed/source feature color (HEX)", _hintSourceColorHexTextBox), ("Association target color (HEX)", _hintAssociationTargetColorHexTextBox), ("Split candidate color (HEX)", _hintSplitCandidateColorHexTextBox))));
		return WrapTab(panel);
	}

	private UIElement BuildAttributeOverridesTab()
	{
		StackPanel panel = CreateTabPanel();
		panel.Children.Add(CreateGroupBox("Session Overrides", BuildAttributeOverrideSection()));
		return WrapTab(panel);
	}

	private UIElement BuildAttributeOverrideSection()
	{
		StackPanel panel = new StackPanel();
		panel.Children.Add(new TextBlock
		{
			Text = "Choose workflow-wide attribute overrides. These apply only to configured fields and can be superseded by the next-placement override dialog from the template right-click menu.",
			Foreground = SecondaryTextBrush,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0)
		});
		_sessionOverrideRows.Clear();
		IReadOnlyList<PlacementAttributeOverrideEditorState> states = PlacementAttributeOverrideService.BuildSessionEditorStates(Settings.SessionAttributeOverrides);
		if (states.Count == 0)
		{
			panel.Children.Add(new TextBlock
			{
				Text = "No packaged placement override fields are currently available.",
				Foreground = SecondaryTextBrush,
				TextWrapping = TextWrapping.Wrap
			});
			return panel;
		}
		foreach (PlacementAttributeOverrideEditorState state in states)
		{
			PlacementOverrideEditorRow row = CreatePlacementOverrideEditorRow(state);
			_sessionOverrideRows.Add(row);
			panel.Children.Add(row.Container);
		}
		return panel;
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
			Width = new GridLength(320.0)
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
			label.TextWrapping = TextWrapping.Wrap;
			label.TextTrimming = TextTrimming.None;
			Grid.SetRow(label, i);
			grid.Children.Add(label);
			Control control = rows[i].Control;
			control.Margin = new Thickness(0.0, 0.0, 0.0, 8.0);
			ApplyControlSizing(rows[i].Label, control);
			Grid.SetRow(control, i);
			Grid.SetColumn(control, 1);
			grid.Children.Add(control);
		}
		return grid;
	}

	private static void ApplyControlSizing(string label, Control control)
	{
		string normalizedLabel = label?.ToUpperInvariant() ?? string.Empty;
		if (control is TextBox textBox &&
			(normalizedLabel.Contains("GROUP") || normalizedLabel.Contains("LAYER") || normalizedLabel.Contains("SUBTYPE")))
		{
			textBox.AcceptsReturn = true;
			textBox.TextWrapping = TextWrapping.Wrap;
			textBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
			textBox.MinHeight = 56.0;
			return;
		}
		if (normalizedLabel.Contains("DISTANCE") ||
			normalizedLabel.Contains("TOLERANCE") ||
			normalizedLabel.Contains("MAXIMUM") ||
			normalizedLabel.Contains("OFFSET") ||
			normalizedLabel.Contains("HEX"))
		{
			control.Width = 180.0;
			control.HorizontalAlignment = HorizontalAlignment.Left;
			return;
		}
		if (control is ComboBox)
		{
			control.Width = 280.0;
			control.HorizontalAlignment = HorizontalAlignment.Left;
		}
	}

	private static Border CreateGroupBox(string header, UIElement content)
	{
		StackPanel panel = new StackPanel();
		panel.Children.Add(new TextBlock
		{
			Text = header,
			Foreground = PrimaryTextBrush,
			FontWeight = FontWeights.SemiBold,
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		});
		panel.Children.Add(new Border
		{
			Height = 1.0,
			Background = SectionBorderBrush,
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		});
		panel.Children.Add(content);
		return new Border
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0),
			Padding = new Thickness(14.0),
			Background = SurfaceBackgroundBrush,
			BorderBrush = SectionBorderBrush,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(4.0),
			Child = panel
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
			Style = CreatePrimaryButtonStyle()
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
			Settings.PreventDefaultVersionPlacement = _preventDefaultVersionPlacementCheckBox.IsChecked == true;
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
			Settings.EnableJunctionJunctionConnectivityPrompts = _enableJunctionJunctionConnectivityPromptsCheckBox.IsChecked == true;
			Settings.EnableLineAssociationPrompts = _enableLineAssociationPromptsCheckBox.IsChecked == true;
			Settings.EnableLineStructuralAttachmentPrompts = _enableLineStructuralAttachmentPromptsCheckBox.IsChecked == true;
			Settings.EnableLineContainmentPointPrompts = _enableLineContainmentPointPromptsCheckBox.IsChecked == true;
			Settings.EnableLineContainmentBoundaryPrompts = _enableLineContainmentBoundaryPromptsCheckBox.IsChecked == true;
			Settings.AssociationPromptMode = GetSelectedComboValue(_associationPromptModeComboBox);
			Settings.ConfiguredAssociationPlacementMode = GetSelectedComboValue(_configuredAssociationPlacementModeComboBox);
			Settings.StopAfterFirstSuccessfulAssociation = _stopAfterFirstSuccessfulAssociationCheckBox.IsChecked == true;
			Settings.HighlightAssociationCandidates = _highlightAssociationCandidatesCheckBox.IsChecked == true;
			Settings.HighlightSplitCandidates = _highlightSplitCandidatesCheckBox.IsChecked == true;
			Settings.ShowAutomaticStepDiagnostics = _showAutomaticStepDiagnosticsCheckBox.IsChecked == true;
			Settings.UseCompactDockpaneLayout = _useCompactDockpaneLayoutCheckBox.IsChecked == true;
			Settings.MaxRecentTemplates = ParsePositiveInteger(_maxRecentTemplatesTextBox.Text, "maximum recent templates");
			Settings.HintSourceColorHex = _hintSourceColorHexTextBox.Text;
			Settings.HintAssociationTargetColorHex = _hintAssociationTargetColorHexTextBox.Text;
			Settings.HintSplitCandidateColorHex = _hintSplitCandidateColorHexTextBox.Text;
			Settings.StructuralAttachmentSearchDistance = ParseDistance(_structuralAttachmentSearchDistanceTextBox.Text, "structural attachment search distance");
			Settings.JunctionJunctionConnectivitySearchDistance = ParseDistance(_junctionJunctionConnectivitySearchDistanceTextBox.Text, "junction-junction connectivity search distance");
			Settings.ContainmentPointSearchDistance = ParseDistance(_containmentPointSearchDistanceTextBox.Text, "containment point search distance");
			Settings.ContainmentBoundarySearchDistance = ParseDistance(_containmentBoundarySearchDistanceTextBox.Text, "structure container search distance");
			Settings.AssociationPlacementGroups = TemplateEditorSettings.ParseGroupNames(_associationPlacementGroupsTextBox.Text);
			Settings.StructuralAttachmentTargetGroups = TemplateEditorSettings.ParseGroupNames(_structuralAttachmentTargetGroupsTextBox.Text);
			Settings.StructuralAttachmentTargetLayerNames = TemplateEditorSettings.ParseGroupNames(_structuralAttachmentTargetLayerNamesTextBox.Text);
			Settings.JunctionJunctionConnectivityTargetGroups = TemplateEditorSettings.ParseGroupNames(_junctionJunctionConnectivityTargetGroupsTextBox.Text);
			Settings.JunctionJunctionConnectivityTargetLayerNames = TemplateEditorSettings.ParseGroupNames(_junctionJunctionConnectivityTargetLayerNamesTextBox.Text);
			Settings.ContainmentPointTargetGroups = TemplateEditorSettings.ParseGroupNames(_containmentPointTargetGroupsTextBox.Text);
			Settings.ContainmentPointTargetLayerNames = TemplateEditorSettings.ParseGroupNames(_containmentPointTargetLayerNamesTextBox.Text);
			Settings.ContainmentBoundaryTargetGroups = TemplateEditorSettings.ParseGroupNames(_containmentBoundaryTargetGroupsTextBox.Text);
			Settings.ContainmentBoundaryTargetLayerNames = TemplateEditorSettings.ParseGroupNames(_containmentBoundaryTargetLayerNamesTextBox.Text);
			Settings.AssociationRulesJsonPath = string.IsNullOrWhiteSpace(_associationRulesJsonPathTextBox.Text) ? null : _associationRulesJsonPathTextBox.Text.Trim();
			Settings.SessionAttributeOverrides = _sessionOverrideRows.Select(row => new PlacementAttributeOverrideValue
			{
				FieldName = row.State.Definition.FieldName,
				Enabled = row.EnabledCheckBox.IsChecked == true,
				Value = row.UseDropDown ? row.ValueComboBox.SelectedItem as string : row.ValueTextBox.Text
			}).ToList();
			Settings.Normalize();
			DialogResult = true;
			Close();
		}
		catch (Exception ex)
		{
			DialogService.Show(ex.Message, "Template Settings");
		}
	}

	private void BrowseAssociationRulesJsonPath_Click(object sender, RoutedEventArgs e)
	{
		SaveFileDialog saveFileDialog = new SaveFileDialog
		{
			Title = "Association Rules JSON",
			Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
			OverwritePrompt = false,
			FileName = "AllowedAssociationRules.json"
		};
		string currentPath = string.IsNullOrWhiteSpace(_associationRulesJsonPathTextBox.Text) ? AssociationRuleCatalog.RuleFilePath : _associationRulesJsonPathTextBox.Text.Trim();
		if (!string.IsNullOrWhiteSpace(currentPath))
		{
			string directoryName = Path.GetDirectoryName(currentPath);
			if (!string.IsNullOrWhiteSpace(directoryName) && Directory.Exists(directoryName))
			{
				saveFileDialog.InitialDirectory = directoryName;
			}
			string fileName = Path.GetFileName(currentPath);
			if (!string.IsNullOrWhiteSpace(fileName))
			{
				saveFileDialog.FileName = fileName;
			}
		}
		if (saveFileDialog.ShowDialog() == true)
		{
			_associationRulesJsonPathTextBox.Text = saveFileDialog.FileName;
		}
	}

	private async void RegenerateAssociationRulesButton_Click(object sender, RoutedEventArgs e)
	{
		if (DialogService.Show(
			"Regenerate the association rule JSON from the utility network in the active map?",
			"Template Editor",
			new DialogButtonChoice("Regenerate", MessageBoxResult.Yes, isPrimary: true),
			new DialogButtonChoice("Cancel", MessageBoxResult.No, isCancel: true)) != MessageBoxResult.Yes)
		{
			return;
		}
		string jsonPath = string.IsNullOrWhiteSpace(_associationRulesJsonPathTextBox.Text) ? null : _associationRulesJsonPathTextBox.Text.Trim();
		_regenerateAssociationRulesButton.IsEnabled = false;
		object originalContent = _regenerateAssociationRulesButton.Content;
		_regenerateAssociationRulesButton.Content = "Regenerating...";
		try
		{
			AssociationRuleGenerationResult result = await AssociationRuleJsonRegenerator.RegenerateFromActiveMapAsync(jsonPath);
			Settings.AssociationRulesJsonPath = result.OutputPath;
			_associationRulesJsonPathTextBox.Text = result.OutputPath;
			DialogService.Show($"Regenerated association rules JSON.\n\nRules written: {result.RuleCount}\nFile: {result.OutputPath}", "Template Editor");
		}
		catch (Exception ex)
		{
			DialogService.Show("The association rules JSON could not be regenerated.\n\n" + ex.Message, "Template Editor");
		}
		finally
		{
			_regenerateAssociationRulesButton.Content = originalContent;
			_regenerateAssociationRulesButton.IsEnabled = true;
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

	private PlacementOverrideEditorRow CreatePlacementOverrideEditorRow(PlacementAttributeOverrideEditorState state)
	{
		CheckBox enabledCheckBox = new CheckBox
		{
			IsChecked = state.IsEnabled,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(0.0, 2.0, 10.0, 0.0)
		};
		TextBlock label = new TextBlock
		{
			Text = state.Definition.Label,
			FontWeight = FontWeights.SemiBold,
			Foreground = PrimaryTextBrush
		};
		TextBlock description = new TextBlock
		{
			Text = string.IsNullOrWhiteSpace(state.Definition.Description) ? state.ConfiguredValueSummary : state.Definition.Description + "\n" + state.ConfiguredValueSummary,
			Foreground = SecondaryTextBrush,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 2.0, 0.0, 0.0)
		};
		Control valueEditor;
		ComboBox valueComboBox = null;
		TextBox valueTextBox = null;
		if (state.UseDropDown)
		{
			valueComboBox = new ComboBox
			{
				ItemsSource = state.AvailableValues,
				SelectedItem = state.AvailableValues.FirstOrDefault(value => string.Equals(value, state.Value, StringComparison.OrdinalIgnoreCase)) ?? state.AvailableValues.FirstOrDefault(),
				MinWidth = 220.0,
				Margin = new Thickness(0.0, 6.0, 0.0, 0.0),
				Style = CreateComboBoxStyle(),
				ItemContainerStyle = CreateComboBoxItemStyle()
			};
			valueEditor = valueComboBox;
		}
		else
		{
			valueTextBox = CreateTextBox(state.Value ?? string.Empty);
			valueTextBox.MinWidth = 220.0;
			valueTextBox.Margin = new Thickness(0.0, 6.0, 0.0, 0.0);
			valueEditor = valueTextBox;
		}
		valueEditor.IsEnabled = state.IsEnabled;
		enabledCheckBox.Checked += delegate { valueEditor.IsEnabled = true; };
		enabledCheckBox.Unchecked += delegate { valueEditor.IsEnabled = false; };
		StackPanel details = new StackPanel();
		details.Children.Add(label);
		details.Children.Add(description);
		details.Children.Add(valueEditor);
		DockPanel content = new DockPanel
		{
			LastChildFill = true
		};
		DockPanel.SetDock(enabledCheckBox, Dock.Left);
		content.Children.Add(enabledCheckBox);
		content.Children.Add(details);
		Border container = new Border
		{
			Background = SurfaceBackgroundBrush,
			BorderBrush = SectionBorderBrush,
			BorderThickness = new Thickness(1.0),
			Padding = new Thickness(10.0),
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0),
			Child = content
		};
		return new PlacementOverrideEditorRow(state, container, enabledCheckBox, valueComboBox, valueTextBox);
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
		SetToolTip(_preventDefaultVersionPlacementCheckBox, "Blocks template placement when a target feature service layer or table is connected to DEFAULT/sde.DEFAULT.");
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
		SetToolTip(_enableJunctionJunctionConnectivityPromptsCheckBox, "Allows prompts to connect a placed junction to nearby device or junction targets when a rule exists.");
		SetToolTip(_enableContainmentPointPromptsCheckBox, "Allows prompts to contain placed features in structure point targets.");
		SetToolTip(_enableContainmentBoundaryPromptsCheckBox, "Allows prompts to contain placed features in structure containers such as boundaries, trenches, or conduits.");
		SetToolTip(_enableLineAssociationPromptsCheckBox, "Broad override for line association prompts. Keep off unless line targets are known valid.");
		SetToolTip(_enableLineStructuralAttachmentPromptsCheckBox, "Allows line features to try structural attachment prompts.");
		SetToolTip(_enableLineContainmentPointPromptsCheckBox, "Allows line features to try containment in structure point targets.");
		SetToolTip(_enableLineContainmentBoundaryPromptsCheckBox, "Allows electric line features to be contained by structure line containers such as Underground/Trench or Underground/Conduit.");
		SetToolTip(_associationPromptModeComboBox, "Choose whether to ask, auto-create single candidates, review only multiples, or disable associations.");
		SetToolTip(_configuredAssociationPlacementModeComboBox, "Fast batches configured template associations into one edit operation. Debug creates them one-by-one and reports the exact failures.");
		SetToolTip(_stopAfterFirstSuccessfulAssociationCheckBox, "Stops looking for more automatic associations after one association succeeds.");
		SetToolTip(_structuralAttachmentSearchDistanceTextBox, "Search distance for structural attachment candidates.");
		SetToolTip(_junctionJunctionConnectivitySearchDistanceTextBox, "Search distance for nearby junction-junction connectivity candidates.");
		SetToolTip(_containmentPointSearchDistanceTextBox, "Search distance for structure point containment candidates.");
		SetToolTip(_containmentBoundarySearchDistanceTextBox, "Search distance for structure container candidates.");
		SetToolTip(_associationPlacementGroupsTextBox, "Fallback group layer names used only when no association rule JSON is loaded.");
		SetToolTip(_structuralAttachmentTargetGroupsTextBox, "Fallback structural attachment target groups used only when no association rule JSON is loaded.");
		SetToolTip(_structuralAttachmentTargetLayerNamesTextBox, "Fallback structural attachment subtype or layer names used only when no association rule JSON is loaded.");
		SetToolTip(_junctionJunctionConnectivityTargetGroupsTextBox, "Fallback JJC target groups used only when no association rule JSON is loaded.");
		SetToolTip(_junctionJunctionConnectivityTargetLayerNamesTextBox, "Fallback JJC subtype or layer names used only when no association rule JSON is loaded.");
		SetToolTip(_containmentPointTargetGroupsTextBox, "Fallback structure point containment target groups used only when no association rule JSON is loaded.");
		SetToolTip(_containmentPointTargetLayerNamesTextBox, "Fallback structure point containment subtype or layer names used only when no association rule JSON is loaded.");
		SetToolTip(_containmentBoundaryTargetGroupsTextBox, "Fallback structure container target groups used only when no association rule JSON is loaded.");
		SetToolTip(_containmentBoundaryTargetLayerNamesTextBox, "Fallback structure container subtype or layer names used only when no association rule JSON is loaded.");
		SetToolTip(_regenerateAssociationRulesButton, "Reads the active map's utility network rules directly through the Pro SDK and rebuilds the local JSON rule catalog.");
		SetToolTip(_highlightSplitCandidatesCheckBox, "Draws a temporary overlay on split candidates while the prompt is visible.");
		SetToolTip(_highlightAssociationCandidatesCheckBox, "Draws a temporary overlay on association candidates while the prompt is visible.");
		SetToolTip(_showAutomaticStepDiagnosticsCheckBox, "Shows popup details when an automatic split or association step fails.");
		SetToolTip(_useCompactDockpaneLayoutCheckBox, "Reduces spacing, button padding, row height, and footer padding in the bottom-docked template editor.");
		SetToolTip(_maxRecentTemplatesTextBox, "Maximum number of recent templates to keep in the dockpane history. Values are clamped from 1 to 50.");
		SetToolTip(_hintSourceColorHexTextBox, "HEX color used for the newly placed/source feature hint, for example #00FF50.");
		SetToolTip(_hintAssociationTargetColorHexTextBox, "HEX color used for association target hints, for example #FF0000.");
		SetToolTip(_hintSplitCandidateColorHexTextBox, "HEX color used for split candidate line hints, for example #FF0000.");
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

	private static Style CreateSettingsTabButtonStyle()
	{
		Style style = new Style(typeof(Button));
		style.Setters.Add(new Setter(Control.ForegroundProperty, PrimaryTextBrush));
		style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
		ControlTemplate template = new ControlTemplate(typeof(Button));
		FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
		border.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
		border.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
		border.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = RelativeSource.TemplatedParent });
		border.SetValue(Border.CornerRadiusProperty, new CornerRadius(3.0, 3.0, 0.0, 0.0));
		FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
		presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
		presenter.SetBinding(ContentPresenter.MarginProperty, new Binding("Padding") { RelativeSource = RelativeSource.TemplatedParent });
		border.AppendChild(presenter);
		template.VisualTree = border;
		style.Setters.Add(new Setter(Control.TemplateProperty, template));

		Trigger hoverTrigger = new Trigger
		{
			Property = UIElement.IsMouseOverProperty,
			Value = true
		};
		hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, TabHoverBackgroundBrush));
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
		style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8.0, 4.0, 8.0, 4.0)));
		style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
		style.Setters.Add(new Setter(Control.TemplateProperty, CreateComboBoxTemplate()));
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
		popupBorder.SetValue(Border.BackgroundProperty, SurfaceBackgroundBrush);
		popupBorder.SetValue(Border.BorderBrushProperty, ControlBorderBrush);
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
		grid.SetValue(FrameworkElement.MinHeightProperty, 28.0);
		grid.SetValue(FrameworkElement.MarginProperty, new Thickness(0.0));
		grid.SetValue(Grid.ColumnProperty, 0);
		FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
		content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
		content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
		content.SetBinding(ContentPresenter.ContentProperty, new Binding("Content") { RelativeSource = RelativeSource.TemplatedParent });
		content.SetBinding(ContentPresenter.MarginProperty, new Binding("Padding") { RelativeSource = RelativeSource.TemplatedParent });
		grid.AppendChild(content);
		FrameworkElementFactory arrow = new FrameworkElementFactory(typeof(TextBlock));
		arrow.SetValue(TextBlock.TextProperty, "v");
		arrow.SetValue(TextBlock.ForegroundProperty, SecondaryTextBrush);
		arrow.SetValue(FrameworkElement.MarginProperty, new Thickness(0.0, 0.0, 8.0, 0.0));
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
		style.Setters.Add(new Setter(Control.BackgroundProperty, SurfaceBackgroundBrush));
		style.Setters.Add(new Setter(Control.BorderBrushProperty, ControlBorderBrush));
		style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1.0)));
		style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
		ControlTemplate template = new ControlTemplate(typeof(CheckBox));
		FrameworkElementFactory panel = new FrameworkElementFactory(typeof(StackPanel));
		panel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
		FrameworkElementFactory box = new FrameworkElementFactory(typeof(Border));
		box.Name = "CheckBoxGlyph";
		box.SetValue(FrameworkElement.WidthProperty, 16.0);
		box.SetValue(FrameworkElement.HeightProperty, 16.0);
		box.SetValue(FrameworkElement.MarginProperty, new Thickness(0.0, 0.0, 8.0, 0.0));
		box.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
		box.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
		box.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = RelativeSource.TemplatedParent });
		FrameworkElementFactory check = new FrameworkElementFactory(typeof(TextBlock));
		check.Name = "CheckMark";
		check.SetValue(TextBlock.TextProperty, "✓");
		check.SetValue(TextBlock.ForegroundProperty, IsDarkTheme ? Brushes.White : Brushes.Black);
		check.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
		check.SetValue(TextBlock.FontSizeProperty, 15.0);
		check.SetValue(TextBlock.LineHeightProperty, 16.0);
		check.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
		check.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		check.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		check.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
		box.AppendChild(check);
		panel.AppendChild(box);
		FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
		content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
		content.SetBinding(ContentPresenter.ContentProperty, new Binding("Content") { RelativeSource = RelativeSource.TemplatedParent });
		panel.AppendChild(content);
		template.VisualTree = panel;
		Trigger checkedTrigger = new Trigger
		{
			Property = ToggleButton.IsCheckedProperty,
			Value = true
		};
		checkedTrigger.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible, "CheckMark"));
		checkedTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, AccentBorderBrush, "CheckBoxGlyph"));
		template.Triggers.Add(checkedTrigger);
		style.Setters.Add(new Setter(Control.TemplateProperty, template));
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

	private static Style CreatePrimaryButtonStyle()
	{
		Style style = CreateButtonStyle();
		style.Setters.Add(new Setter(Control.BackgroundProperty, AccentBorderBrush));
		style.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
		style.Setters.Add(new Setter(Control.BorderBrushProperty, AccentBorderBrush));
		Trigger hoverTrigger = new Trigger
		{
			Property = UIElement.IsMouseOverProperty,
			Value = true
		};
		hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, AccentButtonHoverBrush));
		hoverTrigger.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
		hoverTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, AccentButtonHoverBrush));
		style.Triggers.Add(hoverTrigger);
		return style;
	}

	private sealed class PlacementOverrideEditorRow
	{
		public PlacementOverrideEditorRow(
			PlacementAttributeOverrideEditorState state,
			Border container,
			CheckBox enabledCheckBox,
			ComboBox valueComboBox,
			TextBox valueTextBox)
		{
			State = state;
			Container = container;
			EnabledCheckBox = enabledCheckBox;
			ValueComboBox = valueComboBox;
			ValueTextBox = valueTextBox;
		}

		public PlacementAttributeOverrideEditorState State { get; }

		public Border Container { get; }

		public CheckBox EnabledCheckBox { get; }

		public ComboBox ValueComboBox { get; }

		public TextBox ValueTextBox { get; }

		public bool UseDropDown => ValueComboBox != null;
	}
}
