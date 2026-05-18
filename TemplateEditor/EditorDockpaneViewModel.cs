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

	private List<DisplayTemplate> _simpleTemplates;

	private List<DisplayTemplate> _groupTemplates;

	private List<DisplayTemplate> _allTemplates;

	private DisplayTemplate _selectedTemplate;

	private string _searchText;

	private bool _showGroupTemplates;

	private bool _showSimpleTemplates;

	private bool _showAllTemplates;

	private string _sortField = "Name";

	private bool _sortAscending = true;

	private int _activationVersion;

	public List<DisplayTemplate> Templates { get; set; }

	public string TemplateCount { get; set; }

	public ICommand SortCommand { get; }

	public ICommand ClearSearchCommand { get; }

	public ICommand ActivateSelectedTemplateCommand { get; }

	public ICommand ActivateChildTemplateCommand { get; }

	public ICommand ToggleGroupExpansionCommand { get; }

	public ICommand DeactivateTemplateCommand { get; }

	public ICommand ReloadConfigCommand { get; }

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
			_selectedTemplate = value;
			if (value?.IsGroupChild == true)
			{
				ActivateChildTemplate(value);
			}
			else
			{
				AddinConfiguration.SelectedTemplate = value;
				ActivateTemplate(value);
			}
			NotifyPropertyChanged(() => SelectedTemplate);
		}
	}

	private static void ActivateTemplate(DisplayTemplate selectedTemplate)
	{
		if (selectedTemplate == null)
		{
			return;
		}
		EditorDockpaneViewModel viewModel = FrameworkApplication.DockPaneManager.Find(_dockPaneID) as EditorDockpaneViewModel;
		int activationVersion = viewModel == null ? 0 : ++viewModel._activationVersion;
		_ = ActivateSelectedTemplateToolAsync(selectedTemplate, activationVersion);
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
			SimpleTemplate simpleTemplate = AddinConfiguration.Templates.SimpleTemplates.FirstOrDefault((SimpleTemplate n) => n.Name == AddinConfiguration.SelectedTemplate.Name);
			return simpleTemplate == null || simpleTemplate.Geometry == null ? "TemplateEditor_SketchPolygonTool" : "TemplateEditor_SketchPointTool";
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

	protected EditorDockpaneViewModel()
	{
		SortCommand = new RelayCommand(SortTemplates);
		ClearSearchCommand = new RelayCommand(_ => SearchText = string.Empty);
		ActivateSelectedTemplateCommand = new RelayCommand(_ =>
		{
			if (SelectedTemplate?.IsGroupChild == true)
			{
				ActivateChildTemplate(SelectedTemplate);
				return;
			}
			ActivateTemplate(SelectedTemplate);
		});
		ActivateChildTemplateCommand = new RelayCommand(ActivateChildTemplate);
		ToggleGroupExpansionCommand = new RelayCommand(ToggleGroupExpansion);
		DeactivateTemplateCommand = new RelayCommand(_ => DeactivateTemplate());
		ReloadConfigCommand = new RelayCommand(_ => ReloadTemplateConfig());
		LoadTemplatesFromConfig();
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
		_selectedTemplate = childRow;
		AddinConfiguration.SelectedTemplate = childRow;
		NotifyPropertyChanged(() => SelectedTemplate);
		ActivateTemplate(childRow);
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
			_selectedTemplate = template;
			AddinConfiguration.SelectedTemplate = template;
			NotifyPropertyChanged(() => SelectedTemplate);
		}
		FilterTemplates();
	}

	private void DeactivateTemplate()
	{
		_activationVersion++;
		_selectedTemplate = null;
		AddinConfiguration.SelectedTemplate = null;
		NotifyPropertyChanged(() => SelectedTemplate);
		ToolReactivationService.ActivateSelectTool();
	}

	private void ReloadTemplateConfig()
	{
		try
		{
			string selectedTemplateName = SelectedTemplate?.Name;
			LoadTemplatesFromConfig();
			_selectedTemplate = Templates.FirstOrDefault((DisplayTemplate template) => string.Equals(template.Name, selectedTemplateName, StringComparison.OrdinalIgnoreCase));
			AddinConfiguration.SelectedTemplate = _selectedTemplate;
			NotifyPropertyChanged(() => SelectedTemplate);
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
			AddinConfiguration.Templates = AddinConfiguration.LoadTemplateConfig();
			DialogService.Show("Template configuration reloaded.", "Template Editor");
		}
		catch (Exception ex)
		{
			DialogService.Show(ex.Message, "Template Editor");
		}
	}

	private void LoadTemplatesFromConfig()
	{
		AddinConfiguration.Templates = AddinConfiguration.LoadTemplateConfig();
		Dictionary<string, SimpleTemplate> simpleTemplatesByName = AddinConfiguration.Templates.SimpleTemplates
			.GroupBy((SimpleTemplate template) => template.Name, StringComparer.OrdinalIgnoreCase)
			.ToDictionary((IGrouping<string, SimpleTemplate> group) => group.Key, (IGrouping<string, SimpleTemplate> group) => group.First(), StringComparer.OrdinalIgnoreCase);
		_simpleTemplates = new List<DisplayTemplate>();
		foreach (SimpleTemplate simpleTemplate in AddinConfiguration.Templates.SimpleTemplates)
		{
			if (!AddinConfiguration.Templates.GroupTemplates.Any((GroupTemplate n) => n.SimpleTemplates.Select((SimpleTemplateReference simpleTemplateReference) => simpleTemplateReference.Name).Contains(simpleTemplate.Name)))
			{
				_simpleTemplates.Add(CreateDisplayTemplate(simpleTemplate));
			}
		}
		_groupTemplates = (from n in AddinConfiguration.Templates.GroupTemplates
			select CreateDisplayTemplate(n, simpleTemplatesByName) into n
			orderby n.Name
			select n).ToList();
		List<DisplayTemplate> _allSimpleTemplates = (from n in AddinConfiguration.Templates.SimpleTemplates
			select CreateDisplayTemplate(n) into n
			orderby n.Name
			select n).ToList();
		List<DisplayTemplate> allGroupTemplates = (from n in AddinConfiguration.Templates.GroupTemplates
			select new DisplayTemplate
			{
				Name = n.Name,
				TemplateType = n.TemplateType,
				Description = n.Description
			} into n
			orderby n.Name
			select n).ToList();
		_allTemplates = (from n in _allSimpleTemplates.Concat(allGroupTemplates)
			orderby n.Name
			select n).ToList();
		Templates = ApplySort(_groupTemplates).ToList();
		TemplateCount = $"{Templates.Count} template(s)";
		if (!_showGroupTemplates && !_showSimpleTemplates && !_showAllTemplates)
		{
			_showGroupTemplates = true;
		}
		FilterTemplates();
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
		IEnumerable<DisplayTemplate> source = Enumerable.Empty<DisplayTemplate>();
		if (_showGroupTemplates)
		{
			source = _groupTemplates;
		}
		else if (_showSimpleTemplates)
		{
			source = _simpleTemplates;
		}
		else if (_showAllTemplates)
		{
			source = _allTemplates;
		}
		if (!_showGroupTemplates && _selectedTemplate?.IsGroupChild == true)
		{
			_selectedTemplate = null;
			AddinConfiguration.SelectedTemplate = null;
			NotifyPropertyChanged(() => SelectedTemplate);
		}

		string[] searchTerms = GetSearchTerms(_searchText);
		if (searchTerms.Length > 0)
		{
			source = source.Where((DisplayTemplate template) => MatchesSearchTerms(template, searchTerms));
		}

		List<DisplayTemplate> visibleTemplates = ApplySort(source).ToList();
		Templates = _showGroupTemplates ? ExpandGroupRows(visibleTemplates).ToList() : visibleTemplates;
		TemplateCount = $"{Templates.Count} template(s)";
		NotifyPropertyChanged(() => Templates);
		NotifyPropertyChanged(() => TemplateCount);
	}

	internal void RefreshTemplateRows()
	{
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
