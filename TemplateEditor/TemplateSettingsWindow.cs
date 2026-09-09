using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;

namespace TemplateEditor;

internal sealed class TemplateSettingsWindow : Window
{
	private sealed class PlacementOverrideEditorRow
	{
		public PlacementAttributeOverrideEditorState State { get; }

		public Border Container { get; }

		public CheckBox EnabledCheckBox { get; }

		public ComboBox ValueComboBox { get; }

		public TextBox ValueTextBox { get; }

		public bool UseDropDown => ValueComboBox != null;

		public PlacementOverrideEditorRow(PlacementAttributeOverrideEditorState state, Border container, CheckBox enabledCheckBox, ComboBox valueComboBox, TextBox valueTextBox)
		{
			State = state;
			Container = container;
			EnabledCheckBox = enabledCheckBox;
			ValueComboBox = valueComboBox;
			ValueTextBox = valueTextBox;
		}
	}

	private static readonly Brush WindowBackgroundBrush;

	private static readonly Brush SurfaceBackgroundBrush;

	private static readonly Brush SectionBorderBrush;

	private static readonly Brush ControlBorderBrush;

	private static readonly Brush TabBackgroundBrush;

	private static readonly Brush TabHoverBackgroundBrush;

	private static readonly Brush TabSelectedBackgroundBrush;

	private static readonly Brush PrimaryTextBrush;

	private static readonly Brush SecondaryTextBrush;

	private static readonly Brush AccentBorderBrush;

	private static readonly Brush AccentButtonHoverBrush;

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

	private bool _isLoadingAttributeOverrideEditor;

	public TemplateEditorSettings Settings { get; }

	public TemplateSettingsWindow(TemplateEditorSettings settings)
	{
		Settings = settings.Clone();
		base.Title = "Template Settings";
		base.Width = 900.0;
		base.Height = 680.0;
		base.MinWidth = 760.0;
		base.MinHeight = 560.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		base.ResizeMode = ResizeMode.CanResize;
		base.Background = WindowBackgroundBrush;
		base.Foreground = PrimaryTextBrush;
		base.FontFamily = new FontFamily("Segoe UI");
		base.FontSize = 12.0;
		base.Resources[SystemColors.ControlTextBrushKey] = PrimaryTextBrush;
		base.Resources[SystemColors.WindowTextBrushKey] = PrimaryTextBrush;
		base.Resources[SystemColors.GrayTextBrushKey] = SecondaryTextBrush;
		base.Resources[SystemColors.ControlBrushKey] = WindowBackgroundBrush;
		base.Resources[SystemColors.WindowBrushKey] = SurfaceBackgroundBrush;
		base.Resources[SystemColors.HighlightBrushKey] = TabHoverBackgroundBrush;
		base.Resources[SystemColors.HighlightTextBrushKey] = PrimaryTextBrush;
		base.Resources[SystemColors.ControlLightBrushKey] = SurfaceBackgroundBrush;
		base.Resources[SystemColors.ControlDarkBrushKey] = SectionBorderBrush;
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
		base.Content = DialogAppearance.WithChrome(this, "Template Settings", BuildContent());
	}

	private UIElement BuildContent()
	{
		DockPanel dockPanel = new DockPanel
		{
			LastChildFill = true
		};
		Border element = new Border
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
		DockPanel.SetDock(element, Dock.Top);
		dockPanel.Children.Add(element);
		Border element2 = new Border
		{
			Background = WindowBackgroundBrush,
			BorderBrush = SectionBorderBrush,
			BorderThickness = new Thickness(0.0, 1.0, 0.0, 0.0),
			Padding = new Thickness(12.0),
			Child = BuildButtonsRow()
		};
		DockPanel.SetDock(element2, Dock.Bottom);
		dockPanel.Children.Add(element2);
		dockPanel.Children.Add(BuildSettingsTabs());
		return dockPanel;
	}

