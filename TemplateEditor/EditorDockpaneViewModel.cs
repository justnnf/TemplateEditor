using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

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

	private string _placementStatus = "Ready. Please select a template to place.";

	public List<DisplayTemplate> Templates { get; set; }

	public string TemplateCount { get; set; }

	public string SelectedTemplateStatus => (SelectedTemplate == null) ? "No template selected" : ("Selected: " + SelectedTemplate.DisplayName);

	public string PlacementStatus => _placementStatus;

	public string ConfigurationHealthStatus
	{
		get
		{
			if (!AddinConfiguration.HasValidTemplateConfigPath())
			{
				return "Config: missing";
			}
			AssociationRuleCatalog current = AssociationRuleCatalog.Current;
			if (!current.IsAvailable)
			{
				return "Rules: unavailable";
			}
			return current.HasRules ? "Config and rules ready" : "Config ready; no rules";
		}
	}

	public bool IsContinuousPlacementEnabled => AddinConfiguration.Settings?.EnableContinuousPlacementMode ?? false;

	public string PlacementOptionsStatus
	{
		get
		{
			TemplateEditorSettings settings = AddinConfiguration.Settings;
			if (settings == null)
			{
				return string.Empty;
			}
			List<string> list = new List<string>();
			list.Add(settings.PreventDefaultVersionPlacement ? "DEFAULT blocked" : "DEFAULT allowed");
			list.Add(settings.EnableAssociationPrompts ? "Associations on" : "Associations off");
			list.Add(settings.EnableLineSplitPrompts ? "Splits on" : "Splits off");
			if (settings.EnableContinuousPlacementMode)
			{
				list.Add("Continuous on");
			}
			if (AddinConfiguration.PlacementMirrorMode != PlacementMirrorMode.None)
			{
				list.Add("Mirror " + GetMirrorModeLabel(AddinConfiguration.PlacementMirrorMode));
			}
			string statusLabel = PlacementAttributeOverrideService.GetStatusLabel();
			if (!string.IsNullOrWhiteSpace(statusLabel))
			{
				list.Add(statusLabel);
			}
			return string.Join(" | ", list);
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

	public ICommand PlaceAtOffsetCommand { get; }

	public DisplayTemplate SelectedTemplate
	{
		get
		{
			return _selectedTemplate;
		}
		set
		{
			if (!object.Equals(_selectedTemplate, value))
			{
				SelectTemplate(value, !_isApplyingMirrorPlacementSelection);
			}
		}
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
			NotifyPropertyChanged<string>((Expression<Func<string>>)(() => SearchText));
			NotifyPropertyChanged<bool>((Expression<Func<bool>>)(() => HasSearchText));
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

	private void SelectTemplate(DisplayTemplate template, bool resetMirrorMode = true, bool activateTemplate = true)
	{
		if (object.Equals(_selectedTemplate, template))
		{
			if (activateTemplate && template != null)
			{
				ApplySelectedTemplateState(template, resetMirrorMode);
				ActivateTemplate(template);
			}
			return;
		}
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
		NotifyPropertyChanged<DisplayTemplate>((Expression<Func<DisplayTemplate>>)(() => SelectedTemplate));
		NotifyPropertyChanged<string>((Expression<Func<string>>)(() => SelectedTemplateStatus));
		NotifyPropertyChanged<string>((Expression<Func<string>>)(() => PlacementOptionsStatus));
		SetPlacementStatusCore((template == null) ? "Ready. Please select a template to place." : ("Selected: " + GetPlacementStatusTemplateText(template) + ". Click the map to place."));
	}

	private static void ActivateTemplate(DisplayTemplate selectedTemplate)
	{
		if (selectedTemplate != null)
		{
			int activationVersion = ((FrameworkApplication.DockPaneManager.Find("TemplateEditor_EditorDockpane") is EditorDockpaneViewModel editorDockpaneViewModel) ? (++editorDockpaneViewModel._activationVersion) : 0);
			TaskObservationService.Forget(ActivateSelectedTemplateToolAsync(selectedTemplate, activationVersion), "Template activation failed for '" + selectedTemplate?.Name + "'.");
		}
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
			if (!IsStaleActivation(activationVersion))
			{
				string toolId = GetToolIdForGeometryType(templateGeometryType);
				PreviewSketchTool.ResetActivePreviewTool();
				bool flag = toolId == "TemplateEditor_SketchPolylineTool";
				bool flag2 = flag;
				if (flag2)
				{
					flag2 = await ParallelCopyService.PromptAndCreateIfRequestedAsync();
				}
				if (!flag2 && !IsStaleActivation(activationVersion))
				{
					AddinConfiguration.RecordRecentTemplate(selectedTemplate.UniqueKey);
					RefreshFavouriteAndRecentLists(refreshVisibleTemplates: false);
					ToolReactivationService.ActivateTool(toolId);
				}
			}
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			DialogService.Show(ex2.Message, "Template Editor");
		}
	}

	private static bool IsStaleActivation(int activationVersion)
	{
		return FrameworkApplication.DockPaneManager.Find("TemplateEditor_EditorDockpane") is EditorDockpaneViewModel editorDockpaneViewModel && activationVersion != 0 && activationVersion != editorDockpaneViewModel._activationVersion;
	}

	private static string GetToolIdForGeometryType(GeometryType templateGeometryType)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
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
			return ((AddinConfiguration.Templates?.SimpleTemplates?.FirstOrDefault((SimpleTemplate n) => string.Equals(n.Name, AddinConfiguration.SelectedTemplate?.Name, StringComparison.OrdinalIgnoreCase)))?.Geometry == null) ? "TemplateEditor_SketchPolygonTool" : "TemplateEditor_SketchPointTool";
		}
		return "esri_mapping_exploreTool";
	}

	protected EditorDockpaneViewModel()
	{
		LogService.Write("EditorDockpaneViewModel constructor starting.");
		// Commands are bound by the generated WPF dockpane view; each one keeps UI
		// state changes in this view model and delegates map edits to service classes.
		SortCommand = new RelayCommand(SortTemplates);
		ClearSearchCommand = new RelayCommand(delegate
		{
			SearchText = string.Empty;
		});
		ActivateSelectedTemplateCommand = new RelayCommand(delegate
		{
			if (SelectedTemplate != null)
			{
				SelectTemplate(SelectedTemplate);
			}
		});
		ActivateChildTemplateCommand = new RelayCommand(ActivateChildTemplate);
		ToggleGroupExpansionCommand = new RelayCommand(ToggleGroupExpansion);
		DeactivateTemplateCommand = new RelayCommand(delegate
		{
			DeactivateTemplate();
		});
		ReloadConfigCommand = new RelayCommand(delegate
		{
			ReloadTemplateConfig();
		});
		ToggleFavouriteCommand = new RelayCommand(ToggleFavourite);
		ActivateContinuousPlacementCommand = new RelayCommand(ActivateContinuousPlacement);
		StopContinuousPlacementCommand = new RelayCommand(delegate
		{
			StopContinuousPlacement();
		});
		ActivateMirrorPlacementCommand = new RelayCommand(ActivateMirrorPlacement);
		PlaceWithOverridesCommand = new RelayCommand(delegate(object parameter)
		{
			PlaceWithOverridesAsync(parameter);
		});
		PlaceAtOffsetCommand = new RelayCommand(delegate(object parameter)
		{
			PlaceAtOffsetAsync(parameter);
		});
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
		if (parameter is DisplayTemplate template)
		{
			SetContinuousPlacementMode(enabled: true);
			SelectTemplate(template);
		}
	}

	private void ActivateMirrorPlacement(object parameter)
	{
		if (!(parameter is Tuple<DisplayTemplate, PlacementMirrorMode> { Item1: not null } tuple))
		{
			return;
		}
		AddinConfiguration.SetPlacementMirrorMode(tuple.Item2);
		NotifyPropertyChanged<string>((Expression<Func<string>>)(() => PlacementOptionsStatus));
		if (object.Equals(_selectedTemplate, tuple.Item1))
		{
			SelectTemplate(tuple.Item1, resetMirrorMode: false);
			return;
		}
		_isApplyingMirrorPlacementSelection = true;
		try
		{
			SelectedTemplate = tuple.Item1;
		}
		finally
		{
			_isApplyingMirrorPlacementSelection = false;
		}
	}

	private void StopContinuousPlacement()
	{
		SetContinuousPlacementMode(enabled: false);
		DeactivateTemplate();
	}

	private async Task PlaceWithOverridesAsync(object parameter)
	{
		if (parameter is DisplayTemplate template && await PlacementAttributeOverrideService.ConfigureOneTimePlacementOverridesAsync(template))
		{
			SelectTemplate(template);
		}
	}

	private async Task PlaceAtOffsetAsync(object parameter)
	{
		if (parameter is DisplayTemplate template)
		{
			if (!GeometryTypeHelper.IsPoint(await CommonFunctions.GetTemplateGeometryTypeAsync(template)))
			{
				DialogService.Show("Offset placement is available for point templates.", "Template Editor");
			}
			else if (OffsetPlacementSession.Begin())
			{
				SelectTemplate(template);
			}
		}
	}

	private void SetContinuousPlacementMode(bool enabled)
	{
		TemplateEditorSettings templateEditorSettings = AddinConfiguration.Settings?.Clone() ?? new TemplateEditorSettings();
		if (templateEditorSettings.EnableContinuousPlacementMode == enabled)
		{
			NotifyPropertyChanged<bool>((Expression<Func<bool>>)(() => IsContinuousPlacementEnabled));
			NotifyPropertyChanged<string>((Expression<Func<string>>)(() => PlacementOptionsStatus));
			return;
		}
		templateEditorSettings.EnableContinuousPlacementMode = enabled;
		AddinConfiguration.ApplySettings(templateEditorSettings);
		NotifyPropertyChanged<bool>((Expression<Func<bool>>)(() => IsContinuousPlacementEnabled));
		NotifyPropertyChanged<string>((Expression<Func<string>>)(() => PlacementOptionsStatus));
	}

	private void ActivateChildTemplate(object parameter)
	{
		DisplayTemplate childRow = parameter as DisplayTemplate;
		if (childRow != null && childRow.IsGroupChild && !string.IsNullOrWhiteSpace(childRow.ParentTemplateName))
		{
			DisplayTemplate displayTemplate = _groupTemplates?.FirstOrDefault((DisplayTemplate template) => string.Equals(template.Name, childRow.ParentTemplateName, StringComparison.OrdinalIgnoreCase));
			if (displayTemplate != null)
			{
				SelectTemplate(childRow, !_isApplyingMirrorPlacementSelection);
			}
		}
	}

	internal void ToggleGroupExpansion(object parameter)
	{
		if (!(parameter is DisplayTemplate { HasChildTemplates: not false } displayTemplate))
		{
			return;
		}
		displayTemplate.IsExpanded = !displayTemplate.IsExpanded;
		if (!displayTemplate.IsExpanded)
		{
			DisplayTemplate selectedTemplate = _selectedTemplate;
			if (selectedTemplate != null && selectedTemplate.IsGroupChild && string.Equals(_selectedTemplate.ParentTemplateName, displayTemplate.Name, StringComparison.OrdinalIgnoreCase))
			{
				SelectTemplate(displayTemplate, resetMirrorMode: false, activateTemplate: false);
			}
		}
		FilterTemplates();
	}

	private void DeactivateTemplate()
	{
		_activationVersion++;
		SelectTemplate(null, resetMirrorMode: true, activateTemplate: false);
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
			if (FrameworkApplication.DockPaneManager.Find("TemplateEditor_EditorDockpane") is EditorDockpaneViewModel editorDockpaneViewModel)
			{
				editorDockpaneViewModel.ReloadTemplateConfig();
				Show();
			}
			else
			{
				AddinConfiguration.ReloadTemplates();
				DialogService.Show("Template configuration reloaded.", "Template Editor");
			}
		}
		catch (Exception ex)
		{
			DialogService.Show(ex.Message, "Template Editor");
		}
	}

	private void LoadTemplatesFromConfig()
	{
		// The config JSON can describe simple templates, group templates, and simple
		// templates that are only children of groups. The dockpane keeps separate
		// lists so the tabs can show the user the right level of detail.
		TemplateConfig templateConfig = AddinConfiguration.ReloadTemplates();
		TemplateConfig templateConfig2 = templateConfig;
		if (templateConfig2.SimpleTemplates == null)
		{
			List<SimpleTemplate> list = (templateConfig2.SimpleTemplates = new List<SimpleTemplate>());
		}
		templateConfig2 = templateConfig;
		if (templateConfig2.GroupTemplates == null)
		{
			List<GroupTemplate> list3 = (templateConfig2.GroupTemplates = new List<GroupTemplate>());
		}
		Dictionary<string, SimpleTemplate> simpleTemplatesByName = templateConfig.SimpleTemplates.GroupBy<SimpleTemplate, string>((SimpleTemplate template) => template.Name, StringComparer.OrdinalIgnoreCase).ToDictionary<IGrouping<string, SimpleTemplate>, string, SimpleTemplate>((IGrouping<string, SimpleTemplate> group) => group.Key, (IGrouping<string, SimpleTemplate> group) => group.First(), StringComparer.OrdinalIgnoreCase);
		_simpleTemplates = new List<DisplayTemplate>();
		foreach (SimpleTemplate simpleTemplate in templateConfig.SimpleTemplates)
		{
			if (!templateConfig.GroupTemplates.Any(delegate(GroupTemplate n)
			{
				IEnumerable<SimpleTemplateReference> simpleTemplates = n.SimpleTemplates;
				return (simpleTemplates ?? Enumerable.Empty<SimpleTemplateReference>()).Any((SimpleTemplateReference r) => string.Equals(r.Name, simpleTemplate.Name, StringComparison.OrdinalIgnoreCase));
			}))
			{
				_simpleTemplates.Add(CreateDisplayTemplate(simpleTemplate));
			}
		}
		_groupTemplates = (from n in templateConfig.GroupTemplates
			select CreateDisplayTemplate(n, simpleTemplatesByName) into n
			orderby n.Name
			select n).ToList();
		List<DisplayTemplate> first = (from n in templateConfig.SimpleTemplates
			select CreateDisplayTemplate(n) into n
			orderby n.Name
			select n).ToList();
		List<DisplayTemplate> second = _groupTemplates.OrderBy((DisplayTemplate n) => n.Name).ToList();
		_allTemplates = (from n in first.Concat(second)
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
		NotifyPropertyChanged<List<DisplayTemplate>>((Expression<Func<List<DisplayTemplate>>>)(() => Templates));
		NotifyPropertyChanged<string>((Expression<Func<string>>)(() => TemplateCount));
		NotifyPropertyChanged<string>((Expression<Func<string>>)(() => ConfigurationHealthStatus));
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
		return (from template in (groupTemplate.SimpleTemplates ?? new List<SimpleTemplateReference>()).Select(delegate(SimpleTemplateReference templateReference)
			{
				simpleTemplatesByName.TryGetValue(templateReference.Name ?? string.Empty, out var value);
				return new DisplayTemplateChild
				{
					Name = templateReference.Name,
					FeatureId = templateReference.FeatureId,
					ParentTemplateName = groupTemplate.Name,
					SketchType = templateReference.SketchType,
					TemplateType = value?.TemplateType,
					Description = value?.Description
				};
			})
			orderby template.FeatureId
			select template).ThenBy<DisplayTemplateChild, string>((DisplayTemplateChild template) => template.Name, StringComparer.OrdinalIgnoreCase).ToList();
	}

	private void FilterTemplates()
	{
		IEnumerable<DisplayTemplate> enumerable = GetCurrentViewTemplates();
		string[] searchTerms = GetSearchTerms(_searchText);
		if (searchTerms.Length != 0)
		{
			enumerable = enumerable.Where((DisplayTemplate template) => MatchesSearchTerms(template, searchTerms));
		}
		List<DisplayTemplate> list = (_showRecentTemplates ? enumerable.ToList() : ApplySort(enumerable).ToList());
		Templates = (ShouldShowExpandedGroupRows() ? ExpandGroupRows(list).ToList() : list);
		DisplayTemplate selectedTemplate = _selectedTemplate;
		if (selectedTemplate != null && selectedTemplate.IsGroupChild && !Templates.Any((DisplayTemplate template) => string.Equals(template.UniqueKey, _selectedTemplate.UniqueKey, StringComparison.OrdinalIgnoreCase)))
		{
			_selectedTemplate = null;
			AddinConfiguration.ClearSelectedTemplate();
			NotifyPropertyChanged<DisplayTemplate>((Expression<Func<DisplayTemplate>>)(() => SelectedTemplate));
			NotifyPropertyChanged<string>((Expression<Func<string>>)(() => SelectedTemplateStatus));
			SetPlacementStatusCore("Ready. Please select a template to place.");
			ToolReactivationService.ActivateSelectTool();
		}
		TemplateCount = $"{Templates.Count} template(s)";
		NotifyPropertyChanged<List<DisplayTemplate>>((Expression<Func<List<DisplayTemplate>>>)(() => Templates));
		NotifyPropertyChanged<string>((Expression<Func<string>>)(() => TemplateCount));
		NotifyPropertyChanged<string>((Expression<Func<string>>)(() => PlacementOptionsStatus));
		NotifyPropertyChanged<string>((Expression<Func<string>>)(() => ConfigurationHealthStatus));
	}

	internal static void PostPlacementSummary(string summary, string details = null, bool warning = false)
	{
		CompleteTemplateSelectionAfterPlacement();
		string message = (string.IsNullOrWhiteSpace(details) ? summary : (summary + "\n" + details));
		DialogService.ShowToast(message, "Template Editor", (!warning) ? FeedbackSeverity.Success : FeedbackSeverity.Warning);
	}

	internal static bool ShouldReturnToSelectAfterPlacement(bool placementSucceeded)
	{
		int result;
		if (placementSucceeded)
		{
			TemplateEditorSettings settings = AddinConfiguration.Settings;
			if (settings != null && settings.EnableContinuousPlacementMode)
			{
				result = ((AddinConfiguration.SelectedTemplate == null) ? 1 : 0);
				goto IL_0025;
			}
		}
		result = 1;
		goto IL_0025;
		IL_0025:
		return (byte)result != 0;
	}

	private static void CompleteTemplateSelectionAfterPlacement()
	{
		DockPane val = FrameworkApplication.DockPaneManager.Find("TemplateEditor_EditorDockpane");
		EditorDockpaneViewModel viewModel = val as EditorDockpaneViewModel;
		if (viewModel == null)
		{
			TemplateEditorSettings settings = AddinConfiguration.Settings;
			if (settings == null || !settings.EnableContinuousPlacementMode)
			{
				AddinConfiguration.ClearSelectedTemplate(resetMirrorMode: true);
			}
		}
		else if (viewModel.IsContinuousPlacementEnabled && viewModel._selectedTemplate != null)
		{
			AddinConfiguration.SetSelectedTemplate(viewModel._selectedTemplate);
			viewModel.NotifyPropertyChanged<DisplayTemplate>((Expression<Func<DisplayTemplate>>)(() => viewModel.SelectedTemplate));
			viewModel.NotifyPropertyChanged<string>((Expression<Func<string>>)(() => viewModel.SelectedTemplateStatus));
			viewModel.NotifyPropertyChanged<string>((Expression<Func<string>>)(() => viewModel.PlacementOptionsStatus));
			viewModel.SetPlacementStatusCore("Continuous: " + viewModel.GetPlacementStatusTemplateText(viewModel._selectedTemplate) + ". Click the map to place again.");
		}
		else
		{
			AddinConfiguration.SetPlacementMirrorMode(PlacementMirrorMode.None);
			viewModel.NotifyPropertyChanged<DisplayTemplate>((Expression<Func<DisplayTemplate>>)(() => viewModel.SelectedTemplate));
			viewModel.NotifyPropertyChanged<string>((Expression<Func<string>>)(() => viewModel.SelectedTemplateStatus));
			viewModel.NotifyPropertyChanged<string>((Expression<Func<string>>)(() => viewModel.PlacementOptionsStatus));
			viewModel.SetPlacementStatusCore((viewModel._selectedTemplate == null) ? "Ready. Please select a template to place." : ("Placed: " + viewModel.GetPlacementStatusTemplateText(viewModel._selectedTemplate) + ". Click the highlighted template or press Enter to place again."));
		}
	}

	internal static void SetPlacementStatus(string status)
	{
		if (FrameworkApplication.DockPaneManager.Find("TemplateEditor_EditorDockpane") is EditorDockpaneViewModel editorDockpaneViewModel)
		{
				editorDockpaneViewModel.SetPlacementStatusCore(status);
				}
			}

	internal static void RefreshSettingsStatus()
	{
		DockPane val = FrameworkApplication.DockPaneManager.Find("TemplateEditor_EditorDockpane");
		EditorDockpaneViewModel viewModel = val as EditorDockpaneViewModel;
		if (viewModel != null)
		{
			viewModel.NotifyPropertyChanged<bool>((Expression<Func<bool>>)(() => viewModel.IsContinuousPlacementEnabled));
			viewModel.NotifyPropertyChanged<string>((Expression<Func<string>>)(() => viewModel.PlacementOptionsStatus));
			viewModel.NotifyPropertyChanged<string>((Expression<Func<string>>)(() => viewModel.ConfigurationHealthStatus));
		}
	}

	private void SetPlacementStatusCore(string status)
	{
		_placementStatus = (string.IsNullOrWhiteSpace(status) ? "Ready. Please select a template to place." : status);
		NotifyPropertyChanged<string>((Expression<Func<string>>)(() => PlacementStatus));
	}

	private string GetPlacementStatusTemplateText(DisplayTemplate template)
	{
		if (template == null)
		{
			return string.Empty;
		}
		string mirrorModeLabel = GetMirrorModeLabel(AddinConfiguration.PlacementMirrorMode);
		return string.IsNullOrWhiteSpace(mirrorModeLabel) ? template.DisplayName : (template.DisplayName + " (" + mirrorModeLabel + ")");
	}

	private static string GetMirrorModeLabel(PlacementMirrorMode mirrorMode)
	{
		if (1 == 0)
		{
		}
		string result = mirrorMode switch
		{
			PlacementMirrorMode.Horizontal => "Horizontal", 
			PlacementMirrorMode.Vertical => "Vertical", 
			PlacementMirrorMode.Both => "Both", 
			_ => string.Empty, 
		};
		if (1 == 0)
		{
		}
		return result;
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
			IEnumerable<DisplayTemplate> favouriteTemplates = _favouriteTemplates;
			return favouriteTemplates ?? Enumerable.Empty<DisplayTemplate>();
		}
		if (_showRecentTemplates)
		{
			IEnumerable<DisplayTemplate> favouriteTemplates = _recentTemplates;
			return favouriteTemplates ?? Enumerable.Empty<DisplayTemplate>();
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
		if (FrameworkApplication.DockPaneManager.Find("TemplateEditor_EditorDockpane") is EditorDockpaneViewModel editorDockpaneViewModel)
		{
			editorDockpaneViewModel.BuildFavouriteAndRecentLists();
			if (refreshVisibleTemplates && (editorDockpaneViewModel._showFavouriteTemplates || editorDockpaneViewModel._showRecentTemplates))
			{
				editorDockpaneViewModel.FilterTemplates();
			}
		}
	}

	private static Dictionary<string, DisplayTemplate> BuildAllTemplatesByKey(IEnumerable<DisplayTemplate> simpleTemplates, IEnumerable<DisplayTemplate> groupTemplates)
	{
		Dictionary<string, DisplayTemplate> dictionary = new Dictionary<string, DisplayTemplate>(StringComparer.OrdinalIgnoreCase);
		foreach (DisplayTemplate item in simpleTemplates ?? Enumerable.Empty<DisplayTemplate>())
		{
			dictionary[item.UniqueKey] = item;
		}
		foreach (DisplayTemplate item2 in groupTemplates ?? Enumerable.Empty<DisplayTemplate>())
		{
			dictionary[item2.UniqueKey] = item2;
			IEnumerable<DisplayTemplateChild> childTemplates = item2.ChildTemplates;
			foreach (DisplayTemplateChild item3 in childTemplates ?? Enumerable.Empty<DisplayTemplateChild>())
			{
				DisplayTemplate displayTemplate = new DisplayTemplate
				{
					Name = item3.Name,
					TemplateType = item3.TemplateType,
					Description = item3.Description,
					IsGroupChild = true,
					ParentTemplateName = item3.ParentTemplateName,
					FeatureId = item3.FeatureId,
					SketchType = item3.SketchType
				};
				dictionary[displayTemplate.UniqueKey] = displayTemplate;
			}
		}
		return dictionary;
	}

	private void BuildFavouriteAndRecentLists()
	{
		IEnumerable<string> enumerable = AddinConfiguration.Settings?.FavouriteTemplateKeys;
		HashSet<string> favouriteKeys = new HashSet<string>(enumerable ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
		IEnumerable<DisplayTemplate> enumerable2 = _allTemplatesByKey?.Values;
		_favouriteTemplates = (enumerable2 ?? Enumerable.Empty<DisplayTemplate>()).Where((DisplayTemplate t) => favouriteKeys.Contains(t.UniqueKey)).Select(CloneForFlatList).OrderBy<DisplayTemplate, string>((DisplayTemplate t) => t.Name, StringComparer.OrdinalIgnoreCase)
			.ToList();
		enumerable = AddinConfiguration.Settings?.RecentTemplateKeys;
		_recentTemplates = (from key in enumerable ?? Enumerable.Empty<string>()
			select (_allTemplatesByKey != null && _allTemplatesByKey.TryGetValue(key, out var value)) ? value : null into t
			where t != null
			select t).Select(CloneForFlatList).ToList();
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
			ChildTemplates = ((template.ChildTemplates == null) ? new List<DisplayTemplateChild>() : new List<DisplayTemplateChild>(template.ChildTemplates))
		};
	}

	private void ToggleFavourite(object parameter)
	{
		if (parameter is DisplayTemplate displayTemplate)
		{
			AddinConfiguration.ToggleFavourite(displayTemplate.UniqueKey);
			BuildFavouriteAndRecentLists();
			FilterTemplates();
		}
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
		string text = parameter as string;
		if (!string.IsNullOrWhiteSpace(text))
		{
			if (string.Equals(_sortField, text, StringComparison.Ordinal))
			{
				_sortAscending = !_sortAscending;
			}
			else
			{
				_sortField = text;
				_sortAscending = true;
			}
			FilterTemplates();
		}
	}

	private IEnumerable<DisplayTemplate> ApplySort(IEnumerable<DisplayTemplate> templates)
	{
		string sortField = _sortField;
		if (1 == 0)
		{
		}
		Func<DisplayTemplate, string> func = ((sortField == "TemplateType") ? ((Func<DisplayTemplate, string>)((DisplayTemplate template) => template.TemplateType)) : ((!(sortField == "Description")) ? ((Func<DisplayTemplate, string>)((DisplayTemplate template) => template.Name)) : ((Func<DisplayTemplate, string>)((DisplayTemplate template) => template.Description))));
		if (1 == 0)
		{
		}
		Func<DisplayTemplate, string> keySelector = func;
		return _sortAscending ? templates.OrderBy<DisplayTemplate, string>(keySelector, StringComparer.OrdinalIgnoreCase).ThenBy<DisplayTemplate, string>((DisplayTemplate template) => template.Name, StringComparer.OrdinalIgnoreCase) : templates.OrderByDescending<DisplayTemplate, string>(keySelector, StringComparer.OrdinalIgnoreCase).ThenBy<DisplayTemplate, string>((DisplayTemplate template) => template.Name, StringComparer.OrdinalIgnoreCase);
	}

	private static string[] GetSearchTerms(string searchText)
	{
		if (string.IsNullOrWhiteSpace(searchText) || searchText.Trim().Length < 2)
		{
			return Array.Empty<string>();
		}
		return (from term in searchText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
			select term.Trim() into term
			where term.Length > 0
			select term).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
	}

	private static bool MatchesSearchTerms(DisplayTemplate template, IReadOnlyList<string> searchTerms)
	{
		IEnumerable<string> second = template.ChildTemplates?.Select((DisplayTemplateChild childTemplate) => string.Join(" ", childTemplate.Name, childTemplate.TemplateType, childTemplate.Description, childTemplate.SketchType)) ?? Enumerable.Empty<string>();
		string searchableText = string.Join(" ", new string[3] { template.Name, template.TemplateType, template.Description }.Concat(second));
		return searchTerms.All((string term) => searchableText.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
	}

	internal static void Show()
	{
		DockPane val = FrameworkApplication.DockPaneManager.Find("TemplateEditor_EditorDockpane");
		if (val != null)
		{
			val.Activate();
		}
	}
}
