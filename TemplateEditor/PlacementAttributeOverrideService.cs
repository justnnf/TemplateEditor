using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using DataDomain = ArcGIS.Core.Data.Domain;
using DataSubtype = ArcGIS.Core.Data.Subtype;

namespace TemplateEditor;

internal static class PlacementAttributeOverrideService
{
	private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true,
		WriteIndented = true
	};

	private static readonly string FavouriteDirectoryPath =
		Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FortisAlberta", "TemplateEditor");

	private static readonly string FavouriteFilePath = Path.Combine(FavouriteDirectoryPath, "placement-override-favourites.json");

	private static readonly string[] AlwaysVisiblePlacementEditorFields =
	{
		"PHASESNORMAL",
		"VOLTAGEGROUP",
		"OWNEDBY",
		"MAINTBY"
	};

	private static readonly object _syncRoot = new object();

	private static List<PlacementAttributeOverrideDefinition> _definitions = new List<PlacementAttributeOverrideDefinition>();

	private static Dictionary<string, Dictionary<string, object>> _pendingPlacementValuesByPart =
		new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);

	private static HashSet<string> _placementWarnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	public static IReadOnlyList<PlacementAttributeOverrideDefinition> Definitions => _definitions;

	public static void Initialize()
	{
		LoadDefinitions();
	}

	public static List<PlacementAttributeOverrideValue> NormalizeOverrides(IEnumerable<PlacementAttributeOverrideValue> overrides)
	{
		return (overrides ?? Enumerable.Empty<PlacementAttributeOverrideValue>())
			.Where(value => !string.IsNullOrWhiteSpace(value?.FieldName))
			.GroupBy(value => NormalizeFieldName(value.FieldName), StringComparer.OrdinalIgnoreCase)
			.Select(group =>
			{
				PlacementAttributeOverrideValue latest = group.Last();
				return new PlacementAttributeOverrideValue
				{
					FieldName = group.Key,
					Enabled = latest.Enabled,
					Value = string.IsNullOrWhiteSpace(latest.Value) ? null : latest.Value.Trim()
				};
			})
			.OrderBy(value => value.FieldName, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	public static bool HasSessionOverrides()
	{
		return GetEnabledOverrides(AddinConfiguration.Settings?.SessionAttributeOverrides).Any();
	}

	public static bool HasPendingPlacementOverrides()
	{
		lock (_syncRoot)
		{
			return _pendingPlacementValuesByPart.Any(part => part.Value?.Count > 0);
		}
	}

	public static string GetStatusLabel()
	{
		List<string> parts = new List<string>();
		if (HasSessionOverrides())
		{
			parts.Add("Session attrs on");
		}
		if (HasPendingPlacementOverrides())
		{
			parts.Add("Next-place attrs on");
		}
		return string.Join(" | ", parts);
	}

	public static IReadOnlyList<PlacementAttributeOverrideEditorState> BuildSessionEditorStates(IEnumerable<PlacementAttributeOverrideValue> selectedValues = null)
	{
		List<PlacementAttributeOverrideValue> currentValues = NormalizeOverrides(selectedValues ?? AddinConfiguration.Settings?.SessionAttributeOverrides);
		return BuildEditorStatesAsync(GetAllSimpleTemplates(), currentValues, includeUnavailableDefinitions: true).GetAwaiter().GetResult();
	}

	public static async Task<bool> ConfigureOneTimePlacementOverridesAsync(DisplayTemplate template)
	{
		if (template == null)
		{
			return false;
		}
		PlacementAttributeEditorModel editorModel = await BuildPlacementEditorModelAsync(template).ConfigureAwait(true);
		if (editorModel == null || editorModel.Parts.Count == 0)
		{
			DialogService.Show("No placement targets were found for the selected template.", "Template Editor");
			return false;
		}
		PlacementAttributeOverrideWindow window = new PlacementAttributeOverrideWindow(editorModel);
		if (window.ShowDialog() != true)
		{
			return false;
		}
		lock (_syncRoot)
		{
			_pendingPlacementValuesByPart = BuildPendingPlacementValueMap(window.EditorModel);
		}
		return true;
	}

	public static IReadOnlyList<PlacementAttributeOverrideFavouriteSummary> GetPlacementFavourites(string templateKey)
	{
		string normalizedTemplateKey = NormalizeTemplateKey(templateKey);
		if (string.IsNullOrWhiteSpace(normalizedTemplateKey))
		{
			return new List<PlacementAttributeOverrideFavouriteSummary>();
		}
		PlacementAttributeOverrideFavouriteCatalog catalog = LoadFavouriteCatalog();
		return catalog.Favourites
			.Where(favourite => string.Equals(NormalizeTemplateKey(favourite.TemplateKey), normalizedTemplateKey, StringComparison.OrdinalIgnoreCase))
			.OrderBy(favourite => favourite.Name, StringComparer.OrdinalIgnoreCase)
			.Select(favourite => new PlacementAttributeOverrideFavouriteSummary
			{
				Id = favourite.Id,
				Name = favourite.Name,
				TemplateKey = favourite.TemplateKey,
				TemplateDisplayName = favourite.TemplateDisplayName
			})
			.ToList();
	}

	public static void SavePlacementFavourite(PlacementAttributeEditorModel editorModel, string favouriteName)
	{
		if (editorModel == null)
		{
			throw new ArgumentNullException(nameof(editorModel));
		}
		string normalizedTemplateKey = NormalizeTemplateKey(editorModel.TemplateKey);
		string trimmedName = favouriteName?.Trim();
		if (string.IsNullOrWhiteSpace(normalizedTemplateKey))
		{
			throw new InvalidOperationException("The current placement template does not have a valid favourite key.");
		}
		if (string.IsNullOrWhiteSpace(trimmedName))
		{
			throw new InvalidOperationException("Enter a favourite name.");
		}

		PlacementAttributeOverrideFavouriteCatalog catalog = LoadFavouriteCatalog();
		PlacementAttributeOverrideFavourite existing = catalog.Favourites.FirstOrDefault(favourite =>
			string.Equals(NormalizeTemplateKey(favourite.TemplateKey), normalizedTemplateKey, StringComparison.OrdinalIgnoreCase) &&
			string.Equals(favourite.Name, trimmedName, StringComparison.OrdinalIgnoreCase));
		string timestamp = DateTime.UtcNow.ToString("o");
		if (existing == null)
		{
			existing = new PlacementAttributeOverrideFavourite
			{
				Id = Guid.NewGuid().ToString("N"),
				Name = trimmedName,
				TemplateKey = editorModel.TemplateKey,
				TemplateDisplayName = editorModel.TemplateDisplayName,
				CreatedUtc = timestamp
			};
			catalog.Favourites.Add(existing);
		}
		existing.Name = trimmedName;
		existing.TemplateKey = editorModel.TemplateKey;
		existing.TemplateDisplayName = editorModel.TemplateDisplayName;
		existing.UpdatedUtc = timestamp;
		existing.PartValues = BuildFavouritePartValueMap(editorModel);
		SaveFavouriteCatalog(catalog);
	}

	public static bool DeletePlacementFavourite(string templateKey, string favouriteId)
	{
		string normalizedTemplateKey = NormalizeTemplateKey(templateKey);
		if (string.IsNullOrWhiteSpace(normalizedTemplateKey) || string.IsNullOrWhiteSpace(favouriteId))
		{
			return false;
		}
		PlacementAttributeOverrideFavouriteCatalog catalog = LoadFavouriteCatalog();
		int removed = catalog.Favourites.RemoveAll(favourite =>
			string.Equals(NormalizeTemplateKey(favourite.TemplateKey), normalizedTemplateKey, StringComparison.OrdinalIgnoreCase) &&
			string.Equals(favourite.Id, favouriteId, StringComparison.OrdinalIgnoreCase));
		if (removed <= 0)
		{
			return false;
		}
		SaveFavouriteCatalog(catalog);
		return true;
	}

	public static string ApplyFavouriteToEditorModel(PlacementAttributeEditorModel editorModel, string favouriteId)
	{
		if (editorModel == null || string.IsNullOrWhiteSpace(editorModel.TemplateKey) || string.IsNullOrWhiteSpace(favouriteId))
		{
			return "Choose a saved favourite first.";
		}
		PlacementAttributeOverrideFavourite favourite = LoadFavouriteCatalog().Favourites.FirstOrDefault(candidate =>
			string.Equals(NormalizeTemplateKey(candidate.TemplateKey), NormalizeTemplateKey(editorModel.TemplateKey), StringComparison.OrdinalIgnoreCase) &&
			string.Equals(candidate.Id, favouriteId, StringComparison.OrdinalIgnoreCase));
		if (favourite == null)
		{
			return "The selected favourite could not be found.";
		}

		List<string> warnings = new List<string>();
		bool anyApplied = false;
		foreach (PlacementAttributeEditorPartState part in editorModel.Parts ?? Enumerable.Empty<PlacementAttributeEditorPartState>())
		{
			if (!favourite.PartValues.TryGetValue(part.PartKey ?? string.Empty, out Dictionary<string, string> partValues) || partValues == null)
			{
				continue;
			}
			foreach (PlacementAttributeEditorFieldState field in part.AttributeFields ?? Enumerable.Empty<PlacementAttributeEditorFieldState>())
			{
				if (!partValues.TryGetValue(NormalizeFieldName(field.FieldName), out string favouriteValue))
				{
					continue;
				}
				if (string.IsNullOrWhiteSpace(favouriteValue))
				{
					continue;
				}
				if (field.HasDomainValues &&
					field.AvailableValues?.Count > 0 &&
					!field.AvailableValues.Contains(favouriteValue ?? string.Empty, StringComparer.OrdinalIgnoreCase))
				{
					warnings.Add($"Skipped {field.Label ?? field.FieldName} on {part.DisplayName} because '{favouriteValue}' is no longer valid.");
					continue;
				}
				field.CurrentValue = favouriteValue ?? string.Empty;
				anyApplied = true;
			}
		}

		if (!anyApplied && warnings.Count == 0)
		{
			return "The selected favourite does not contain any values that apply to this template.";
		}
		return warnings.Count == 0 ? null : string.Join(Environment.NewLine, warnings.Distinct(StringComparer.OrdinalIgnoreCase));
	}

	public static void BeginPlacement()
	{
		lock (_syncRoot)
		{
			_placementWarnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		}
	}

	public static string EndPlacementAttempt()
	{
		string warnings = ConsumePlacementWarnings();
		ClearPendingPlacementOverrides();
		return warnings;
	}

	public static string ConsumePlacementWarnings()
	{
		lock (_syncRoot)
		{
			string warnings = BuildWarningsText(_placementWarnings);
			_placementWarnings.Clear();
			return warnings;
		}
	}

	public static bool HasPlacementWarnings()
	{
		lock (_syncRoot)
		{
			return _placementWarnings.Count > 0;
		}
	}

	public static void ClearPendingPlacementOverrides()
	{
		lock (_syncRoot)
		{
			_pendingPlacementValuesByPart = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
		}
	}

	public static async Task<Dictionary<string, object>> ApplyOverridesAsync(
		SimpleTemplate template,
		Dictionary<string, object> defaultFieldValues,
		DataSubtype subtype,
		List<Field> fields,
		string placementPartKey = null)
	{
		Dictionary<string, object> effectiveValues = new Dictionary<string, object>(defaultFieldValues ?? new Dictionary<string, object>(), StringComparer.OrdinalIgnoreCase);
		if (template == null || fields == null || fields.Count == 0)
		{
			return effectiveValues;
		}
		foreach ((PlacementAttributeOverrideDefinition definition, PlacementAttributeOverrideValue value) in GetActiveOverrideSelections())
		{
			Field field = fields.FirstOrDefault(candidate => string.Equals(candidate.Name, definition.FieldName, StringComparison.OrdinalIgnoreCase));
			if (field == null)
			{
				continue;
			}
			OverrideValidationResult validation = await ValidateOverrideAsync(definition, field, subtype, value.Value).ConfigureAwait(false);
			if (!validation.IsValid)
			{
				RegisterPlacementWarning($"Skipped {definition.Label} override '{value.Value}' for {template.Name}.");
				continue;
			}
			effectiveValues[field.Name] = validation.ConfigValue;
		}
		ApplyPendingPlacementValues(effectiveValues, placementPartKey);
		return effectiveValues;
	}

	private static void LoadDefinitions()
	{
		try
		{
			string catalogPath = ResolveCatalogFilePath();
			if (string.IsNullOrWhiteSpace(catalogPath) || !File.Exists(catalogPath))
			{
				_definitions = new List<PlacementAttributeOverrideDefinition>();
				return;
			}
			PlacementAttributeOverrideCatalog catalog = JsonSerializer.Deserialize<PlacementAttributeOverrideCatalog>(File.ReadAllText(catalogPath), _jsonOptions);
			_definitions = (catalog?.Fields ?? new List<PlacementAttributeOverrideDefinition>())
				.Where(definition => !string.IsNullOrWhiteSpace(definition?.FieldName))
				.Select(definition => new PlacementAttributeOverrideDefinition
				{
					FieldName = NormalizeFieldName(definition.FieldName),
					Label = string.IsNullOrWhiteSpace(definition.Label) ? NormalizeFieldName(definition.FieldName) : definition.Label.Trim(),
					Description = string.IsNullOrWhiteSpace(definition.Description) ? null : definition.Description.Trim(),
					DomainName = string.IsNullOrWhiteSpace(definition.DomainName) ? null : definition.DomainName.Trim()
				})
				.GroupBy(definition => definition.FieldName, StringComparer.OrdinalIgnoreCase)
				.Select(group => group.First())
				.ToList();
		}
		catch (Exception ex)
		{
			_definitions = new List<PlacementAttributeOverrideDefinition>();
			LogService.LogException("Placement override definitions could not be loaded.", ex);
		}
	}

	private static IEnumerable<SimpleTemplate> GetAllSimpleTemplates()
	{
		if (AddinConfiguration.Templates?.SimpleTemplates?.Count > 0)
		{
			return AddinConfiguration.Templates.SimpleTemplates;
		}
		try
		{
			return AddinConfiguration.HasValidTemplateConfigPath()
				? AddinConfiguration.LoadTemplateConfig().SimpleTemplates
				: Enumerable.Empty<SimpleTemplate>();
		}
		catch (Exception ex)
		{
			LogService.LogException("Could not load simple templates while resolving placement attribute overrides.", ex);
			return Enumerable.Empty<SimpleTemplate>();
		}
	}

	private static IEnumerable<SimpleTemplate> GetPlacementTargetTemplates(DisplayTemplate displayTemplate)
	{
		if (displayTemplate?.IsGroupChild == true)
		{
			// ✅ Use cache for O(1) lookup instead of O(n) FirstOrDefault
			GroupTemplate parentTemplate = TemplateCache.GetGroupTemplate(displayTemplate.ParentTemplateName);
			SimpleTemplateReference childTemplateRef = parentTemplate?.SimpleTemplates?.FirstOrDefault(reference =>
				reference.FeatureId == displayTemplate.FeatureId &&
				string.Equals(reference.Name, displayTemplate.Name, StringComparison.OrdinalIgnoreCase));
			// ✅ Use cache for O(1) lookup instead of O(n) FirstOrDefault
			SimpleTemplate childTemplate = TemplateCache.GetSimpleTemplate(childTemplateRef?.Name);
			if (childTemplate != null)
			{
				yield return childTemplate;
			}
			yield break;
		}
		// ✅ Use cache for O(1) lookup instead of O(n) FirstOrDefault
		SimpleTemplate simpleTemplate = TemplateCache.GetSimpleTemplate(displayTemplate?.Name);
		if (simpleTemplate != null)
		{
			yield return simpleTemplate;
			yield break;
		}
		// ✅ Use cache for O(1) lookup instead of O(n) FirstOrDefault
		GroupTemplate groupTemplate = TemplateCache.GetGroupTemplate(displayTemplate?.Name);
		foreach (SimpleTemplateReference templateReference in groupTemplate?.SimpleTemplates ?? Enumerable.Empty<SimpleTemplateReference>())
		{
			// ✅ Use cache for O(1) lookup instead of O(n) FirstOrDefault
			SimpleTemplate targetTemplate = TemplateCache.GetSimpleTemplate(templateReference.Name);
			if (targetTemplate != null)
			{
				yield return targetTemplate;
			}
		}
	}

	public static string BuildPlacementPartKey(SimpleTemplate template, string parentTemplateName = null, int featureId = 0)
	{
		if (template == null)
		{
			return null;
		}
		return string.IsNullOrWhiteSpace(parentTemplateName)
			? "SIMPLE|" + template.Name
			: "GROUP|" + parentTemplateName + "|" + featureId + "|" + template.Name;
	}

	private static async Task<PlacementAttributeEditorModel> BuildPlacementEditorModelAsync(DisplayTemplate displayTemplate)
	{
		List<PlacementAttributeEditorPartState> parts = await BuildPlacementEditorPartsAsync(displayTemplate).ConfigureAwait(false);
		if (parts.Count == 0)
		{
			return null;
		}
		return new PlacementAttributeEditorModel
		{
			TemplateKey = displayTemplate.UniqueKey,
			TemplateDisplayName = displayTemplate.DisplayName,
			IsGroupTemplate = parts.Count > 1 || displayTemplate.IsGroupChild != true && AddinConfiguration.Templates?.GroupTemplates?.Any(group => string.Equals(group.Name, displayTemplate.Name, StringComparison.OrdinalIgnoreCase)) == true,
			Parts = parts,
			AvailableFavourites = GetPlacementFavourites(displayTemplate.UniqueKey).ToList()
		};
	}

	private static async Task<List<PlacementAttributeEditorPartState>> BuildPlacementEditorPartsAsync(DisplayTemplate displayTemplate)
	{
		List<PlacementAttributeEditorPartState> parts = new List<PlacementAttributeEditorPartState>();
		if (displayTemplate == null)
		{
			return parts;
		}
		if (displayTemplate.IsGroupChild)
		{
			GroupTemplate parentTemplate = AddinConfiguration.Templates?.GroupTemplates?.FirstOrDefault(group =>
				string.Equals(group.Name, displayTemplate.ParentTemplateName, StringComparison.OrdinalIgnoreCase));
			SimpleTemplateReference childReference = parentTemplate?.SimpleTemplates?.FirstOrDefault(reference =>
				reference.FeatureId == displayTemplate.FeatureId &&
				string.Equals(reference.Name, displayTemplate.Name, StringComparison.OrdinalIgnoreCase));
			SimpleTemplate childTemplate = AddinConfiguration.Templates?.SimpleTemplates?.FirstOrDefault(template =>
				string.Equals(template.Name, childReference?.Name, StringComparison.OrdinalIgnoreCase));
			if (childTemplate != null)
			{
				PlacementAttributeEditorPartState part = await BuildPlacementEditorPartAsync(
					childTemplate,
					BuildPlacementPartKey(childTemplate, displayTemplate.ParentTemplateName, displayTemplate.FeatureId),
					displayTemplate.FeatureId > 0 ? $"{displayTemplate.FeatureId}. {childTemplate.Name}" : childTemplate.Name,
					childTemplate.TemplateType,
					displayTemplate.FeatureId).ConfigureAwait(false);
				if (part != null)
				{
					parts.Add(part);
				}
			}
			return parts;
		}

		SimpleTemplate simpleTemplate = AddinConfiguration.Templates?.SimpleTemplates?.FirstOrDefault(template =>
			string.Equals(template.Name, displayTemplate.Name, StringComparison.OrdinalIgnoreCase));
		if (simpleTemplate != null)
		{
			PlacementAttributeEditorPartState simplePart = await BuildPlacementEditorPartAsync(
				simpleTemplate,
				BuildPlacementPartKey(simpleTemplate),
				simpleTemplate.Name,
				simpleTemplate.TemplateType,
				0).ConfigureAwait(false);
			if (simplePart != null)
			{
				parts.Add(simplePart);
			}
			return parts;
		}

		GroupTemplate groupTemplate = AddinConfiguration.Templates?.GroupTemplates?.FirstOrDefault(template =>
			string.Equals(template.Name, displayTemplate.Name, StringComparison.OrdinalIgnoreCase));
		foreach (SimpleTemplateReference templateReference in groupTemplate?.SimpleTemplates ?? Enumerable.Empty<SimpleTemplateReference>())
		{
			SimpleTemplate targetTemplate = AddinConfiguration.Templates?.SimpleTemplates?.FirstOrDefault(template =>
				string.Equals(template.Name, templateReference.Name, StringComparison.OrdinalIgnoreCase));
			if (targetTemplate == null)
			{
				continue;
			}
			PlacementAttributeEditorPartState part = await BuildPlacementEditorPartAsync(
				targetTemplate,
				BuildPlacementPartKey(targetTemplate, groupTemplate.Name, templateReference.FeatureId),
				templateReference.FeatureId > 0 ? $"{templateReference.FeatureId}. {targetTemplate.Name}" : targetTemplate.Name,
				targetTemplate.TemplateType,
				templateReference.FeatureId).ConfigureAwait(false);
			if (part != null)
			{
				parts.Add(part);
			}
		}
		return parts;
	}

	private static async Task<PlacementAttributeEditorPartState> BuildPlacementEditorPartAsync(
		SimpleTemplate template,
		string partKey,
		string displayName,
		string detailText,
		int featureId)
	{
		if (template == null)
		{
			return null;
		}
		MapMember mapMember = GetMapMemberForTemplate(template);
		if (mapMember == null)
		{
			return null;
		}
		List<PlacementAttributeEditorFieldState> fieldStates = await BuildPlacementFieldStatesAsync(template, mapMember).ConfigureAwait(false);
		if (fieldStates.Count == 0)
		{
			return null;
		}
		return new PlacementAttributeEditorPartState
		{
			PartKey = partKey,
			DisplayName = displayName,
			DetailText = detailText,
			FeatureId = featureId,
			Template = template,
			AttributeFields = fieldStates
		};
	}

	private static async Task<List<PlacementAttributeEditorFieldState>> BuildPlacementFieldStatesAsync(SimpleTemplate template, MapMember mapMember)
	{
		(TableDefinition definition, DataSubtype subtype) = await QueuedTask.Run(() => GetDefinitionAndSubtype(mapMember, template)).ConfigureAwait(false);
		List<Field> fields = await QueuedTask.Run(() => definition?.GetFields()?.ToList() ?? new List<Field>()).ConfigureAwait(false);
		Dictionary<string, object> configuredValues = new Dictionary<string, object>(template.DefaultFieldValues ?? new Dictionary<string, object>(), StringComparer.OrdinalIgnoreCase);
		AddAlwaysVisiblePlacementEditorFields(configuredValues, fields);
		Dictionary<string, object> currentValues = await ApplySessionOverridesOnlyAsync(template, configuredValues, subtype, fields).ConfigureAwait(false);
		return await QueuedTask.Run(() =>
		{
			List<PlacementAttributeEditorFieldState> states = new List<PlacementAttributeEditorFieldState>();
			foreach (string fieldName in configuredValues.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
			{
				if (ShouldHidePlacementEditorField(fieldName))
				{
					continue;
				}
				Field field = fields.FirstOrDefault(candidate => string.Equals(candidate.Name, fieldName, StringComparison.OrdinalIgnoreCase));
				if (field == null)
				{
					continue;
				}
				List<string> domainValues = GetAvailableDomainValues(field, subtype);
				string configuredEditorValue = ConvertToEditorValue(configuredValues.TryGetValue(fieldName, out object configuredValue) ? configuredValue : null);
				string currentEditorValue = ConvertToEditorValue(currentValues.TryGetValue(field.Name, out object currentValue) ? currentValue : null);
				if (!ShouldShowPlacementEditorField(field.Name, configuredEditorValue, currentEditorValue, domainValues))
				{
					continue;
				}
				states.Add(new PlacementAttributeEditorFieldState
				{
					FieldName = field.Name,
					Label = field.AliasName,
					ConfiguredValue = configuredEditorValue,
					CurrentValue = currentEditorValue,
					HasDomainValues = domainValues.Count > 0,
					AvailableValues = domainValues
				});
			}
			return states;
		}).ConfigureAwait(false);
	}

	private static bool ShouldHidePlacementEditorField(string fieldName)
	{
		return string.Equals(fieldName, "ASSETGROUP", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(fieldName, "ASSETTYPE", StringComparison.OrdinalIgnoreCase);
	}

	private static void AddAlwaysVisiblePlacementEditorFields(Dictionary<string, object> configuredValues, List<Field> fields)
	{
		if (configuredValues == null || fields == null || fields.Count == 0)
		{
			return;
		}
		foreach (string preferredFieldName in AlwaysVisiblePlacementEditorFields)
		{
			Field field = fields.FirstOrDefault(candidate => string.Equals(candidate.Name, preferredFieldName, StringComparison.OrdinalIgnoreCase));
			if (field == null || ShouldHidePlacementEditorField(field.Name))
			{
				continue;
			}
			if (!configuredValues.Keys.Any(candidate => string.Equals(candidate, field.Name, StringComparison.OrdinalIgnoreCase)))
			{
				configuredValues[field.Name] = null;
			}
		}
	}

	private static bool ShouldShowPlacementEditorField(string fieldName, string configuredValue, string currentValue, List<string> domainValues)
	{
		if (!string.IsNullOrWhiteSpace(configuredValue) || !string.IsNullOrWhiteSpace(currentValue))
		{
			return true;
		}
		if (domainValues == null || domainValues.Count == 0)
		{
			return !AlwaysVisiblePlacementEditorFields.Contains(NormalizeFieldName(fieldName), StringComparer.OrdinalIgnoreCase);
		}
		return domainValues.Any(value => !IsNotApplicableEditorValue(value));
	}

	private static bool IsNotApplicableEditorValue(string value)
	{
		string normalized = (value ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(normalized))
		{
			return true;
		}
		return string.Equals(normalized, "Not Applicable", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(normalized, "N/A", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(normalized, "NA", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(normalized, "Unknown", StringComparison.OrdinalIgnoreCase);
	}

	private static async Task<IReadOnlyList<PlacementAttributeOverrideEditorState>> BuildEditorStatesAsync(
		IEnumerable<SimpleTemplate> templates,
		IEnumerable<PlacementAttributeOverrideValue> selectedValues,
		bool includeUnavailableDefinitions)
	{
		List<SimpleTemplate> templateList = (templates ?? Enumerable.Empty<SimpleTemplate>()).ToList();
		List<PlacementAttributeOverrideValue> selectedValueList = NormalizeOverrides(selectedValues);
		Dictionary<string, FieldDomainSummary> domainSummaryCache = new Dictionary<string, FieldDomainSummary>(StringComparer.OrdinalIgnoreCase);
		List<PlacementAttributeOverrideEditorState> states = new List<PlacementAttributeOverrideEditorState>();
		foreach (PlacementAttributeOverrideDefinition definition in _definitions)
		{
			OverrideFieldSummary summary = await SummarizeFieldAsync(templateList, definition, domainSummaryCache).ConfigureAwait(false);
			if (!summary.IsApplicable && !includeUnavailableDefinitions)
			{
				continue;
			}
			PlacementAttributeOverrideValue selected = selectedValueList.FirstOrDefault(value =>
				string.Equals(value.FieldName, definition.FieldName, StringComparison.OrdinalIgnoreCase));
			List<string> availableValues = summary.AvailableValues.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
			string editorValue = string.IsNullOrWhiteSpace(selected?.Value)
				? availableValues.FirstOrDefault() ?? summary.FirstConfiguredValue
				: selected.Value;
			if (!string.IsNullOrWhiteSpace(editorValue) &&
				availableValues.Count > 0 &&
				!availableValues.Contains(editorValue, StringComparer.OrdinalIgnoreCase))
			{
				availableValues.Insert(0, editorValue);
			}
			states.Add(new PlacementAttributeOverrideEditorState
			{
				Definition = definition,
				IsEnabled = selected?.Enabled == true,
				Value = editorValue,
				ConfiguredValueSummary = summary.IsApplicable ? summary.ConfiguredValueSummary : "Not currently found in the loaded template configuration.",
				AvailableValues = availableValues
			});
		}
		return states;
	}

	private static IReadOnlyList<PlacementAttributeOverrideEditorState> BuildLightweightEditorStates(
		IEnumerable<SimpleTemplate> templates,
		IEnumerable<PlacementAttributeOverrideValue> selectedValues,
		bool includeUnavailableDefinitions)
	{
		List<SimpleTemplate> templateList = (templates ?? Enumerable.Empty<SimpleTemplate>()).ToList();
		List<PlacementAttributeOverrideValue> selectedValueList = NormalizeOverrides(selectedValues);
		List<PlacementAttributeOverrideEditorState> states = new List<PlacementAttributeOverrideEditorState>();
		foreach (PlacementAttributeOverrideDefinition definition in _definitions)
		{
			HashSet<string> configuredValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			bool isApplicable = false;
			foreach (SimpleTemplate template in templateList)
			{
				Dictionary<string, object> defaultFieldValues = template.DefaultFieldValues ?? new Dictionary<string, object>();
				string configuredFieldName = FindConfiguredFieldName(defaultFieldValues.Keys, definition.FieldName);
				if (configuredFieldName == null)
				{
					continue;
				}
				isApplicable = true;
				string configuredValue = Convert.ToString(CommonFunctions.GetObjectValue(defaultFieldValues[configuredFieldName]));
				if (!string.IsNullOrWhiteSpace(configuredValue))
				{
					configuredValues.Add(configuredValue);
				}
			}
			if (!isApplicable && !includeUnavailableDefinitions)
			{
				continue;
			}
			PlacementAttributeOverrideValue selected = selectedValueList.FirstOrDefault(value =>
				string.Equals(value.FieldName, definition.FieldName, StringComparison.OrdinalIgnoreCase));
			List<string> availableValues = configuredValues.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
			string editorValue = string.IsNullOrWhiteSpace(selected?.Value)
				? availableValues.FirstOrDefault()
				: selected.Value;
			if (!string.IsNullOrWhiteSpace(editorValue) &&
				availableValues.Count > 0 &&
				!availableValues.Contains(editorValue, StringComparer.OrdinalIgnoreCase))
			{
				availableValues.Insert(0, editorValue);
			}
			string configuredValueSummary = configuredValues.Count switch
			{
				0 => isApplicable ? "Configured default varies by template." : "Not currently found in the loaded template configuration.",
				1 => "Configured default: " + configuredValues.First(),
				_ => "Configured defaults: " + string.Join(", ", availableValues)
			};
			states.Add(new PlacementAttributeOverrideEditorState
			{
				Definition = definition,
				IsEnabled = selected?.Enabled == true,
				Value = editorValue,
				ConfiguredValueSummary = configuredValueSummary,
				AvailableValues = availableValues
			});
		}
		return states;
	}

	private static string ResolveCatalogFilePath()
	{
		string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
		string[] candidatePaths =
		{
			Path.Combine(assemblyDirectory, "PlacementAttributeOverrides.json"),
			Path.Combine(assemblyDirectory, "TemplateEditor", "PlacementAttributeOverrides.json")
		};
		return candidatePaths.FirstOrDefault(File.Exists);
	}

	private static async Task<OverrideFieldSummary> SummarizeFieldAsync(IEnumerable<SimpleTemplate> templates, PlacementAttributeOverrideDefinition definition)
	{
		return await SummarizeFieldAsync(templates, definition, new Dictionary<string, FieldDomainSummary>(StringComparer.OrdinalIgnoreCase)).ConfigureAwait(false);
	}

	private static async Task<OverrideFieldSummary> SummarizeFieldAsync(
		IEnumerable<SimpleTemplate> templates,
		PlacementAttributeOverrideDefinition definition,
		Dictionary<string, FieldDomainSummary> domainSummaryCache)
	{
		HashSet<string> configuredValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		HashSet<string> availableValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		bool isApplicable = false;
		foreach (SimpleTemplate template in templates ?? Enumerable.Empty<SimpleTemplate>())
		{
			Dictionary<string, object> defaultFieldValues = template.DefaultFieldValues ?? new Dictionary<string, object>();
			string configuredFieldName = FindConfiguredFieldName(defaultFieldValues.Keys, definition.FieldName);
			if (configuredFieldName != null)
			{
				string configuredValue = Convert.ToString(CommonFunctions.GetObjectValue(defaultFieldValues[configuredFieldName]));
				if (!string.IsNullOrWhiteSpace(configuredValue))
				{
					configuredValues.Add(configuredValue);
				}
			}
			FieldDomainSummary domainSummary = await GetFieldDomainSummaryAsync(template, definition.FieldName, definition.DomainName, domainSummaryCache).ConfigureAwait(false);
			if (!domainSummary.IsApplicable)
			{
				continue;
			}
			isApplicable = true;
			foreach (string value in domainSummary.AvailableValues)
			{
				availableValues.Add(value);
			}
		}
		string configuredValueSummary = configuredValues.Count switch
		{
			0 => isApplicable
				? "The field is available on matching templates even when the config JSON does not set a default."
				: "Not currently found on the loaded template targets.",
			1 => "Configured default: " + configuredValues.First(),
			_ => "Configured defaults: " + string.Join(", ", configuredValues.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
		};
		return new OverrideFieldSummary
		{
			IsApplicable = isApplicable,
			ConfiguredValueSummary = configuredValueSummary,
			AvailableValues = availableValues.ToList(),
			FirstConfiguredValue = configuredValues.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).FirstOrDefault()
		};
	}

	private static async Task<FieldDomainSummary> GetFieldDomainSummaryAsync(
		SimpleTemplate template,
		string fieldName,
		string expectedDomainName,
		Dictionary<string, FieldDomainSummary> cache)
	{
		string cacheKey = BuildFieldDomainSummaryCacheKey(template, fieldName, expectedDomainName);
		if (!string.IsNullOrWhiteSpace(cacheKey) && cache != null && cache.TryGetValue(cacheKey, out FieldDomainSummary cachedSummary))
		{
			return cachedSummary;
		}
		MapMember target = GetMapMemberForTemplate(template);
		if (target == null)
		{
			return CacheFieldDomainSummary(cache, cacheKey, FieldDomainSummary.Empty);
		}
		FieldDomainSummary summary = await QueuedTask.Run(() =>
		{
			(TableDefinition definition, DataSubtype subtype) = GetDefinitionAndSubtype(target, template);
			Field field = definition?.GetFields()?.FirstOrDefault(candidate => string.Equals(candidate.Name, fieldName, StringComparison.OrdinalIgnoreCase));
			if (field == null)
			{
				return FieldDomainSummary.Empty;
			}
			DataDomain domain = field.GetDomain(subtype) ?? field.GetDomain((DataSubtype)null);
			if (domain == null)
			{
				return FieldDomainSummary.Empty;
			}
			string actualDomainName = TryGetDomainName(domain);
			if (!string.IsNullOrWhiteSpace(expectedDomainName) &&
				!string.Equals(expectedDomainName, actualDomainName, StringComparison.OrdinalIgnoreCase))
			{
				return FieldDomainSummary.Empty;
			}
			if (domain is CodedValueDomain codedDomain)
			{
				return new FieldDomainSummary
				{
					IsApplicable = true,
					AvailableValues = codedDomain.GetCodedValuePairs().Values.Select(value => Convert.ToString(value)).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
				};
			}
			return new FieldDomainSummary
			{
				IsApplicable = true
			};
		}).ConfigureAwait(false);
		return CacheFieldDomainSummary(cache, cacheKey, summary);
	}

	private static async Task<OverrideValidationResult> ValidateOverrideAsync(
		PlacementAttributeOverrideDefinition definition,
		Field field,
		DataSubtype subtype,
		string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return OverrideValidationResult.Invalid;
		}
		return await QueuedTask.Run(() =>
		{
			DataDomain domain = field.GetDomain(subtype) ?? field.GetDomain((DataSubtype)null);
			if (domain == null)
			{
				return new OverrideValidationResult(true, value.Trim());
			}
			string actualDomainName = TryGetDomainName(domain);
			if (!string.IsNullOrWhiteSpace(definition.DomainName) &&
				!string.Equals(definition.DomainName, actualDomainName, StringComparison.OrdinalIgnoreCase))
			{
				return OverrideValidationResult.Invalid;
			}
			if (domain is CodedValueDomain codedDomain)
			{
				string match = codedDomain.GetCodedValuePairs().Values
					.Select(candidate => Convert.ToString(candidate))
					.FirstOrDefault(candidate => string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase));
				return string.IsNullOrWhiteSpace(match) ? OverrideValidationResult.Invalid : new OverrideValidationResult(true, match);
			}
			return new OverrideValidationResult(true, value.Trim());
		}).ConfigureAwait(false);
	}

	private static async Task<Dictionary<string, object>> ApplySessionOverridesOnlyAsync(
		SimpleTemplate template,
		Dictionary<string, object> defaultFieldValues,
		DataSubtype subtype,
		List<Field> fields)
	{
		Dictionary<string, object> effectiveValues = new Dictionary<string, object>(defaultFieldValues ?? new Dictionary<string, object>(), StringComparer.OrdinalIgnoreCase);
		if (template == null || fields == null || fields.Count == 0)
		{
			return effectiveValues;
		}
		foreach ((PlacementAttributeOverrideDefinition definition, PlacementAttributeOverrideValue value) in GetSessionOverrideSelections())
		{
			Field field = fields.FirstOrDefault(candidate => string.Equals(candidate.Name, definition.FieldName, StringComparison.OrdinalIgnoreCase));
			if (field == null)
			{
				continue;
			}
			OverrideValidationResult validation = await ValidateOverrideAsync(definition, field, subtype, value.Value).ConfigureAwait(false);
			if (validation.IsValid)
			{
				effectiveValues[field.Name] = validation.ConfigValue;
			}
		}
		return effectiveValues;
	}

	private static IEnumerable<(PlacementAttributeOverrideDefinition Definition, PlacementAttributeOverrideValue Value)> GetSessionOverrideSelections()
	{
		Dictionary<string, PlacementAttributeOverrideValue> valuesByField = GetEnabledOverrides(AddinConfiguration.Settings?.SessionAttributeOverrides)
			.ToDictionary(value => NormalizeFieldName(value.FieldName), StringComparer.OrdinalIgnoreCase);
		foreach (PlacementAttributeOverrideDefinition definition in _definitions)
		{
			if (valuesByField.TryGetValue(definition.FieldName, out PlacementAttributeOverrideValue value))
			{
				yield return (definition, value);
			}
		}
	}

	private static IEnumerable<(PlacementAttributeOverrideDefinition Definition, PlacementAttributeOverrideValue Value)> GetActiveOverrideSelections()
	{
		return GetSessionOverrideSelections();
	}

	private static IEnumerable<PlacementAttributeOverrideValue> GetEnabledOverrides(IEnumerable<PlacementAttributeOverrideValue> overrides)
	{
		return NormalizeOverrides(overrides)
			.Where(value => value.Enabled && !string.IsNullOrWhiteSpace(value.Value));
	}

	private static MapMember GetMapMemberForTemplate(SimpleTemplate template)
	{
		if (template == null || string.IsNullOrWhiteSpace(template.GroupLayer) || string.IsNullOrWhiteSpace(template.SubtypeLayer))
		{
			return null;
		}
		string groupLayerName = template.GroupLayer.ToUpperInvariant();
		if (AddinConfiguration.GroupFeatureLayerNames.Contains(groupLayerName))
		{
			return MapMemberLookupService.GetFeatureLayerByName(template.SubtypeLayer, template.GroupLayer);
		}
		return MapMemberLookupService.GetTableByName(template.SubtypeLayer, template.GroupLayer);
	}

	private static (TableDefinition Definition, DataSubtype Subtype) GetDefinitionAndSubtype(MapMember mapMember, SimpleTemplate template)
	{
		if (mapMember == null)
		{
			return (null, null);
		}
		TableDefinition definition;
		if (mapMember is FeatureLayer featureLayer)
		{
			definition = (TableDefinition)featureLayer.GetFeatureClass().GetDefinition();
		}
		else if (mapMember is StandaloneTable standaloneTable)
		{
			definition = standaloneTable.GetTable().GetDefinition();
		}
		else
		{
			return (null, null);
		}
		string subtypeField = definition.GetSubtypeField();
		if (string.IsNullOrWhiteSpace(subtypeField))
		{
			return (definition, null);
		}
		Dictionary<string, object> defaultFieldValues = template.DefaultFieldValues ?? new Dictionary<string, object>();
		string configuredFieldName = FindConfiguredFieldName(defaultFieldValues.Keys, subtypeField);
		if (configuredFieldName == null)
		{
			return (definition, null);
		}
		string subtypeName = Convert.ToString(CommonFunctions.GetObjectValue(defaultFieldValues[configuredFieldName]));
		DataSubtype subtype = definition.GetSubtypes().FirstOrDefault(candidate => string.Equals(candidate.GetName(), subtypeName, StringComparison.OrdinalIgnoreCase));
		return (definition, subtype);
	}

	private static string FindConfiguredFieldName(IEnumerable<string> fieldNames, string expectedFieldName)
	{
		return (fieldNames ?? Enumerable.Empty<string>())
			.FirstOrDefault(fieldName => string.Equals(fieldName, expectedFieldName, StringComparison.OrdinalIgnoreCase));
	}

	private static string NormalizeFieldName(string fieldName)
	{
		return (fieldName ?? string.Empty).Trim().ToUpperInvariant();
	}

	private static string NormalizeTemplateKey(string templateKey)
	{
		return (templateKey ?? string.Empty).Trim();
	}

	private static string TryGetDomainName(DataDomain domain)
	{
		try
		{
			return domain?.GetName();
		}
		catch (Exception ex)
		{
			LogService.LogException("Could not resolve domain name while evaluating placement attribute overrides.", ex);
			return null;
		}
	}

	private static List<string> GetAvailableDomainValues(Field field, DataSubtype subtype)
	{
		DataDomain domain = field?.GetDomain(subtype) ?? field?.GetDomain((DataSubtype)null);
		if (domain is not CodedValueDomain codedDomain)
		{
			return new List<string>();
		}
		return codedDomain.GetCodedValuePairs().Values
			.Select(value => Convert.ToString(value))
			.Where(value => !string.IsNullOrWhiteSpace(value))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private static string ConvertToEditorValue(object value)
	{
		return Convert.ToString(CommonFunctions.GetObjectValue(value)) ?? string.Empty;
	}

	private static Dictionary<string, Dictionary<string, string>> BuildFavouritePartValueMap(PlacementAttributeEditorModel editorModel)
	{
		Dictionary<string, Dictionary<string, string>> result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
		foreach (PlacementAttributeEditorPartState part in editorModel?.Parts ?? Enumerable.Empty<PlacementAttributeEditorPartState>())
		{
			Dictionary<string, string> fieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (PlacementAttributeEditorFieldState field in part.AttributeFields ?? Enumerable.Empty<PlacementAttributeEditorFieldState>())
			{
				if (!string.IsNullOrWhiteSpace(field.CurrentValue))
				{
					fieldValues[NormalizeFieldName(field.FieldName)] = field.CurrentValue.Trim();
				}
			}
			result[part.PartKey ?? string.Empty] = fieldValues;
		}
		return result;
	}

	private static PlacementAttributeOverrideFavouriteCatalog LoadFavouriteCatalog()
	{
		try
		{
			lock (_syncRoot)
			{
				if (!File.Exists(FavouriteFilePath))
				{
					return new PlacementAttributeOverrideFavouriteCatalog();
				}
				PlacementAttributeOverrideFavouriteCatalog catalog = JsonSerializer.Deserialize<PlacementAttributeOverrideFavouriteCatalog>(File.ReadAllText(FavouriteFilePath), _jsonOptions);
				catalog ??= new PlacementAttributeOverrideFavouriteCatalog();
				catalog.Favourites ??= new List<PlacementAttributeOverrideFavourite>();
				catalog.Favourites = catalog.Favourites
					.Where(favourite => !string.IsNullOrWhiteSpace(favourite?.Id) && !string.IsNullOrWhiteSpace(favourite.TemplateKey))
					.ToList();
				return catalog;
			}
		}
		catch (Exception ex)
		{
			LogService.LogException("Placement override favourites could not be loaded.", ex);
			return new PlacementAttributeOverrideFavouriteCatalog();
		}
	}

	private static void SaveFavouriteCatalog(PlacementAttributeOverrideFavouriteCatalog catalog)
	{
		try
		{
			lock (_syncRoot)
			{
				Directory.CreateDirectory(FavouriteDirectoryPath);
				string json = JsonSerializer.Serialize(catalog ?? new PlacementAttributeOverrideFavouriteCatalog(), _jsonOptions);
				File.WriteAllText(FavouriteFilePath, json);
			}
		}
		catch (Exception ex)
		{
			LogService.LogException("Placement override favourites could not be saved.", ex);
			throw new InvalidOperationException("The placement override favourites file could not be saved.", ex);
		}
	}

	private static Dictionary<string, Dictionary<string, object>> BuildPendingPlacementValueMap(PlacementAttributeEditorModel editorModel)
	{
		Dictionary<string, Dictionary<string, object>> result =
			new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
		foreach (PlacementAttributeEditorPartState part in editorModel?.Parts ?? Enumerable.Empty<PlacementAttributeEditorPartState>())
		{
			Dictionary<string, object> fieldValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
			foreach (PlacementAttributeEditorFieldState field in part.AttributeFields ?? Enumerable.Empty<PlacementAttributeEditorFieldState>())
			{
				fieldValues[field.FieldName] = string.IsNullOrWhiteSpace(field.CurrentValue) ? null : field.CurrentValue.Trim();
			}
			if (fieldValues.Count > 0 && !string.IsNullOrWhiteSpace(part.PartKey))
			{
				result[part.PartKey] = fieldValues;
			}
		}
		return result;
	}

	private static void ApplyPendingPlacementValues(Dictionary<string, object> effectiveValues, string placementPartKey)
	{
		if (effectiveValues == null || string.IsNullOrWhiteSpace(placementPartKey))
		{
			return;
		}
		Dictionary<string, object> pendingValues = null;
		lock (_syncRoot)
		{
			if (!_pendingPlacementValuesByPart.TryGetValue(placementPartKey, out pendingValues))
			{
				return;
			}
			pendingValues = new Dictionary<string, object>(pendingValues, StringComparer.OrdinalIgnoreCase);
		}
		foreach ((string fieldName, object fieldValue) in pendingValues)
		{
			effectiveValues[fieldName] = fieldValue;
		}
	}

	private static string BuildFieldDomainSummaryCacheKey(SimpleTemplate template, string fieldName, string expectedDomainName)
	{
		if (template == null || string.IsNullOrWhiteSpace(fieldName))
		{
			return null;
		}
		return string.Join("|",
			template.GroupLayer ?? string.Empty,
			template.SubtypeLayer ?? string.Empty,
			NormalizeFieldName(fieldName),
			(expectedDomainName ?? string.Empty).Trim());
	}

	private static FieldDomainSummary CacheFieldDomainSummary(
		Dictionary<string, FieldDomainSummary> cache,
		string cacheKey,
		FieldDomainSummary summary)
	{
		if (cache != null && !string.IsNullOrWhiteSpace(cacheKey))
		{
			cache[cacheKey] = summary ?? FieldDomainSummary.Empty;
		}
		return summary ?? FieldDomainSummary.Empty;
	}

	private static void RegisterPlacementWarning(string warning)
	{
		if (string.IsNullOrWhiteSpace(warning))
		{
			return;
		}
		lock (_syncRoot)
		{
			_placementWarnings.Add(warning.Trim());
		}
	}

	private static string BuildWarningsText(IEnumerable<string> warnings)
	{
		List<string> warningList = (warnings ?? Enumerable.Empty<string>()).Where(warning => !string.IsNullOrWhiteSpace(warning)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		if (warningList.Count == 0)
		{
			return null;
		}
		return "Override notes:\n" + string.Join("\n", warningList.Select(warning => "- " + warning));
	}

	private sealed class OverrideFieldSummary
	{
		public bool IsApplicable { get; set; }

		public string ConfiguredValueSummary { get; set; }

		public List<string> AvailableValues { get; set; } = new List<string>();

		public string FirstConfiguredValue { get; set; }
	}

	private sealed class FieldDomainSummary
	{
		public static FieldDomainSummary Empty { get; } = new FieldDomainSummary();

		public bool IsApplicable { get; set; }

		public List<string> AvailableValues { get; set; } = new List<string>();
	}

	private readonly struct OverrideValidationResult
	{
		public static OverrideValidationResult Invalid { get; } = new OverrideValidationResult(false, null);

		public OverrideValidationResult(bool isValid, string configValue)
		{
			IsValid = isValid;
			ConfigValue = configValue;
		}

		public bool IsValid { get; }

		public string ConfigValue { get; }
	}
}
