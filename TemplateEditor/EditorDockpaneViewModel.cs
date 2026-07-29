using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;

namespace TemplateEditor;

internal class EditorDockpaneViewModel : DockPane
{
	private const string _dockPaneID = "TemplateEditor_EditorDockpane";

	internal const string ReadyPlacementStatus = "Ready. Please select a template to place.";

	private List<DisplayTemplate> _simpleTemplates;

	private List<DisplayTemplate> _groupTemplates;

	private List<DisplayTemplate> _allTemplates;

	private DisplayTemplate _selectedTemplate;

	private string _searchText;

	private bool _showGroupTemplates;

	private bool _showSimpleTemplates;

	private bool _showAllTemplates;

	private bool _showFavouriteTemplates;

	private bool _showRecentTemplates;

	private string _sortField = "Name";

	private bool _sortAscending = true;

	private int _activationVersion;

	private bool _isApplyingMirrorPlacementSelection;

	private List<DisplayTemplate> _favouriteTemplates;

	private List<DisplayTemplate> _recentTemplates;

	private Dictionary<string, DisplayTemplate> _allTemplatesByKey;

	private string _placementStatus = ReadyPlacementStatus;

	public List<DisplayTemplate> Templates { get; set; }

	public string TemplateCount { get; set; }

	public string SelectedTemplateStatus => SelectedTemplate == null ? "No template selected" : "Selected: " + SelectedTemplate.DisplayName;

	public string PlacementStatus => _placementStatus;

	public bool IsContinuousPlacementEnabled => AddinConfiguration.Settings?.EnableContinuousPlacementMode == true;

	public string PlacementOptionsStatus
	{
		get
		{
			TemplateEditorSettings settings = AddinConfiguration.Settings;
			if (settings == null)
			{
				return string.Empty;
			}
			List<string> options = new List<string>();
			options.Add(settings.PreventDefaultVersionPlacement ? "DEFAULT blocked" : "DEFAULT allowed");
			options.Add(settings.EnableAssociationPrompts ? "Associations on" : "Associations off");
			options.Add(settings.EnableLineSplitPrompts ? "Splits on" : "Splits off");
			if (settings.EnableContinuousPlacementMode)
			{
				options.Add("Continuous on");
			}
			if (AddinConfiguration.PlacementMirrorMode != PlacementMirrorMode.None)
			{
				options.Add("Mirror " + GetMirrorModeLabel(AddinConfiguration.PlacementMirrorMode));
			}
			string overrideStatus = PlacementAttributeOverrideService.GetStatusLabel();
			if (!string.IsNullOrWhiteSpace(overrideStatus))
			{
				options.Add(overrideStatus);
			}
			return string.Join(" | ", options);
		}
	}

	public ICommand SortCommand { get; }

	public ICommand ClearSearchCommand { get; }

	public ICommand ActivateSelectedTemplateCommand { get; }

	public ICommand ActivateChildTemplateCommand { get; }

	public ICommand ToggleGroupExpansionCommand { get; }

	public ICommand DeactivateTemplateCommand { get; }

	public ICommand ReloadConfigCommand { get; }

	public ICommand ToggleFavouriteCommand { get; }

	public ICommand ActivateContinuousPlacementCommand { get; }

	public ICommand StopContinuousPlacementCommand { get; }

	public ICommand ActivateMirrorPlacementCommand { get; }

	public ICommand PlaceWithOverridesCommand { get; }

	public DisplayTemplate SelectedTemplate
	{
		get
		{
			return _selectedTemplate;
		}
		set
		{
			if (Equals(_selectedTemplate, value))
			{
				return;
			}
			SelectTemplate(value, resetMirrorMode: !_isApplyingMirrorPlacementSelection);
		}
	}

	private void SelectTemplate(DisplayTemplate template, bool resetMirrorMode = true, bool activateTemplate = true)
	{
		_selectedTemplate = template;
		ApplySelectedTemplateState(template, resetMirrorMode);
		if (activateTemplate && template != null)
		{
			ActivateTemplate(template);
		}
	}