	private UIElement BuildSettingsTabs()
	{
		DockPanel dockPanel = new DockPanel
		{
			LastChildFill = true,
			Margin = new Thickness(12.0)
		};
		Border contentFrame = new Border
		{
			Background = SurfaceBackgroundBrush,
			BorderBrush = SectionBorderBrush,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(0.0),
			MinHeight = 360.0
		};
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(0.0, 0.0, 0.0, -1.0)
		};
		DockPanel.SetDock(stackPanel, Dock.Top);
		dockPanel.Children.Add(stackPanel);
		dockPanel.Children.Add(contentFrame);
		List<Button> buttons = new List<Button>();
		Button selectedTabButton = null;
		UIElement generalContent = BuildGeneralTab();
		UIElement lineSplitContent = BuildLineSplitTab();
		UIElement parallelCopyContent = BuildParallelCopyTab();
		UIElement associationsContent = BuildAssociationTab();
		UIElement attributeOverridesContent = BuildAttributeOverridesTab();
		UIElement interfaceContent = BuildInterfaceTab();
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
		generalButton.Click += delegate
		{
			SelectTab(generalButton, generalContent);
		};
		lineSplitButton.Click += delegate
		{
			SelectTab(lineSplitButton, lineSplitContent);
		};
		parallelCopyButton.Click += delegate
		{
			SelectTab(parallelCopyButton, parallelCopyContent);
		};
		associationsButton.Click += delegate
		{
			SelectTab(associationsButton, associationsContent);
		};
		attributeOverridesButton.Click += delegate
		{
			SelectTab(attributeOverridesButton, attributeOverridesContent);
		};
		interfaceButton.Click += delegate
		{
			SelectTab(interfaceButton, interfaceContent);
		};
		foreach (Button item in buttons)
		{
			stackPanel.Children.Add(item);
		}
		SelectTab(generalButton, generalContent);
		return dockPanel;
		void SelectTab(Button selectedButton, UIElement content)
		{
			if (selectedTabButton != selectedButton)
			{
				contentFrame.Child = null;
				foreach (Button item2 in buttons)
				{
					item2.Background = TabBackgroundBrush;
					item2.BorderBrush = SectionBorderBrush;
					item2.FontWeight = FontWeights.Normal;
				}
				selectedButton.Background = SurfaceBackgroundBrush;
				selectedButton.BorderBrush = SectionBorderBrush;
				selectedButton.FontWeight = FontWeights.SemiBold;
				selectedTabButton = selectedButton;
				contentFrame.Child = content;
			}
		}
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
		StackPanel stackPanel = CreateTabPanel();
		stackPanel.Children.Add(CreateGroupBox("Template Configuration", BuildTemplateConfigSection()));
		stackPanel.Children.Add(CreateGroupBox("Placement Safety", CreateCheckBoxPanel(_preventDefaultVersionPlacementCheckBox)));
		return WrapTab(stackPanel);
	}

	private UIElement BuildLineSplitTab()
	{
		StackPanel stackPanel = CreateTabPanel();
		stackPanel.Children.Add(CreateGroupBox("Behavior", CreateCheckBoxPanel(_enableLineSplitPromptsCheckBox, _enablePointPlacementSplitPromptCheckBox, _enableLineEndpointSplitPromptCheckBox, _enableSplitAtLineStartPointCheckBox, _enableSplitAtLineEndPointCheckBox, _enableConfiguredLinePartSplitsCheckBox, _suppressDuplicateSplitPromptsCheckBox, _splitOnlyInteriorCandidatesCheckBox)));
		stackPanel.Children.Add(CreateGroupBox("Prompting", CreateFormGrid(("Split prompt mode", _splitPromptModeComboBox), ("Maximum split candidates to review", _maxSplitCandidatesToReviewTextBox))));
		stackPanel.Children.Add(CreateGroupBox("Eligible Groups", CreateFormGrid(("Split search distance (map units)", _splitSearchDistanceTextBox), ("Eligible point placement groups", _splitPointPlacementGroupsTextBox), ("Eligible line placement groups", _splitLinePlacementGroupsTextBox), ("Underlying target line groups", _splitTargetLineGroupsTextBox), ("Underlying target subtype/layer names", _splitTargetLayerNamesTextBox))));
		return WrapTab(stackPanel);
	}

	private UIElement BuildParallelCopyTab()
	{
		StackPanel stackPanel = CreateTabPanel();
		stackPanel.Children.Add(CreateGroupBox("Behavior", CreateCheckBoxPanel(_enableParallelCopyPromptCheckBox, _enableMultiSegmentParallelCopyCheckBox, _requireConnectedParallelCopySpanCheckBox, _defaultParallelCopyLeftSideCheckBox, _rememberLastParallelCopyOptionsCheckBox, _autoCreateParallelCopyWhenSelectedLineExistsCheckBox)));
		stackPanel.Children.Add(CreateGroupBox("Defaults", CreateFormGrid(("Endpoint match tolerance (map units)", _parallelCopyEndpointMatchToleranceTextBox), ("Default offset distance", _defaultParallelCopyOffsetDistanceTextBox))));
		return WrapTab(stackPanel);
	}

	private UIElement BuildAssociationTab()
	{
		StackPanel stackPanel = CreateTabPanel();
		stackPanel.Children.Add(CreateGroupBox("Behavior", CreateCheckBoxPanel(_enableAssociationPromptsCheckBox, _enableStructuralAttachmentPromptsCheckBox, _enableJunctionJunctionConnectivityPromptsCheckBox, _enableContainmentPointPromptsCheckBox, _enableContainmentBoundaryPromptsCheckBox, _enableLineAssociationPromptsCheckBox, _enableLineStructuralAttachmentPromptsCheckBox, _enableLineContainmentPointPromptsCheckBox, _enableLineContainmentBoundaryPromptsCheckBox, _stopAfterFirstSuccessfulAssociationCheckBox)));
		stackPanel.Children.Add(CreateGroupBox("Prompting", CreateFormGrid(("Association prompt mode", _associationPromptModeComboBox), ("Configured association mode", _configuredAssociationPlacementModeComboBox))));
		stackPanel.Children.Add(CreateGroupBox("Search Distances", CreateFormGrid(("Structural attachment search distance", _structuralAttachmentSearchDistanceTextBox), ("Junction-junction connectivity search distance", _junctionJunctionConnectivitySearchDistanceTextBox), ("Containment point search distance", _containmentPointSearchDistanceTextBox), ("Structure container search distance", _containmentBoundarySearchDistanceTextBox))));
		stackPanel.Children.Add(CreateGroupBox("Fallback Eligible Groups", CreateFormGrid(("Eligible placement groups", _associationPlacementGroupsTextBox), ("Structural attachment target groups", _structuralAttachmentTargetGroupsTextBox), ("Structural attachment subtype/layer names", _structuralAttachmentTargetLayerNamesTextBox), ("Junction-junction connectivity target groups", _junctionJunctionConnectivityTargetGroupsTextBox), ("Junction-junction connectivity subtype/layer names", _junctionJunctionConnectivityTargetLayerNamesTextBox), ("Containment target point groups", _containmentPointTargetGroupsTextBox), ("Containment target point subtype/layer names", _containmentPointTargetLayerNamesTextBox), ("Structure container target groups", _containmentBoundaryTargetGroupsTextBox), ("Structure container subtype/layer names", _containmentBoundaryTargetLayerNamesTextBox))));
		stackPanel.Children.Add(CreateGroupBox("Rule Catalog", BuildAssociationRuleCatalogSection()));
		return WrapTab(stackPanel);
	}

	private UIElement BuildAssociationRuleCatalogSection()
	{
		StackPanel stackPanel = new StackPanel();
		Grid grid = new Grid
		{
			Margin = new Thickness(0.0, 4.0, 0.0, 8.0)
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
		TextBlock element = CreateLabel("Association rules JSON path");
		Grid.SetColumnSpan(element, 2);
		grid.Children.Add(element);
		Grid.SetRow(_associationRulesJsonPathTextBox, 1);
		grid.Children.Add(_associationRulesJsonPathTextBox);
		Button button = new Button
		{
			Content = "Browse...",
			MinWidth = 90.0,
			Margin = new Thickness(8.0, 22.0, 0.0, 0.0),
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0),
			Style = CreateButtonStyle()
		};
		button.Click += BrowseAssociationRulesJsonPath_Click;
		Grid.SetRow(button, 1);
		Grid.SetColumn(button, 1);
		grid.Children.Add(button);
		stackPanel.Children.Add(grid);
		stackPanel.Children.Add(_regenerateAssociationRulesButton);
		return stackPanel;
	}

	private UIElement BuildInterfaceTab()
	{
		StackPanel stackPanel = CreateTabPanel();
		stackPanel.Children.Add(CreateGroupBox("Dockpane Layout", CreateFormGrid(("Use compact dockpane layout", _useCompactDockpaneLayoutCheckBox), ("Maximum recent templates", _maxRecentTemplatesTextBox))));
		stackPanel.Children.Add(CreateGroupBox("Map Feedback", CreateCheckBoxPanel(_highlightSplitCandidatesCheckBox, _highlightAssociationCandidatesCheckBox, _showAutomaticStepDiagnosticsCheckBox)));
		stackPanel.Children.Add(CreateGroupBox("Hint Colors", CreateFormGrid(("Placed/source feature color (HEX)", _hintSourceColorHexTextBox), ("Association target color (HEX)", _hintAssociationTargetColorHexTextBox), ("Split candidate color (HEX)", _hintSplitCandidateColorHexTextBox))));
		return WrapTab(stackPanel);
	}

	private UIElement BuildAttributeOverridesTab()
	{
		StackPanel stackPanel = CreateTabPanel();
		stackPanel.Children.Add(CreateGroupBox("Session Overrides", BuildAttributeOverrideSection()));
		return WrapTab(stackPanel);
	}

	private UIElement BuildAttributeOverrideSection()
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Children.Add(new TextBlock
		{
			Text = "Choose workflow-wide attribute overrides. These apply only to configured fields and can be superseded by the next-placement override dialog from the template right-click menu.",
			Foreground = SecondaryTextBrush,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0)
		});
		StackPanel editorPanel = new StackPanel();
		editorPanel.Children.Add(new TextBlock
		{
			Text = "Loading placement override fields...",
			Foreground = SecondaryTextBrush
		});
		stackPanel.Children.Add(editorPanel);
		stackPanel.Loaded += delegate
		{
			if (_isLoadingAttributeOverrideEditor)
			{
				return;
			}
			_isLoadingAttributeOverrideEditor = true;
			TaskObservationService.Forget(LoadAttributeOverrideEditorAsync(editorPanel), "Placement override editor fields could not be loaded.");
		};
		return stackPanel;
	}

	private async Task LoadAttributeOverrideEditorAsync(StackPanel editorPanel)
	{
		try
		{
			IReadOnlyList<PlacementAttributeOverrideEditorState> readOnlyList = await PlacementAttributeOverrideService.BuildSessionEditorStatesAsync(Settings.SessionAttributeOverrides);
			_sessionOverrideRows.Clear();
			editorPanel.Children.Clear();
		if (readOnlyList.Count == 0)
		{
			editorPanel.Children.Add(new TextBlock
			{
				Text = "No packaged placement override fields are currently available.",
				Foreground = SecondaryTextBrush,
				TextWrapping = TextWrapping.Wrap
			});
			return;
		}
		foreach (PlacementAttributeOverrideEditorState item in readOnlyList)
		{
			PlacementOverrideEditorRow placementOverrideEditorRow = CreatePlacementOverrideEditorRow(item);
			_sessionOverrideRows.Add(placementOverrideEditorRow);
			editorPanel.Children.Add(placementOverrideEditorRow.Container);
		}
		}
		catch (Exception ex)
		{
			LogService.LogException("Placement override editor fields could not be loaded.", ex);
			editorPanel.Children.Clear();
			editorPanel.Children.Add(new TextBlock
			{
				Text = "Placement override fields could not be loaded. See the Template Editor log for details.",
				Foreground = SecondaryTextBrush,
				TextWrapping = TextWrapping.Wrap
			});
		}
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
		TextBlock element = CreateLabel("Template config file");
		Grid.SetColumnSpan(element, 2);
		grid.Children.Add(element);
		Grid.SetRow(_templateConfigPathTextBox, 1);
		grid.Children.Add(_templateConfigPathTextBox);
		Button button = new Button
		{
			Content = "Browse...",
			MinWidth = 90.0,
			Margin = new Thickness(8.0, 22.0, 0.0, 0.0),
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0),
			Style = CreateButtonStyle()
		};
		button.Click += delegate
		{
			string text = AddinConfiguration.PromptForTemplateConfigFilePath(_templateConfigPathTextBox.Text);
			if (!string.IsNullOrWhiteSpace(text))
			{
				_templateConfigPathTextBox.Text = text;
			}
		};
		Grid.SetRow(button, 1);
		Grid.SetColumn(button, 1);
		grid.Children.Add(button);
		StackPanel stackPanel = new StackPanel();
		stackPanel.Children.Add(grid);
		stackPanel.Children.Add(_validateConfigCheckBox);
		return stackPanel;
	}

	private static StackPanel CreateCheckBoxPanel(params CheckBox[] checkBoxes)
	{
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(0.0, 4.0, 0.0, 0.0)
		};
		foreach (CheckBox element in checkBoxes)
		{
			stackPanel.Children.Add(element);
		}
		return stackPanel;
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
			TextBlock textBlock = CreateLabel(rows[i].Label);
			textBlock.VerticalAlignment = VerticalAlignment.Center;
			textBlock.Margin = new Thickness(0.0, 0.0, 12.0, 8.0);
			textBlock.TextWrapping = TextWrapping.Wrap;
			textBlock.TextTrimming = TextTrimming.None;
			Grid.SetRow(textBlock, i);
			grid.Children.Add(textBlock);
			Control item = rows[i].Control;
			item.Margin = new Thickness(0.0, 0.0, 0.0, 8.0);
			ApplyControlSizing(rows[i].Label, item);
			Grid.SetRow(item, i);
			Grid.SetColumn(item, 1);
			grid.Children.Add(item);
		}
		return grid;
	}

	private static void ApplyControlSizing(string label, Control control)
	{
		string text = label?.ToUpperInvariant() ?? string.Empty;
		if (control is TextBox textBox && (text.Contains("GROUP") || text.Contains("LAYER") || text.Contains("SUBTYPE")))
		{
			textBox.AcceptsReturn = true;
			textBox.TextWrapping = TextWrapping.Wrap;
			textBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
			textBox.MinHeight = 56.0;
		}
		else if (text.Contains("DISTANCE") || text.Contains("TOLERANCE") || text.Contains("MAXIMUM") || text.Contains("OFFSET") || text.Contains("HEX"))
		{
			control.Width = 180.0;
			control.HorizontalAlignment = HorizontalAlignment.Left;
		}
		else if (control is ComboBox)
		{
			control.Width = 280.0;
			control.HorizontalAlignment = HorizontalAlignment.Left;
		}
	}

	private static Border CreateGroupBox(string header, UIElement content)
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Children.Add(new TextBlock
		{
			Text = header,
			Foreground = PrimaryTextBrush,
			FontWeight = FontWeights.SemiBold,
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		});
		stackPanel.Children.Add(new Border
		{
			Height = 1.0,
			Background = SectionBorderBrush,
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		});
		stackPanel.Children.Add(content);
		return new Border
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0),
			Padding = new Thickness(14.0),
			Background = SurfaceBackgroundBrush,
			BorderBrush = SectionBorderBrush,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(0.0),
			Child = stackPanel
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
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		Button button = new Button
		{
			Content = "Cancel",
			MinWidth = 88.0,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0),
			IsCancel = true,
			Style = CreateButtonStyle()
		};
		button.Click += delegate
		{
			base.DialogResult = false;
			Close();
		};
		Button button2 = new Button
		{
			Content = "OK",
			MinWidth = 88.0,
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0),
			IsDefault = true,
			Style = CreatePrimaryButtonStyle()
		};
		button2.Click += SaveButton_Click;
		stackPanel.Children.Add(button);
		stackPanel.Children.Add(button2);
		return stackPanel;
	}

	private void SaveButton_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			Settings.TemplateConfigFilePath = (string.IsNullOrWhiteSpace(_templateConfigPathTextBox.Text) ? null : _templateConfigPathTextBox.Text.Trim());
			if (!string.IsNullOrWhiteSpace(Settings.TemplateConfigFilePath) && !File.Exists(Settings.TemplateConfigFilePath))
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
			Settings.AssociationRulesJsonPath = (string.IsNullOrWhiteSpace(_associationRulesJsonPathTextBox.Text) ? null : AtomicFileService.NormalizeJsonFilePath(_associationRulesJsonPathTextBox.Text));
			Settings.SessionAttributeOverrides = _sessionOverrideRows.Select((PlacementOverrideEditorRow row) => new PlacementAttributeOverrideValue
			{
				FieldName = row.State.Definition.FieldName,
				Enabled = (row.EnabledCheckBox.IsChecked == true),
				Value = (row.UseDropDown ? (row.ValueComboBox.SelectedItem as string) : row.ValueTextBox.Text)
			}).ToList();
			Settings.Normalize();
			base.DialogResult = true;
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
		string text = (string.IsNullOrWhiteSpace(_associationRulesJsonPathTextBox.Text) ? AssociationRuleCatalog.RuleFilePath : _associationRulesJsonPathTextBox.Text.Trim());
		if (!string.IsNullOrWhiteSpace(text))
		{
			string directoryName = Path.GetDirectoryName(text);
			if (!string.IsNullOrWhiteSpace(directoryName) && Directory.Exists(directoryName))
			{
				saveFileDialog.InitialDirectory = directoryName;
			}
			string fileName = Path.GetFileName(text);
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
		string requestedPath = (string.IsNullOrWhiteSpace(_associationRulesJsonPathTextBox.Text) ? AssociationRuleCatalog.RuleFilePath : _associationRulesJsonPathTextBox.Text.Trim());
		string outputPath;
		try
		{
			outputPath = AtomicFileService.NormalizeJsonFilePath(requestedPath);
		}
		catch (Exception ex)
		{
			DialogService.Show(ex.Message, "Template Editor");
			return;
		}
		if (DialogService.Show("Regenerate the association rule JSON from the utility network in the active map?\n\nThe following file will be replaced:\n" + outputPath, "Template Editor", new DialogButtonChoice("Regenerate", MessageBoxResult.Yes, isPrimary: true), new DialogButtonChoice("Cancel", MessageBoxResult.No, isPrimary: false, isCancel: true)) != MessageBoxResult.Yes)
		{
			return;
		}
		_regenerateAssociationRulesButton.IsEnabled = false;
		object originalContent = _regenerateAssociationRulesButton.Content;
		_regenerateAssociationRulesButton.Content = "Regenerating...";
		try
		{
			AssociationRuleGenerationResult result = await AssociationRuleJsonRegenerator.RegenerateFromActiveMapAsync(outputPath);
			Settings.AssociationRulesJsonPath = result.OutputPath;
			_associationRulesJsonPathTextBox.Text = result.OutputPath;
			DialogService.Show($"Regenerated association rules JSON.\n\nRules written: {result.RuleCount}\nFile: {result.OutputPath}", "Template Editor");
		}
		catch (Exception ex2)
		{
			Exception ex3 = ex2;
			DialogService.Show("The association rules JSON could not be regenerated.\n\n" + ex3.Message, "Template Editor");
		}
		finally
		{
			_regenerateAssociationRulesButton.Content = originalContent;
			_regenerateAssociationRulesButton.IsEnabled = true;
		}
	}

	private static double ParseDistance(string text, string label)
	{
		if (!double.TryParse(text, out var result) || result < 0.0)
		{
			throw new InvalidOperationException("Enter a valid non-negative number for " + label + ".");
		}
		return result;
	}

	private static double ParsePositiveDistance(string text, string label)
	{
		if (!double.TryParse(text, out var result) || result <= 0.0)
		{
			throw new InvalidOperationException("Enter a valid positive number for " + label + ".");
		}
		return result;
	}

	private static int ParsePositiveInteger(string text, string label)
	{
		if (!int.TryParse(text, out var result) || result <= 0)
		{
			throw new InvalidOperationException("Enter a valid positive whole number for " + label + ".");
		}
		return result;
	}

	private PlacementOverrideEditorRow CreatePlacementOverrideEditorRow(PlacementAttributeOverrideEditorState state)
	{
		CheckBox checkBox = new CheckBox
		{
			IsChecked = state.IsEnabled,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(0.0, 2.0, 10.0, 0.0)
		};
		TextBlock element = new TextBlock
		{
			Text = state.Definition.Label,
			FontWeight = FontWeights.SemiBold,
			Foreground = PrimaryTextBrush
		};
		TextBlock element2 = new TextBlock
		{
			Text = (string.IsNullOrWhiteSpace(state.Definition.Description) ? state.ConfiguredValueSummary : (state.Definition.Description + "\n" + state.ConfiguredValueSummary)),
			Foreground = SecondaryTextBrush,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 2.0, 0.0, 0.0)
		};
		ComboBox valueComboBox = null;
		TextBox textBox = null;
		Control valueEditor;
		if (state.UseDropDown)
		{
			valueComboBox = (ComboBox)(valueEditor = new ComboBox
			{
				ItemsSource = state.AvailableValues,
				SelectedItem = (state.AvailableValues.FirstOrDefault((string value) => string.Equals(value, state.Value, StringComparison.OrdinalIgnoreCase)) ?? state.AvailableValues.FirstOrDefault()),
				MinWidth = 220.0,
				Margin = new Thickness(0.0, 6.0, 0.0, 0.0),
				Style = CreateComboBoxStyle(),
				ItemContainerStyle = CreateComboBoxItemStyle()
			});
		}
		else
		{
			textBox = CreateTextBox(state.Value ?? string.Empty);
			textBox.MinWidth = 220.0;
			textBox.Margin = new Thickness(0.0, 6.0, 0.0, 0.0);
			valueEditor = textBox;
		}
		valueEditor.IsEnabled = state.IsEnabled;
		checkBox.Checked += delegate
		{
			valueEditor.IsEnabled = true;
		};
		checkBox.Unchecked += delegate
		{
			valueEditor.IsEnabled = false;
		};
		StackPanel stackPanel = new StackPanel();
		stackPanel.Children.Add(element);
		stackPanel.Children.Add(element2);
		stackPanel.Children.Add(valueEditor);
		DockPanel dockPanel = new DockPanel
		{
			LastChildFill = true
		};
		DockPanel.SetDock(checkBox, Dock.Left);
		dockPanel.Children.Add(checkBox);
		dockPanel.Children.Add(stackPanel);
		Border container = new Border
		{
			Background = SurfaceBackgroundBrush,
			BorderBrush = SectionBorderBrush,
			BorderThickness = new Thickness(1.0),
			Padding = new Thickness(10.0),
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0),
			Child = dockPanel
		};
		return new PlacementOverrideEditorRow(state, container, checkBox, valueComboBox, textBox);
	}

	private static TextBox CreateTextBox(string text)
	{
		return new TextBox
		{
			Text = (text ?? string.Empty),
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
		for (int i = 0; i < items.Length; i++)
		{
			var (content, tag) = items[i];
			comboBox.Items.Add(new ComboBoxItem
			{
				Content = content,
				Tag = tag
			});
		}
		foreach (ComboBoxItem item in (IEnumerable)comboBox.Items)
		{
			if (string.Equals(Convert.ToString(item.Tag), selectedValue, StringComparison.OrdinalIgnoreCase))
			{
				comboBox.SelectedItem = item;
				break;
			}
		}
		comboBox.SelectedIndex = ((comboBox.SelectedIndex >= 0 || comboBox.Items.Count <= 0) ? comboBox.SelectedIndex : 0);
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
		ControlTemplate controlTemplate = new ControlTemplate(typeof(Button));
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
		frameworkElementFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(0.0));
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
			Property = UIElement.IsMouseOverProperty,
			Value = true
		};
		trigger.Setters.Add(new Setter(Control.BackgroundProperty, TabHoverBackgroundBrush));
		style.Triggers.Add(trigger);
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
				(SetterBase)new Setter(Control.BackgroundProperty, SurfaceBackgroundBrush),
				(SetterBase)new Setter(Control.ForegroundProperty, PrimaryTextBrush),
				(SetterBase)new Setter(Control.BorderBrushProperty, AccentBorderBrush)
			}
		});
		style.Triggers.Add(new Trigger
		{
			Property = UIElement.IsKeyboardFocusedProperty,
			Value = true,
			Setters = 
			{
				(SetterBase)new Setter(Control.BackgroundProperty, SurfaceBackgroundBrush),
				(SetterBase)new Setter(Control.ForegroundProperty, PrimaryTextBrush),
				(SetterBase)new Setter(Control.BorderBrushProperty, AccentBorderBrush)
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
		frameworkElementFactory4.SetValue(Border.BackgroundProperty, SurfaceBackgroundBrush);
		frameworkElementFactory4.SetValue(Border.BorderBrushProperty, ControlBorderBrush);
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
		frameworkElementFactory2.SetValue(FrameworkElement.MinHeightProperty, 28.0);
		frameworkElementFactory2.SetValue(FrameworkElement.MarginProperty, new Thickness(0.0));
		frameworkElementFactory2.SetValue(Grid.ColumnProperty, 0);
		FrameworkElementFactory frameworkElementFactory3 = new FrameworkElementFactory(typeof(ContentPresenter));
		frameworkElementFactory3.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		frameworkElementFactory3.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
		frameworkElementFactory3.SetBinding(ContentPresenter.ContentProperty, new Binding("Content")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		frameworkElementFactory3.SetBinding(FrameworkElement.MarginProperty, new Binding("Padding")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		frameworkElementFactory2.AppendChild(frameworkElementFactory3);
		FrameworkElementFactory frameworkElementFactory4 = new FrameworkElementFactory(typeof(TextBlock));
		frameworkElementFactory4.SetValue(TextBlock.TextProperty, "v");
		frameworkElementFactory4.SetValue(TextBlock.ForegroundProperty, SecondaryTextBrush);
		frameworkElementFactory4.SetValue(FrameworkElement.MarginProperty, new Thickness(0.0, 0.0, 8.0, 0.0));
		frameworkElementFactory4.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
		frameworkElementFactory4.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		frameworkElementFactory2.AppendChild(frameworkElementFactory4);
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		controlTemplate.VisualTree = frameworkElementFactory;
		return controlTemplate;
	}

	private static Style CreateComboBoxItemStyle()
	{
		Style style = new Style(typeof(ComboBoxItem));
		style.Setters.Add(new Setter(Control.BackgroundProperty, SurfaceBackgroundBrush));
		style.Setters.Add(new Setter(Control.ForegroundProperty, PrimaryTextBrush));
		style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8.0, 4.0, 8.0, 4.0)));
		Trigger trigger = new Trigger
		{
			Property = ListBoxItem.IsSelectedProperty,
			Value = true
		};
		trigger.Setters.Add(new Setter(Control.BackgroundProperty, TabHoverBackgroundBrush));
		trigger.Setters.Add(new Setter(Control.ForegroundProperty, PrimaryTextBrush));
		style.Triggers.Add(trigger);
		Trigger trigger2 = new Trigger
		{
			Property = UIElement.IsMouseOverProperty,
			Value = true
		};
		trigger2.Setters.Add(new Setter(Control.BackgroundProperty, TabHoverBackgroundBrush));
		trigger2.Setters.Add(new Setter(Control.ForegroundProperty, PrimaryTextBrush));
		style.Triggers.Add(trigger2);
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
		ControlTemplate controlTemplate = new ControlTemplate(typeof(CheckBox));
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(StackPanel));
		frameworkElementFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory2.Name = "CheckBoxGlyph";
		frameworkElementFactory2.SetValue(FrameworkElement.WidthProperty, 16.0);
		frameworkElementFactory2.SetValue(FrameworkElement.HeightProperty, 16.0);
		frameworkElementFactory2.SetValue(FrameworkElement.MarginProperty, new Thickness(0.0, 0.0, 8.0, 0.0));
		frameworkElementFactory2.SetBinding(Border.BackgroundProperty, new Binding("Background")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		frameworkElementFactory2.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		frameworkElementFactory2.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		FrameworkElementFactory frameworkElementFactory3 = new FrameworkElementFactory(typeof(TextBlock));
		frameworkElementFactory3.Name = "CheckMark";
		frameworkElementFactory3.SetValue(TextBlock.TextProperty, "✓");
		frameworkElementFactory3.SetValue(TextBlock.ForegroundProperty, PrimaryTextBrush);
		frameworkElementFactory3.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
		frameworkElementFactory3.SetValue(TextBlock.FontSizeProperty, 15.0);
		frameworkElementFactory3.SetValue(TextBlock.LineHeightProperty, 16.0);
		frameworkElementFactory3.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
		frameworkElementFactory3.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		frameworkElementFactory3.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		frameworkElementFactory3.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
		frameworkElementFactory2.AppendChild(frameworkElementFactory3);
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		FrameworkElementFactory frameworkElementFactory4 = new FrameworkElementFactory(typeof(ContentPresenter));
		frameworkElementFactory4.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		frameworkElementFactory4.SetBinding(ContentPresenter.ContentProperty, new Binding("Content")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		frameworkElementFactory.AppendChild(frameworkElementFactory4);
		controlTemplate.VisualTree = frameworkElementFactory;
		Trigger trigger = new Trigger
		{
			Property = ToggleButton.IsCheckedProperty,
			Value = true
		};
		trigger.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible, "CheckMark"));
		trigger.Setters.Add(new Setter(Border.BorderBrushProperty, AccentBorderBrush, "CheckBoxGlyph"));
		controlTemplate.Triggers.Add(trigger);
		style.Setters.Add(new Setter(Control.TemplateProperty, controlTemplate));
		style.Triggers.Add(new Trigger
		{
			Property = UIElement.IsMouseOverProperty,
			Value = true,
			Setters = 
			{
				(SetterBase)new Setter(Control.ForegroundProperty, PrimaryTextBrush),
				(SetterBase)new Setter(Control.BorderBrushProperty, AccentBorderBrush)
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
		DialogAppearance.ApplySquareButtonTemplate(style);
		Trigger trigger = new Trigger
		{
			Property = UIElement.IsMouseOverProperty,
			Value = true
		};
		trigger.Setters.Add(new Setter(Control.BackgroundProperty, TabHoverBackgroundBrush));
		trigger.Setters.Add(new Setter(Control.ForegroundProperty, PrimaryTextBrush));
		trigger.Setters.Add(new Setter(Control.BorderBrushProperty, AccentBorderBrush));
		style.Triggers.Add(trigger);
		Trigger trigger2 = new Trigger
		{
			Property = UIElement.IsEnabledProperty,
			Value = false
		};
		trigger2.Setters.Add(new Setter(Control.ForegroundProperty, SystemColors.GrayTextBrush));
		style.Triggers.Add(trigger2);
		return style;
	}

	private static Style CreatePrimaryButtonStyle()
	{
		Style style = CreateButtonStyle();
		style.Setters.Add(new Setter(Control.BackgroundProperty, AccentBorderBrush));
		style.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
		style.Setters.Add(new Setter(Control.BorderBrushProperty, AccentBorderBrush));
		Trigger trigger = new Trigger
		{
			Property = UIElement.IsMouseOverProperty,
			Value = true
		};
		trigger.Setters.Add(new Setter(Control.BackgroundProperty, AccentButtonHoverBrush));
		trigger.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
		trigger.Setters.Add(new Setter(Control.BorderBrushProperty, AccentButtonHoverBrush));
		style.Triggers.Add(trigger);
		return style;
	}

	static TemplateSettingsWindow()
	{
		WindowBackgroundBrush = DialogAppearance.Background;
		SurfaceBackgroundBrush = DialogAppearance.InputBackground;
		SectionBorderBrush = DialogAppearance.SectionBorder;
		ControlBorderBrush = DialogAppearance.ControlBorder;
		TabBackgroundBrush = DialogAppearance.ButtonBackground;
		TabHoverBackgroundBrush = DialogAppearance.ButtonHoverBackground;
		TabSelectedBackgroundBrush = SurfaceBackgroundBrush;
		PrimaryTextBrush = DialogAppearance.Foreground;
		SecondaryTextBrush = DialogAppearance.SecondaryForeground;
		AccentBorderBrush = DialogAppearance.Accent;
		AccentButtonHoverBrush = DialogAppearance.AccentHover;
	}
}