	private void ApplySelectedTemplateState(DisplayTemplate template, bool resetMirrorMode = true)
	{
		if (resetMirrorMode)
		{
			AddinConfiguration.SetPlacementMirrorMode(PlacementMirrorMode.None);
		}
		AddinConfiguration.SetSelectedTemplate(template);
		NotifyPropertyChanged(() => SelectedTemplate);
		NotifyPropertyChanged(() => SelectedTemplateStatus);
		NotifyPropertyChanged(() => PlacementOptionsStatus);
		SetPlacementStatusCore(template == null ? ReadyPlacementStatus : "Selected: " + GetPlacementStatusTemplateText(template) + ". Click the map to place.");
	}

	private static void ActivateTemplate(DisplayTemplate selectedTemplate)
	{
		if (selectedTemplate == null)
		{
			return;
		}
		EditorDockpaneViewModel viewModel = FrameworkApplication.DockPaneManager.Find(_dockPaneID) as EditorDockpaneViewModel;
		int activationVersion = viewModel == null ? 0 : ++viewModel._activationVersion;
		TaskObservationService.Forget(ActivateSelectedTemplateToolAsync(selectedTemplate, activationVersion), $"Template activation failed for '{selectedTemplate?.Name}'.");
	}

	private static async Task ActivateSelectedTemplateToolAsync(DisplayTemplate selectedTemplate, int activationVersion)
	{
		try
		{
			if (selectedTemplate == null || AddinConfiguration.SelectedTemplate == null)
			{
				return;
			}
			GeometryType templateGeometryType = await CommonFunctions.GetTemplateGeometryTypeAsync(selectedTemplate);
			if (IsStaleActivation(activationVersion))
			{
				return;
			}
			string toolId = GetToolIdForGeometryType(templateGeometryType);
			PreviewSketchTool.ResetActivePreviewTool();
			if (toolId == "TemplateEditor_SketchPolylineTool" && await ParallelCopyService.PromptAndCreateIfRequestedAsync())
			{
				return;
			}
			if (IsStaleActivation(activationVersion))
			{
				return;
			}
			AddinConfiguration.RecordRecentTemplate(selectedTemplate.UniqueKey);
			RefreshFavouriteAndRecentLists(refreshVisibleTemplates: false);
			ToolReactivationService.ActivateTool(toolId);
		}
		catch (Exception ex)
		{
			DialogService.Show(ex.Message, "Template Editor");
		}
	}

	private static bool IsStaleActivation(int activationVersion)
	{
		EditorDockpaneViewModel viewModel = FrameworkApplication.DockPaneManager.Find(_dockPaneID) as EditorDockpaneViewModel;
		return viewModel != null && activationVersion != 0 && activationVersion != viewModel._activationVersion;
	}

	private static string GetToolIdForGeometryType(GeometryType templateGeometryType)
	{
		if (GeometryTypeHelper.IsTable(templateGeometryType))
		{
			return "TemplateEditor_AddRowTool";
		}
		if (GeometryTypeHelper.IsPoint(templateGeometryType))
		{
			return "TemplateEditor_SketchPointTool";
		}
		if (GeometryTypeHelper.IsPolyline(templateGeometryType))
		{
			return "TemplateEditor_SketchPolylineTool";
		}
		if (GeometryTypeHelper.IsPolygon(templateGeometryType))
		{
			SimpleTemplate simpleTemplate = AddinConfiguration.Templates?.SimpleTemplates?.FirstOrDefault((SimpleTemplate n) => string.Equals(n.Name, AddinConfiguration.SelectedTemplate?.Name, StringComparison.OrdinalIgnoreCase));
			return simpleTemplate?.Geometry == null ? "TemplateEditor_SketchPolygonTool" : "TemplateEditor_SketchPointTool";
		}
		return "esri_mapping_exploreTool";
	}

	public string SearchText
	{
		get
		{
			return _searchText;
		}
		set
		{
			_searchText = value ?? string.Empty;
			FilterTemplates();
			NotifyPropertyChanged(() => SearchText);
			NotifyPropertyChanged(() => HasSearchText);
		}
	}

	public bool HasSearchText => !string.IsNullOrWhiteSpace(_searchText);

	public bool ShowGroupTemplates
	{
		get
		{
			return _showGroupTemplates;
		}
		set
		{
			_showGroupTemplates = value;
			FilterTemplates();
		}
	}

	public bool ShowSimpleTemplates
	{
		get
		{
			return _showSimpleTemplates;
		}
		set
		{
			_showSimpleTemplates = value;
			FilterTemplates();
		}
	}

	public bool ShowAllTemplates
	{
		get
		{
			return _showAllTemplates;
		}
		set
		{
			_showAllTemplates = value;
			FilterTemplates();
		}
	}

	public bool ShowFavouriteTemplates
	{
		get
		{
			return _showFavouriteTemplates;
		}
		set
		{
			_showFavouriteTemplates = value;
			FilterTemplates();
		}
	}

	public bool ShowRecentTemplates
	{
		get
		{
			return _showRecentTemplates;
		}
		set
		{
			_showRecentTemplates = value;
			FilterTemplates();
		}
	}

	protected EditorDockpaneViewModel()
	{
		LogService.Write("EditorDockpaneViewModel constructor starting.");
		SortCommand = new RelayCommand(SortTemplates);
		ClearSearchCommand = new RelayCommand(_ => SearchText = string.Empty);
		ActivateSelectedTemplateCommand = new RelayCommand(_ =>
		{
			if (SelectedTemplate == null)
			{
				return;
			}
			SelectTemplate(SelectedTemplate);
		});
		ActivateChildTemplateCommand = new RelayCommand(ActivateChildTemplate);
		ToggleGroupExpansionCommand = new RelayCommand(ToggleGroupExpansion);
		DeactivateTemplateCommand = new RelayCommand(_ => DeactivateTemplate());
		ReloadConfigCommand = new RelayCommand(_ => ReloadTemplateConfig());
		ToggleFavouriteCommand = new RelayCommand(ToggleFavourite);
		ActivateContinuousPlacementCommand = new RelayCommand(ActivateContinuousPlacement);
		StopContinuousPlacementCommand = new RelayCommand(_ => StopContinuousPlacement());
		ActivateMirrorPlacementCommand = new RelayCommand(ActivateMirrorPlacement);
		PlaceWithOverridesCommand = new RelayCommand(parameter => _ = PlaceWithOverridesAsync(parameter));
		try
		{
			LoadTemplatesFromConfig();
			LogService.Write("EditorDockpaneViewModel loaded templates successfully.");
		}
		catch (Exception ex)
		{
			InitializeEmptyTemplateLists();
			SetPlacementStatusCore("Choose a valid template configuration to begin.");
			LogService.LogException("Template editor dockpane could not load templates during initialization.", ex);
			DialogService.ShowAsync("Template configuration could not be loaded.\n\n" + ex.Message, "Template Editor");
		}
	}

	private void ActivateContinuousPlacement(object parameter)
	{
		if (parameter is not DisplayTemplate template)
		{
			return;
		}
		SetContinuousPlacementMode(true);
		SelectTemplate(template);
	}


	private void ActivateMirrorPlacement(object parameter)
	{
		if (parameter is not Tuple<DisplayTemplate, PlacementMirrorMode> mirrorRequest || mirrorRequest.Item1 == null)
		{
			return;
		}
		AddinConfiguration.SetPlacementMirrorMode(mirrorRequest.Item2);
		NotifyPropertyChanged(() => PlacementOptionsStatus);
		if (Equals(_selectedTemplate, mirrorRequest.Item1))
		{
			SelectTemplate(mirrorRequest.Item1, resetMirrorMode: false);
			return;
		}
		_isApplyingMirrorPlacementSelection = true;
		try
		{
			SelectedTemplate = mirrorRequest.Item1;
		}
		finally
		{
			_isApplyingMirrorPlacementSelection = false;
		}
	}

	private void StopContinuousPlacement()
	{
		SetContinuousPlacementMode(false);
		DeactivateTemplate();
	}

	private async Task PlaceWithOverridesAsync(object parameter)
	{
		if (parameter is not DisplayTemplate template)
		{
			return;
		}
		if (!await PlacementAttributeOverrideService.ConfigureOneTimePlacementOverridesAsync(template))
		{
			return;
		}
		SelectTemplate(template);
	}

	private void SetContinuousPlacementMode(bool enabled)
	{
		TemplateEditorSettings settings = AddinConfiguration.Settings?.Clone() ?? new TemplateEditorSettings();
		if (settings.EnableContinuousPlacementMode == enabled)
		{
			NotifyPropertyChanged(() => IsContinuousPlacementEnabled);
			NotifyPropertyChanged(() => PlacementOptionsStatus);
			return;
		}
		settings.EnableContinuousPlacementMode = enabled;
		AddinConfiguration.ApplySettings(settings);
		NotifyPropertyChanged(() => IsContinuousPlacementEnabled);
		NotifyPropertyChanged(() => PlacementOptionsStatus);
	}

	private void ActivateChildTemplate(object parameter)
	{
		if (parameter is not DisplayTemplate childRow || !childRow.IsGroupChild || string.IsNullOrWhiteSpace(childRow.ParentTemplateName))
		{
			return;
		}
		DisplayTemplate groupTemplate = _groupTemplates?.FirstOrDefault((DisplayTemplate template) =>
			string.Equals(template.Name, childRow.ParentTemplateName, StringComparison.OrdinalIgnoreCase));
		if (groupTemplate == null)
		{
			return;
		}
		SelectTemplate(childRow, resetMirrorMode: !_isApplyingMirrorPlacementSelection);
	}

	private void ToggleGroupExpansion(object parameter)
	{
		if (parameter is not DisplayTemplate template || !template.HasChildTemplates)
		{
			return;
		}
		template.IsExpanded = !template.IsExpanded;
		if (!template.IsExpanded && _selectedTemplate?.IsGroupChild == true &&
			string.Equals(_selectedTemplate.ParentTemplateName, template.Name, StringComparison.OrdinalIgnoreCase))
		{
			SelectTemplate(template, resetMirrorMode: false, activateTemplate: false);
		}
		FilterTemplates();
	}

	private void DeactivateTemplate()
	{
		_activationVersion++;
		SelectTemplate(null, activateTemplate: false);
		ToolReactivationService.ActivateSelectTool();
	}

	private void ReloadTemplateConfig()
	{
		try
		{
			string selectedTemplateName = SelectedTemplate?.Name;
			LoadTemplatesFromConfig();
			SelectTemplate(Templates.FirstOrDefault((DisplayTemplate template) => string.Equals(template.Name, selectedTemplateName, StringComparison.OrdinalIgnoreCase)), resetMirrorMode: false, activateTemplate: false);
		}
		catch (Exception ex)
		{
			DialogService.Show(ex.Message, "Template Editor");
		}
	}

	internal static void ReloadConfig()
	{
		try
		{
			if (FrameworkApplication.DockPaneManager.Find(_dockPaneID) is EditorDockpaneViewModel viewModel)
			{
				viewModel.ReloadTemplateConfig();
				EditorDockpaneViewModel.Show();
				return;
			}
			AddinConfiguration.ReloadTemplates();
			DialogService.Show("Template configuration reloaded.", "Template Editor");
		}
		catch (Exception ex)
		{
			DialogService.Show(ex.Message, "Template Editor");
		}
	}

	private void LoadTemplatesFromConfig()
	{
		TemplateConfig templates = AddinConfiguration.ReloadTemplates();
		templates.SimpleTemplates ??= new List<SimpleTemplate>();
		templates.GroupTemplates ??= new List<GroupTemplate>();
		Dictionary<string, SimpleTemplate> simpleTemplatesByName = templates.SimpleTemplates
			.GroupBy((SimpleTemplate template) => template.Name, StringComparer.OrdinalIgnoreCase)
			.ToDictionary((IGrouping<string, SimpleTemplate> group) => group.Key, (IGrouping<string, SimpleTemplate> group) => group.First(), StringComparer.OrdinalIgnoreCase);
		_simpleTemplates = new List<DisplayTemplate>();
		foreach (SimpleTemplate simpleTemplate in templates.SimpleTemplates)
		{
			if (!templates.GroupTemplates.Any((GroupTemplate n) => (n.SimpleTemplates ?? Enumerable.Empty<SimpleTemplateReference>()).Any((SimpleTemplateReference r) => string.Equals(r.Name, simpleTemplate.Name, StringComparison.OrdinalIgnoreCase))))
			{
				_simpleTemplates.Add(CreateDisplayTemplate(simpleTemplate));
			}
		}
		_groupTemplates = (from n in templates.GroupTemplates
			select CreateDisplayTemplate(n, simpleTemplatesByName) into n
			orderby n.Name
			select n).ToList();
		List<DisplayTemplate> _allSimpleTemplates = (from n in templates.SimpleTemplates
			select CreateDisplayTemplate(n) into n
			orderby n.Name
			select n).ToList();
		List<DisplayTemplate> allGroupTemplates = (from n in _groupTemplates
			orderby n.Name
			select n).ToList();
		_allTemplates = (from n in _allSimpleTemplates.Concat(allGroupTemplates)
			orderby n.Name
			select n).ToList();
		_allTemplatesByKey = BuildAllTemplatesByKey(_simpleTemplates, _groupTemplates);
		BuildFavouriteAndRecentLists();
		Templates = ApplySort(_groupTemplates).ToList();
		TemplateCount = $"{Templates.Count} template(s)";
		if (!_showGroupTemplates && !_showSimpleTemplates && !_showAllTemplates && !_showFavouriteTemplates && !_showRecentTemplates)
		{
			_showGroupTemplates = true;
		}
		FilterTemplates();
	}

	private void InitializeEmptyTemplateLists()
	{
		_simpleTemplates = new List<DisplayTemplate>();
		_groupTemplates = new List<DisplayTemplate>();
		_allTemplates = new List<DisplayTemplate>();
		_favouriteTemplates = new List<DisplayTemplate>();
		_recentTemplates = new List<DisplayTemplate>();
		_allTemplatesByKey = new Dictionary<string, DisplayTemplate>(StringComparer.OrdinalIgnoreCase);
		Templates = new List<DisplayTemplate>();
		TemplateCount = "0 template(s)";
		_showGroupTemplates = true;
		NotifyPropertyChanged(() => Templates);
		NotifyPropertyChanged(() => TemplateCount);
	}

	private static DisplayTemplate CreateDisplayTemplate(SimpleTemplate simpleTemplate)
	{
		return new DisplayTemplate
		{
			Name = simpleTemplate.Name,
			TemplateType = simpleTemplate.TemplateType,
			Description = simpleTemplate.Description
		};
	}

	private static DisplayTemplate CreateDisplayTemplate(GroupTemplate groupTemplate, IReadOnlyDictionary<string, SimpleTemplate> simpleTemplatesByName)
	{
		return new DisplayTemplate
		{
			Name = groupTemplate.Name,
			TemplateType = groupTemplate.TemplateType,
			Description = groupTemplate.Description,
			ChildTemplates = CreateChildTemplates(groupTemplate, simpleTemplatesByName)
		};
	}

	private static List<DisplayTemplateChild> CreateChildTemplates(GroupTemplate groupTemplate, IReadOnlyDictionary<string, SimpleTemplate> simpleTemplatesByName)
	{
		return (groupTemplate.SimpleTemplates ?? new List<SimpleTemplateReference>())
			.Select((SimpleTemplateReference templateReference) =>
			{
				simpleTemplatesByName.TryGetValue(templateReference.Name ?? string.Empty, out SimpleTemplate simpleTemplate);
				return new DisplayTemplateChild
				{
					Name = templateReference.Name,
					FeatureId = templateReference.FeatureId,
					ParentTemplateName = groupTemplate.Name,
					SketchType = templateReference.SketchType,
					TemplateType = simpleTemplate?.TemplateType,
					Description = simpleTemplate?.Description
				};
			})
			.OrderBy((DisplayTemplateChild template) => template.FeatureId)
			.ThenBy((DisplayTemplateChild template) => template.Name, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private void FilterTemplates()
	{
		IEnumerable<DisplayTemplate> source = GetCurrentViewTemplates();

		string[] searchTerms = GetSearchTerms(_searchText);
		if (searchTerms.Length > 0)
		{
			source = source.Where((DisplayTemplate template) => MatchesSearchTerms(template, searchTerms));
		}

		// Keep recency order for the Recent view; sort everything else.
		List<DisplayTemplate> visibleTemplates = _showRecentTemplates
			? source.ToList()
			: ApplySort(source).ToList();
		Templates = ShouldShowExpandedGroupRows()
			? ExpandGroupRows(visibleTemplates).ToList()
			: visibleTemplates;
		if (_selectedTemplate?.IsGroupChild == true && !Templates.Any((DisplayTemplate template) => string.Equals(template.UniqueKey, _selectedTemplate.UniqueKey, StringComparison.OrdinalIgnoreCase)))
		{
			_selectedTemplate = null;
			AddinConfiguration.ClearSelectedTemplate();
			NotifyPropertyChanged(() => SelectedTemplate);
			NotifyPropertyChanged(() => SelectedTemplateStatus);
			SetPlacementStatusCore(ReadyPlacementStatus);
			ToolReactivationService.ActivateSelectTool();
		}
		TemplateCount = $"{Templates.Count} template(s)";
		NotifyPropertyChanged(() => Templates);
		NotifyPropertyChanged(() => TemplateCount);
		NotifyPropertyChanged(() => PlacementOptionsStatus);
	}

	internal static void PostPlacementSummary(string summary, string details = null, bool warning = false)
	{
		CompleteTemplateSelectionAfterPlacement();
		string message = string.IsNullOrWhiteSpace(details) ? summary : summary + "\n" + details;
		DialogService.ShowToast(message, "Template Editor", warning ? FeedbackSeverity.Warning : FeedbackSeverity.Success);
	}

	internal static bool ShouldReturnToSelectAfterPlacement(bool placementSucceeded)
	{
		return !placementSucceeded || AddinConfiguration.Settings?.EnableContinuousPlacementMode != true || AddinConfiguration.SelectedTemplate == null;
	}

	private static void CompleteTemplateSelectionAfterPlacement()
	{
		if (FrameworkApplication.DockPaneManager.Find(_dockPaneID) is not EditorDockpaneViewModel viewModel)
		{
			if (AddinConfiguration.Settings?.EnableContinuousPlacementMode != true)
			{
				AddinConfiguration.ClearSelectedTemplate(resetMirrorMode: true);
			}
			return;
		}
		if (viewModel.IsContinuousPlacementEnabled && viewModel._selectedTemplate != null)
		{
			AddinConfiguration.SetSelectedTemplate(viewModel._selectedTemplate);
			viewModel.NotifyPropertyChanged(() => viewModel.SelectedTemplate);
			viewModel.NotifyPropertyChanged(() => viewModel.SelectedTemplateStatus);
			viewModel.NotifyPropertyChanged(() => viewModel.PlacementOptionsStatus);
			viewModel.SetPlacementStatusCore("Continuous: " + viewModel.GetPlacementStatusTemplateText(viewModel._selectedTemplate) + ". Click the map to place again.");
			return;
		}
		AddinConfiguration.SetPlacementMirrorMode(PlacementMirrorMode.None);
		viewModel.NotifyPropertyChanged(() => viewModel.SelectedTemplate);
		viewModel.NotifyPropertyChanged(() => viewModel.SelectedTemplateStatus);
		viewModel.NotifyPropertyChanged(() => viewModel.PlacementOptionsStatus);
		viewModel.SetPlacementStatusCore(viewModel._selectedTemplate == null
			? ReadyPlacementStatus
			: "Placed: " + viewModel.GetPlacementStatusTemplateText(viewModel._selectedTemplate) + ". Click the highlighted template or press Enter to place again.");
	}

	internal static void SetPlacementStatus(string status)
	{
		if (FrameworkApplication.DockPaneManager.Find(_dockPaneID) is EditorDockpaneViewModel viewModel)
		{
			viewModel.SetPlacementStatusCore(status);
		}
		DialogService.UpdatePlacementProgress(status);
	}

	internal static void RefreshSettingsStatus()
	{
		if (FrameworkApplication.DockPaneManager.Find(_dockPaneID) is EditorDockpaneViewModel viewModel)
		{
			viewModel.NotifyPropertyChanged(() => viewModel.IsContinuousPlacementEnabled);
			viewModel.NotifyPropertyChanged(() => viewModel.PlacementOptionsStatus);
		}
	}

	private void SetPlacementStatusCore(string status)
	{
		_placementStatus = string.IsNullOrWhiteSpace(status) ? ReadyPlacementStatus : status;
		NotifyPropertyChanged(() => PlacementStatus);
	}

	private string GetPlacementStatusTemplateText(DisplayTemplate template)
	{
		if (template == null)
		{
			return string.Empty;
		}
		string mirrorLabel = GetMirrorModeLabel(AddinConfiguration.PlacementMirrorMode);
		return string.IsNullOrWhiteSpace(mirrorLabel) ? template.DisplayName : template.DisplayName + " (" + mirrorLabel + ")";
	}

	private static string GetMirrorModeLabel(PlacementMirrorMode mirrorMode)
	{
		return mirrorMode switch
		{
			PlacementMirrorMode.Horizontal => "Horizontal",
			PlacementMirrorMode.Vertical => "Vertical",
			PlacementMirrorMode.Both => "Both",
			_ => string.Empty
		};
	}

	private IEnumerable<DisplayTemplate> GetCurrentViewTemplates()
	{
		if (_showGroupTemplates)
		{
			return _groupTemplates;
		}
		if (_showSimpleTemplates)
		{
			return _simpleTemplates;
		}
		if (_showAllTemplates)
		{
			return _allTemplates;
		}
		if (_showFavouriteTemplates)
		{
			return _favouriteTemplates ?? Enumerable.Empty<DisplayTemplate>();
		}
		if (_showRecentTemplates)
		{
			return _recentTemplates ?? Enumerable.Empty<DisplayTemplate>();
		}
		return Enumerable.Empty<DisplayTemplate>();
	}

	private bool ShouldShowExpandedGroupRows()
	{
		return _showGroupTemplates || _showAllTemplates || _showFavouriteTemplates || _showRecentTemplates;
	}

	internal void RefreshTemplateRows()
	{
		FilterTemplates();
	}

	internal static void RefreshFavouriteAndRecentLists(bool refreshVisibleTemplates = true)
	{
		if (FrameworkApplication.DockPaneManager.Find(_dockPaneID) is EditorDockpaneViewModel viewModel)
		{
			viewModel.BuildFavouriteAndRecentLists();
			if (refreshVisibleTemplates && (viewModel._showFavouriteTemplates || viewModel._showRecentTemplates))
			{
				viewModel.FilterTemplates();
			}
		}
	}

	private static Dictionary<string, DisplayTemplate> BuildAllTemplatesByKey(
		IEnumerable<DisplayTemplate> simpleTemplates,
		IEnumerable<DisplayTemplate> groupTemplates)
	{
		Dictionary<string, DisplayTemplate> result = new Dictionary<string, DisplayTemplate>(StringComparer.OrdinalIgnoreCase);
		foreach (DisplayTemplate t in simpleTemplates ?? Enumerable.Empty<DisplayTemplate>())
		{
			result[t.UniqueKey] = t;
		}
		foreach (DisplayTemplate group in groupTemplates ?? Enumerable.Empty<DisplayTemplate>())
		{
			result[group.UniqueKey] = group;
			foreach (DisplayTemplateChild child in group.ChildTemplates ?? Enumerable.Empty<DisplayTemplateChild>())
			{
				DisplayTemplate childDisplay = new DisplayTemplate
				{
					Name = child.Name,
					TemplateType = child.TemplateType,
					Description = child.Description,
					IsGroupChild = true,
					ParentTemplateName = child.ParentTemplateName,
					FeatureId = child.FeatureId,
					SketchType = child.SketchType
				};
				result[childDisplay.UniqueKey] = childDisplay;
			}
		}
		return result;
	}

	private void BuildFavouriteAndRecentLists()
	{
		HashSet<string> favouriteKeys = new HashSet<string>(
			AddinConfiguration.Settings?.FavouriteTemplateKeys ?? Enumerable.Empty<string>(),
			StringComparer.OrdinalIgnoreCase);
		_favouriteTemplates = (_allTemplatesByKey?.Values ?? Enumerable.Empty<DisplayTemplate>())
			.Where(t => favouriteKeys.Contains(t.UniqueKey))
			.Select(CloneForFlatList)
			.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
			.ToList();
		_recentTemplates = (AddinConfiguration.Settings?.RecentTemplateKeys ?? Enumerable.Empty<string>())
			.Select(key => _allTemplatesByKey != null && _allTemplatesByKey.TryGetValue(key, out DisplayTemplate t) ? t : null)
			.Where(t => t != null)
			.Select(CloneForFlatList)
			.ToList();
	}

	private static DisplayTemplate CloneForFlatList(DisplayTemplate template)
	{
		return new DisplayTemplate
		{
			Name = template.Name,
			TemplateType = template.TemplateType,
			Description = template.Description,
			IsGroupChild = template.IsGroupChild,
			IsFlatListItem = template.IsGroupChild,
			ParentTemplateName = template.ParentTemplateName,
			FeatureId = template.FeatureId,
			SketchType = template.SketchType,
			ChildTemplates = template.ChildTemplates == null
				? new List<DisplayTemplateChild>()
				: new List<DisplayTemplateChild>(template.ChildTemplates)
		};
	}

	private void ToggleFavourite(object parameter)
	{
		if (parameter is not DisplayTemplate template)
		{
			return;
		}
		AddinConfiguration.ToggleFavourite(template.UniqueKey);
		BuildFavouriteAndRecentLists();
		FilterTemplates();
	}

	private IEnumerable<DisplayTemplate> ExpandGroupRows(IEnumerable<DisplayTemplate> templates)
	{
		foreach (DisplayTemplate template in templates)
		{
			yield return template;
			if (!template.IsExpanded || template.ChildTemplates == null)
			{
				continue;
			}
			foreach (DisplayTemplateChild childTemplate in template.ChildTemplates)
			{
				yield return new DisplayTemplate
				{
					Name = childTemplate.Name,
					TemplateType = childTemplate.TemplateType,
					Description = childTemplate.Description,
					IsGroupChild = true,
					ParentTemplateName = template.Name,
					FeatureId = childTemplate.FeatureId,
					SketchType = childTemplate.SketchType
				};
			}
		}
	}

	private void SortTemplates(object parameter)
	{
		string sortField = parameter as string;
		if (string.IsNullOrWhiteSpace(sortField))
		{
			return;
		}
		if (string.Equals(_sortField, sortField, StringComparison.Ordinal))
		{
			_sortAscending = !_sortAscending;
		}
		else
		{
			_sortField = sortField;
			_sortAscending = true;
		}
		FilterTemplates();
	}

	private IEnumerable<DisplayTemplate> ApplySort(IEnumerable<DisplayTemplate> templates)
	{
		Func<DisplayTemplate, string> selector = _sortField switch
		{
			"TemplateType" => (DisplayTemplate template) => template.TemplateType,
			"Description" => (DisplayTemplate template) => template.Description,
			_ => (DisplayTemplate template) => template.Name
		};
		return _sortAscending
			? templates.OrderBy(selector, StringComparer.OrdinalIgnoreCase).ThenBy((DisplayTemplate template) => template.Name, StringComparer.OrdinalIgnoreCase)
			: templates.OrderByDescending(selector, StringComparer.OrdinalIgnoreCase).ThenBy((DisplayTemplate template) => template.Name, StringComparer.OrdinalIgnoreCase);
	}

	private static string[] GetSearchTerms(string searchText)
	{
		if (string.IsNullOrWhiteSpace(searchText) || searchText.Trim().Length < 2)
		{
			return Array.Empty<string>();
		}
		return searchText.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
			.Select((string term) => term.Trim())
			.Where((string term) => term.Length > 0)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();
	}

	private static bool MatchesSearchTerms(DisplayTemplate template, IReadOnlyList<string> searchTerms)
	{
		IEnumerable<string> childTemplateText = template.ChildTemplates?.Select((DisplayTemplateChild childTemplate) =>
			string.Join(" ", childTemplate.Name, childTemplate.TemplateType, childTemplate.Description, childTemplate.SketchType)) ?? Enumerable.Empty<string>();
		string searchableText = string.Join(" ", new[] { template.Name, template.TemplateType, template.Description }.Concat(childTemplateText));
		return searchTerms.All((string term) => searchableText.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
	}

	internal static void Show()
	{
		DockPane pane = FrameworkApplication.DockPaneManager.Find("TemplateEditor_EditorDockpane");
		if (pane != null)
		{
			pane.Activate();
		}
	}
}

internal sealed class RelayCommand : ICommand
{
	private readonly Action<object> _execute;

	public RelayCommand(Action<object> execute)
	{
		_execute = execute ?? throw new ArgumentNullException(nameof(execute));
	}

	public event EventHandler CanExecuteChanged
	{
		add { }
		remove { }
	}

	public bool CanExecute(object parameter)
	{
		return true;
	}

	public void Execute(object parameter)
	{
		_execute(parameter);
	}
}
