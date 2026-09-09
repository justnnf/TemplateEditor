using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace TemplateEditor;

internal static class PlacementAttributeOverrideService
{
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
		public static OverrideValidationResult Invalid { get; } = new OverrideValidationResult(isValid: false, null);

		public bool IsValid { get; }

		public string ConfigValue { get; }

		public OverrideValidationResult(bool isValid, string configValue)
		{
			IsValid = isValid;
			ConfigValue = configValue;
		}
	}

	private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true,
		WriteIndented = true
	};

	private static readonly string FavouriteDirectoryPath = AddinConfiguration.UserDataDirectoryPath;

	private static readonly string FavouriteFilePath = Path.Combine(FavouriteDirectoryPath, "placement-override-favourites.json");

	private static readonly string[] AlwaysVisiblePlacementEditorFields = new string[4] { "PHASESNORMAL", "VOLTAGEGROUP", "OWNEDBY", "MAINTBY" };

	private static readonly object _syncRoot = new object();

	private static List<PlacementAttributeOverrideDefinition> _definitions = new List<PlacementAttributeOverrideDefinition>();

	private static Dictionary<string, Dictionary<string, object>> _pendingPlacementValuesByPart = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);

	private static HashSet<string> _placementWarnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	public static IReadOnlyList<PlacementAttributeOverrideDefinition> Definitions => _definitions;

	public static void Initialize()
	{
		LoadDefinitions();
	}

	public static List<PlacementAttributeOverrideValue> NormalizeOverrides(IEnumerable<PlacementAttributeOverrideValue> overrides)
	{
		return (overrides ?? Enumerable.Empty<PlacementAttributeOverrideValue>()).Where((PlacementAttributeOverrideValue value) => !string.IsNullOrWhiteSpace(value?.FieldName)).GroupBy<PlacementAttributeOverrideValue, string>((PlacementAttributeOverrideValue value) => NormalizeFieldName(value.FieldName), StringComparer.OrdinalIgnoreCase).Select(delegate(IGrouping<string, PlacementAttributeOverrideValue> group)
		{
			PlacementAttributeOverrideValue placementAttributeOverrideValue = group.Last();
			return new PlacementAttributeOverrideValue
			{
				FieldName = group.Key,
				Enabled = placementAttributeOverrideValue.Enabled,
				Value = (string.IsNullOrWhiteSpace(placementAttributeOverrideValue.Value) ? null : placementAttributeOverrideValue.Value.Trim())
			};
		})
			.OrderBy<PlacementAttributeOverrideValue, string>((PlacementAttributeOverrideValue value) => value.FieldName, StringComparer.OrdinalIgnoreCase)
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
			return _pendingPlacementValuesByPart.Any(delegate(KeyValuePair<string, Dictionary<string, object>> part)
			{
				Dictionary<string, object> value = part.Value;
				return value != null && value.Count > 0;
			});
		}
	}

	public static string GetStatusLabel()
	{
		List<string> list = new List<string>();
		if (HasSessionOverrides())
		{
			list.Add("Session attrs on");
		}
		if (HasPendingPlacementOverrides())
		{
			list.Add("Next-place attrs on");
		}
		return string.Join(" | ", list);
	}

	public static async Task<IReadOnlyList<PlacementAttributeOverrideEditorState>> BuildSessionEditorStatesAsync(IEnumerable<PlacementAttributeOverrideValue> selectedValues = null)
	{
		List<PlacementAttributeOverrideValue> selectedValues2 = NormalizeOverrides(selectedValues ?? AddinConfiguration.Settings?.SessionAttributeOverrides);
		return await BuildEditorStatesAsync(GetAllSimpleTemplates(), selectedValues2, includeUnavailableDefinitions: true);
	}

	public static async Task<bool> ConfigureOneTimePlacementOverridesAsync(DisplayTemplate template)
	{
		if (template == null)
		{
			return false;
		}
		PlacementAttributeEditorModel editorModel = await BuildPlacementEditorModelAsync(template).ConfigureAwait(continueOnCapturedContext: true);
		if (editorModel == null || editorModel.Parts.Count == 0)
		{
			DialogService.Show("No placement targets were found for the selected template.", "Template Editor");
			return false;
		}
		PlacementAttributeOverrideWindow window = new PlacementAttributeOverrideWindow(editorModel)
		{
			Owner = Application.Current?.MainWindow
		};
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
		PlacementAttributeOverrideFavouriteCatalog placementAttributeOverrideFavouriteCatalog = LoadFavouriteCatalog();
		return (from favourite in placementAttributeOverrideFavouriteCatalog.Favourites.Where((PlacementAttributeOverrideFavourite favourite) => string.Equals(NormalizeTemplateKey(favourite.TemplateKey), normalizedTemplateKey, StringComparison.OrdinalIgnoreCase)).OrderBy<PlacementAttributeOverrideFavourite, string>((PlacementAttributeOverrideFavourite favourite) => favourite.Name, StringComparer.OrdinalIgnoreCase)
			select new PlacementAttributeOverrideFavouriteSummary
			{
				Id = favourite.Id,
				Name = favourite.Name,
				TemplateKey = favourite.TemplateKey,
				TemplateDisplayName = favourite.TemplateDisplayName
			}).ToList();
	}

	public static void SavePlacementFavourite(PlacementAttributeEditorModel editorModel, string favouriteName)
	{
		if (editorModel == null)
		{
			throw new ArgumentNullException("editorModel");
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
		PlacementAttributeOverrideFavouriteCatalog placementAttributeOverrideFavouriteCatalog = LoadFavouriteCatalog();
		PlacementAttributeOverrideFavourite placementAttributeOverrideFavourite = placementAttributeOverrideFavouriteCatalog.Favourites.FirstOrDefault((PlacementAttributeOverrideFavourite favourite) => string.Equals(NormalizeTemplateKey(favourite.TemplateKey), normalizedTemplateKey, StringComparison.OrdinalIgnoreCase) && string.Equals(favourite.Name, trimmedName, StringComparison.OrdinalIgnoreCase));
		string text = DateTime.UtcNow.ToString("o");
		if (placementAttributeOverrideFavourite == null)
		{
			placementAttributeOverrideFavourite = new PlacementAttributeOverrideFavourite
			{
				Id = Guid.NewGuid().ToString("N"),
				Name = trimmedName,
				TemplateKey = editorModel.TemplateKey,
				TemplateDisplayName = editorModel.TemplateDisplayName,
				CreatedUtc = text
			};
			placementAttributeOverrideFavouriteCatalog.Favourites.Add(placementAttributeOverrideFavourite);
		}
		placementAttributeOverrideFavourite.Name = trimmedName;
		placementAttributeOverrideFavourite.TemplateKey = editorModel.TemplateKey;
		placementAttributeOverrideFavourite.TemplateDisplayName = editorModel.TemplateDisplayName;
		placementAttributeOverrideFavourite.UpdatedUtc = text;
		placementAttributeOverrideFavourite.PartValues = BuildFavouritePartValueMap(editorModel);
		SaveFavouriteCatalog(placementAttributeOverrideFavouriteCatalog);
	}

	public static bool DeletePlacementFavourite(string templateKey, string favouriteId)
	{
		string normalizedTemplateKey = NormalizeTemplateKey(templateKey);
		if (string.IsNullOrWhiteSpace(normalizedTemplateKey) || string.IsNullOrWhiteSpace(favouriteId))
		{
			return false;
		}
		PlacementAttributeOverrideFavouriteCatalog placementAttributeOverrideFavouriteCatalog = LoadFavouriteCatalog();
		int num = placementAttributeOverrideFavouriteCatalog.Favourites.RemoveAll((PlacementAttributeOverrideFavourite favourite) => string.Equals(NormalizeTemplateKey(favourite.TemplateKey), normalizedTemplateKey, StringComparison.OrdinalIgnoreCase) && string.Equals(favourite.Id, favouriteId, StringComparison.OrdinalIgnoreCase));
		if (num <= 0)
		{
			return false;
		}
		SaveFavouriteCatalog(placementAttributeOverrideFavouriteCatalog);
		return true;
	}

	public static string ApplyFavouriteToEditorModel(PlacementAttributeEditorModel editorModel, string favouriteId)
	{
		if (editorModel == null || string.IsNullOrWhiteSpace(editorModel.TemplateKey) || string.IsNullOrWhiteSpace(favouriteId))
		{
			return "Choose a saved favourite first.";
		}
		PlacementAttributeOverrideFavourite placementAttributeOverrideFavourite = LoadFavouriteCatalog().Favourites.FirstOrDefault((PlacementAttributeOverrideFavourite candidate) => string.Equals(NormalizeTemplateKey(candidate.TemplateKey), NormalizeTemplateKey(editorModel.TemplateKey), StringComparison.OrdinalIgnoreCase) && string.Equals(candidate.Id, favouriteId, StringComparison.OrdinalIgnoreCase));
		if (placementAttributeOverrideFavourite == null)
		{
			return "The selected favourite could not be found.";
		}
		List<string> list = new List<string>();
		bool flag = false;
		IEnumerable<PlacementAttributeEditorPartState> parts = editorModel.Parts;
		foreach (PlacementAttributeEditorPartState item in parts ?? Enumerable.Empty<PlacementAttributeEditorPartState>())
		{
			if (!placementAttributeOverrideFavourite.PartValues.TryGetValue(item.PartKey ?? string.Empty, out var value) || value == null)
			{
				continue;
			}
			IEnumerable<PlacementAttributeEditorFieldState> attributeFields = item.AttributeFields;
			foreach (PlacementAttributeEditorFieldState item2 in attributeFields ?? Enumerable.Empty<PlacementAttributeEditorFieldState>())
			{
				if (!value.TryGetValue(NormalizeFieldName(item2.FieldName), out var value2) || string.IsNullOrWhiteSpace(value2))
				{
					continue;
				}
				if (item2.HasDomainValues)
				{
					List<string> availableValues = item2.AvailableValues;
					if (availableValues != null && availableValues.Count > 0 && !item2.AvailableValues.Contains<string>(value2 ?? string.Empty, StringComparer.OrdinalIgnoreCase))
					{
						list.Add($"Skipped {item2.Label ?? item2.FieldName} on {item.DisplayName} because '{value2}' is no longer valid.");
						continue;
					}
				}
				item2.CurrentValue = value2 ?? string.Empty;
				flag = true;
			}
		}
		if (!flag && list.Count == 0)
		{
			return "The selected favourite does not contain any values that apply to this template.";
		}
		return (list.Count == 0) ? null : string.Join(Environment.NewLine, list.Distinct<string>(StringComparer.OrdinalIgnoreCase));
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
		string result = ConsumePlacementWarnings();
		ClearPendingPlacementOverrides();
		return result;
	}

	public static string ConsumePlacementWarnings()
	{
		lock (_syncRoot)
		{
			string result = BuildWarningsText(_placementWarnings);
			_placementWarnings.Clear();
			return result;
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

	public static async Task<Dictionary<string, object>> ApplyOverridesAsync(SimpleTemplate template, Dictionary<string, object> defaultFieldValues, Subtype subtype, List<Field> fields, string placementPartKey = null)
	{
		Dictionary<string, object> effectiveValues = new Dictionary<string, object>(defaultFieldValues ?? new Dictionary<string, object>(), StringComparer.OrdinalIgnoreCase);
		if (template == null || fields == null || fields.Count == 0)
		{
			return effectiveValues;
		}
		foreach (var activeOverrideSelection in GetActiveOverrideSelections())
		{
			PlacementAttributeOverrideDefinition definition = activeOverrideSelection.Definition;
			PlacementAttributeOverrideValue value = activeOverrideSelection.Value;
			Field field = fields.FirstOrDefault((Field candidate) => string.Equals(candidate.Name, definition.FieldName, StringComparison.OrdinalIgnoreCase));
			if (field != null)
			{
				OverrideValidationResult validation = await ValidateOverrideAsync(definition, field, subtype, value.Value).ConfigureAwait(continueOnCapturedContext: false);
				if (!validation.IsValid)
				{
					RegisterPlacementWarning($"Skipped {definition.Label} override '{value.Value}' for {template.Name}.");
				}
				else
				{
					effectiveValues[field.Name] = validation.ConfigValue;
				}
			}
		}
		ApplyPendingPlacementValues(effectiveValues, placementPartKey);
		return effectiveValues;
	}

	private static void LoadDefinitions()
	{
		try
		{
			// The override catalog is optional. If the user has not created one in
			// local app data, placement still works without session override fields.
			string text = ResolveCatalogFilePath();
			if (string.IsNullOrWhiteSpace(text) || !File.Exists(text))
			{
				_definitions = new List<PlacementAttributeOverrideDefinition>();
				return;
			}
			_definitions = (from @group in (from definition in JsonSerializer.Deserialize<PlacementAttributeOverrideCatalog>(File.ReadAllText(text), _jsonOptions)?.Fields ?? new List<PlacementAttributeOverrideDefinition>()
					where !string.IsNullOrWhiteSpace(definition?.FieldName)
					select new PlacementAttributeOverrideDefinition
					{
						FieldName = NormalizeFieldName(definition.FieldName),
						Label = (string.IsNullOrWhiteSpace(definition.Label) ? NormalizeFieldName(definition.FieldName) : definition.Label.Trim()),
						Description = (string.IsNullOrWhiteSpace(definition.Description) ? null : definition.Description.Trim()),
						DomainName = (string.IsNullOrWhiteSpace(definition.DomainName) ? null : definition.DomainName.Trim())
					}).GroupBy<PlacementAttributeOverrideDefinition, string>((PlacementAttributeOverrideDefinition definition) => definition.FieldName, StringComparer.OrdinalIgnoreCase)
				select @group.First()).ToList();
		}
		catch (Exception exception)
		{
			_definitions = new List<PlacementAttributeOverrideDefinition>();
			LogService.LogException("Placement override definitions could not be loaded.", exception);
		}
	}

	private static IEnumerable<SimpleTemplate> GetAllSimpleTemplates()
	{
		TemplateConfig templates = AddinConfiguration.Templates;
		if (templates != null && templates.SimpleTemplates?.Count > 0)
		{
			return AddinConfiguration.Templates.SimpleTemplates;
		}
		try
		{
			IEnumerable<SimpleTemplate> result;
			if (!AddinConfiguration.HasValidTemplateConfigPath())
			{
				result = Enumerable.Empty<SimpleTemplate>();
			}
			else
			{
				IEnumerable<SimpleTemplate> simpleTemplates = AddinConfiguration.LoadTemplateConfig().SimpleTemplates;
				result = simpleTemplates;
			}
			return result;
		}
		catch (Exception exception)
		{
			LogService.LogException("Could not load simple templates while resolving placement attribute overrides.", exception);
			return Enumerable.Empty<SimpleTemplate>();
		}
	}

	private static IEnumerable<SimpleTemplate> GetPlacementTargetTemplates(DisplayTemplate displayTemplate)
	{
		if (displayTemplate?.IsGroupChild ?? false)
		{
			SimpleTemplate childTemplate = TemplateCache.GetSimpleTemplate((TemplateCache.GetGroupTemplate(displayTemplate.ParentTemplateName)?.SimpleTemplates?.FirstOrDefault((SimpleTemplateReference reference) => reference.FeatureId == displayTemplate.FeatureId && string.Equals(reference.Name, displayTemplate.Name, StringComparison.OrdinalIgnoreCase)))?.Name);
			if (childTemplate != null)
			{
				yield return childTemplate;
			}
			yield break;
		}
		SimpleTemplate simpleTemplate = TemplateCache.GetSimpleTemplate(displayTemplate?.Name);
		if (simpleTemplate != null)
		{
			yield return simpleTemplate;
			yield break;
		}
		IEnumerable<SimpleTemplateReference> enumerable = TemplateCache.GetGroupTemplate(displayTemplate?.Name)?.SimpleTemplates;
		foreach (SimpleTemplateReference templateReference in enumerable ?? Enumerable.Empty<SimpleTemplateReference>())
		{
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
		return string.IsNullOrWhiteSpace(parentTemplateName) ? ("SIMPLE|" + template.Name) : ("GROUP|" + parentTemplateName + "|" + featureId + "|" + template.Name);
	}

	private static async Task<PlacementAttributeEditorModel> BuildPlacementEditorModelAsync(DisplayTemplate displayTemplate)
	{
		List<PlacementAttributeEditorPartState> parts = await BuildPlacementEditorPartsAsync(displayTemplate).ConfigureAwait(continueOnCapturedContext: false);
		if (parts.Count == 0)
		{
			return null;
		}
		PlacementAttributeEditorModel obj = new PlacementAttributeEditorModel
		{
			TemplateKey = displayTemplate.UniqueKey,
			TemplateDisplayName = displayTemplate.DisplayName
		};
		int isGroupTemplate;
		if (parts.Count <= 1)
		{
			if (!displayTemplate.IsGroupChild)
			{
				TemplateConfig templates = AddinConfiguration.Templates;
				isGroupTemplate = ((templates != null && templates.GroupTemplates?.Any((GroupTemplate group) => string.Equals(group.Name, displayTemplate.Name, StringComparison.OrdinalIgnoreCase)) == true) ? 1 : 0);
			}
			else
			{
				isGroupTemplate = 0;
			}
		}
		else
		{
			isGroupTemplate = 1;
		}
		obj.IsGroupTemplate = (byte)isGroupTemplate != 0;
		obj.Parts = parts;
		obj.AvailableFavourites = GetPlacementFavourites(displayTemplate.UniqueKey).ToList();
		return obj;
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
			SimpleTemplateReference childReference = (AddinConfiguration.Templates?.GroupTemplates?.FirstOrDefault((GroupTemplate group) => string.Equals(group.Name, displayTemplate.ParentTemplateName, StringComparison.OrdinalIgnoreCase)))?.SimpleTemplates?.FirstOrDefault((SimpleTemplateReference reference) => reference.FeatureId == displayTemplate.FeatureId && string.Equals(reference.Name, displayTemplate.Name, StringComparison.OrdinalIgnoreCase));
			SimpleTemplate childTemplate = AddinConfiguration.Templates?.SimpleTemplates?.FirstOrDefault((SimpleTemplate template) => string.Equals(template.Name, childReference?.Name, StringComparison.OrdinalIgnoreCase));
			if (childTemplate != null)
			{
				PlacementAttributeEditorPartState part = await BuildPlacementEditorPartAsync(childTemplate, BuildPlacementPartKey(childTemplate, displayTemplate.ParentTemplateName, displayTemplate.FeatureId), (displayTemplate.FeatureId > 0) ? $"{displayTemplate.FeatureId}. {childTemplate.Name}" : childTemplate.Name, childTemplate.TemplateType, displayTemplate.FeatureId).ConfigureAwait(continueOnCapturedContext: false);
				if (part != null)
				{
					parts.Add(part);
				}
			}
			return parts;
		}
		SimpleTemplate simpleTemplate = AddinConfiguration.Templates?.SimpleTemplates?.FirstOrDefault((SimpleTemplate template) => string.Equals(template.Name, displayTemplate.Name, StringComparison.OrdinalIgnoreCase));
		if (simpleTemplate != null)
		{
			PlacementAttributeEditorPartState simplePart = await BuildPlacementEditorPartAsync(simpleTemplate, BuildPlacementPartKey(simpleTemplate), simpleTemplate.Name, simpleTemplate.TemplateType, 0).ConfigureAwait(continueOnCapturedContext: false);
			if (simplePart != null)
			{
				parts.Add(simplePart);
			}
			return parts;
		}
		GroupTemplate groupTemplate = AddinConfiguration.Templates?.GroupTemplates?.FirstOrDefault((GroupTemplate template) => string.Equals(template.Name, displayTemplate.Name, StringComparison.OrdinalIgnoreCase));
		IEnumerable<SimpleTemplateReference> enumerable = groupTemplate?.SimpleTemplates;
		foreach (SimpleTemplateReference templateReference in enumerable ?? Enumerable.Empty<SimpleTemplateReference>())
		{
			SimpleTemplate targetTemplate = AddinConfiguration.Templates?.SimpleTemplates?.FirstOrDefault((SimpleTemplate template) => string.Equals(template.Name, templateReference.Name, StringComparison.OrdinalIgnoreCase));
			if (targetTemplate != null)
			{
				PlacementAttributeEditorPartState part2 = await BuildPlacementEditorPartAsync(targetTemplate, BuildPlacementPartKey(targetTemplate, groupTemplate.Name, templateReference.FeatureId), (templateReference.FeatureId > 0) ? $"{templateReference.FeatureId}. {targetTemplate.Name}" : targetTemplate.Name, targetTemplate.TemplateType, templateReference.FeatureId).ConfigureAwait(continueOnCapturedContext: false);
				if (part2 != null)
				{
					parts.Add(part2);
				}
			}
		}
		return parts;
	}

	private static async Task<PlacementAttributeEditorPartState> BuildPlacementEditorPartAsync(SimpleTemplate template, string partKey, string displayName, string detailText, int featureId)
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
		List<PlacementAttributeEditorFieldState> fieldStates = await BuildPlacementFieldStatesAsync(template, mapMember).ConfigureAwait(continueOnCapturedContext: false);
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
		(TableDefinition Definition, Subtype Subtype) tuple = await QueuedTask.Run<(TableDefinition, Subtype)>((Func<(TableDefinition, Subtype)>)(() => GetDefinitionAndSubtype(mapMember, template)), TaskCreationOptions.None).ConfigureAwait(continueOnCapturedContext: false);
		TableDefinition definition = tuple.Definition;
		Subtype subtype = tuple.Subtype;
		List<Field> fields = await QueuedTask.Run<List<Field>>((Func<List<Field>>)delegate
		{
			TableDefinition obj = definition;
			return ((obj == null) ? null : obj.GetFields()?.ToList()) ?? new List<Field>();
		}, TaskCreationOptions.None).ConfigureAwait(continueOnCapturedContext: false);
		Dictionary<string, object> configuredValues = new Dictionary<string, object>(template.DefaultFieldValues ?? new Dictionary<string, object>(), StringComparer.OrdinalIgnoreCase);
		AddAlwaysVisiblePlacementEditorFields(configuredValues, fields);
		Dictionary<string, object> currentValues = await ApplySessionOverridesOnlyAsync(template, configuredValues, subtype, fields).ConfigureAwait(continueOnCapturedContext: false);
		return await QueuedTask.Run<List<PlacementAttributeEditorFieldState>>((Func<List<PlacementAttributeEditorFieldState>>)delegate
		{
			List<PlacementAttributeEditorFieldState> list = new List<PlacementAttributeEditorFieldState>();
			foreach (string fieldName in configuredValues.Keys.OrderBy<string, string>((string name) => name, StringComparer.OrdinalIgnoreCase))
			{
				if (!ShouldHidePlacementEditorField(fieldName))
				{
					Field val = fields.FirstOrDefault((Field candidate) => string.Equals(candidate.Name, fieldName, StringComparison.OrdinalIgnoreCase));
					if (val != null)
					{
						List<string> availableDomainValues = GetAvailableDomainValues(val, subtype);
						string configuredValue = ConvertToEditorValue(configuredValues.TryGetValue(fieldName, out var value) ? value : null);
						string currentValue = ConvertToEditorValue(currentValues.TryGetValue(val.Name, out var value2) ? value2 : null);
						if (ShouldShowPlacementEditorField(val.Name, configuredValue, currentValue, availableDomainValues))
						{
							list.Add(new PlacementAttributeEditorFieldState
							{
								FieldName = val.Name,
								Label = val.AliasName,
								ConfiguredValue = configuredValue,
								CurrentValue = currentValue,
								HasDomainValues = (availableDomainValues.Count > 0),
								AvailableValues = availableDomainValues
							});
						}
					}
				}
			}
			return list;
		}, TaskCreationOptions.None).ConfigureAwait(continueOnCapturedContext: false);
	}

	private static bool ShouldHidePlacementEditorField(string fieldName)
	{
		return string.Equals(fieldName, "ASSETGROUP", StringComparison.OrdinalIgnoreCase) || string.Equals(fieldName, "ASSETTYPE", StringComparison.OrdinalIgnoreCase);
	}

	private static void AddAlwaysVisiblePlacementEditorFields(Dictionary<string, object> configuredValues, List<Field> fields)
	{
		if (configuredValues == null || fields == null || fields.Count == 0)
		{
			return;
		}
		string[] alwaysVisiblePlacementEditorFields = AlwaysVisiblePlacementEditorFields;
		foreach (string preferredFieldName in alwaysVisiblePlacementEditorFields)
		{
			Field field = fields.FirstOrDefault((Field candidate) => string.Equals(candidate.Name, preferredFieldName, StringComparison.OrdinalIgnoreCase));
			if (field != null && !ShouldHidePlacementEditorField(field.Name) && !configuredValues.Keys.Any((string candidate) => string.Equals(candidate, field.Name, StringComparison.OrdinalIgnoreCase)))
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
			return !AlwaysVisiblePlacementEditorFields.Contains<string>(NormalizeFieldName(fieldName), StringComparer.OrdinalIgnoreCase);
		}
		return domainValues.Any((string value) => !IsNotApplicableEditorValue(value));
	}

	private static bool IsNotApplicableEditorValue(string value)
	{
		string text = (value ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			return true;
		}
		return string.Equals(text, "Not Applicable", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "N/A", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "NA", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "Unknown", StringComparison.OrdinalIgnoreCase);
	}

	private static async Task<IReadOnlyList<PlacementAttributeOverrideEditorState>> BuildEditorStatesAsync(IEnumerable<SimpleTemplate> templates, IEnumerable<PlacementAttributeOverrideValue> selectedValues, bool includeUnavailableDefinitions)
	{
		List<SimpleTemplate> templateList = (templates ?? Enumerable.Empty<SimpleTemplate>()).ToList();
		List<PlacementAttributeOverrideValue> selectedValueList = NormalizeOverrides(selectedValues);
		Dictionary<string, FieldDomainSummary> domainSummaryCache = new Dictionary<string, FieldDomainSummary>(StringComparer.OrdinalIgnoreCase);
		List<PlacementAttributeOverrideEditorState> states = new List<PlacementAttributeOverrideEditorState>();
		foreach (PlacementAttributeOverrideDefinition definition in _definitions)
		{
			OverrideFieldSummary summary = await SummarizeFieldAsync(templateList, definition, domainSummaryCache).ConfigureAwait(continueOnCapturedContext: false);
			if (summary.IsApplicable || includeUnavailableDefinitions)
			{
				PlacementAttributeOverrideValue selected = selectedValueList.FirstOrDefault((PlacementAttributeOverrideValue value) => string.Equals(value.FieldName, definition.FieldName, StringComparison.OrdinalIgnoreCase));
				List<string> availableValues = summary.AvailableValues.OrderBy<string, string>((string value) => value, StringComparer.OrdinalIgnoreCase).ToList();
				string editorValue = (string.IsNullOrWhiteSpace(selected?.Value) ? (availableValues.FirstOrDefault() ?? summary.FirstConfiguredValue) : selected.Value);
				if (!string.IsNullOrWhiteSpace(editorValue) && availableValues.Count > 0 && !availableValues.Contains<string>(editorValue, StringComparer.OrdinalIgnoreCase))
				{
					availableValues.Insert(0, editorValue);
				}
				states.Add(new PlacementAttributeOverrideEditorState
				{
					Definition = definition,
					IsEnabled = (selected?.Enabled ?? false),
					Value = editorValue,
					ConfiguredValueSummary = (summary.IsApplicable ? summary.ConfiguredValueSummary : "Not currently found in the loaded template configuration."),
					AvailableValues = availableValues
				});
			}
		}
		return states;
	}

	private static IReadOnlyList<PlacementAttributeOverrideEditorState> BuildLightweightEditorStates(IEnumerable<SimpleTemplate> templates, IEnumerable<PlacementAttributeOverrideValue> selectedValues, bool includeUnavailableDefinitions)
	{
		List<SimpleTemplate> list = (templates ?? Enumerable.Empty<SimpleTemplate>()).ToList();
		List<PlacementAttributeOverrideValue> source = NormalizeOverrides(selectedValues);
		List<PlacementAttributeOverrideEditorState> list2 = new List<PlacementAttributeOverrideEditorState>();
		foreach (PlacementAttributeOverrideDefinition definition in _definitions)
		{
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			bool flag = false;
			foreach (SimpleTemplate item in list)
			{
				Dictionary<string, object> dictionary = item.DefaultFieldValues ?? new Dictionary<string, object>();
				string text = FindConfiguredFieldName(dictionary.Keys, definition.FieldName);
				if (text != null)
				{
					flag = true;
					string text2 = Convert.ToString(CommonFunctions.GetObjectValue(dictionary[text]));
					if (!string.IsNullOrWhiteSpace(text2))
					{
						hashSet.Add(text2);
					}
				}
			}
			if (flag || includeUnavailableDefinitions)
			{
				PlacementAttributeOverrideValue placementAttributeOverrideValue = source.FirstOrDefault((PlacementAttributeOverrideValue value) => string.Equals(value.FieldName, definition.FieldName, StringComparison.OrdinalIgnoreCase));
				List<string> list3 = hashSet.OrderBy<string, string>((string value) => value, StringComparer.OrdinalIgnoreCase).ToList();
				string text3 = (string.IsNullOrWhiteSpace(placementAttributeOverrideValue?.Value) ? list3.FirstOrDefault() : placementAttributeOverrideValue.Value);
				if (!string.IsNullOrWhiteSpace(text3) && list3.Count > 0 && !list3.Contains<string>(text3, StringComparer.OrdinalIgnoreCase))
				{
					list3.Insert(0, text3);
				}
				int count = hashSet.Count;
				if (1 == 0)
				{
				}
				string text4 = count switch
				{
					0 => flag ? "Configured default varies by template." : "Not currently found in the loaded template configuration.", 
					1 => "Configured default: " + hashSet.First(), 
					_ => "Configured defaults: " + string.Join(", ", list3), 
				};
				if (1 == 0)
				{
				}
				string configuredValueSummary = text4;
				list2.Add(new PlacementAttributeOverrideEditorState
				{
					Definition = definition,
					IsEnabled = (placementAttributeOverrideValue?.Enabled ?? false),
					Value = text3,
					ConfiguredValueSummary = configuredValueSummary,
					AvailableValues = list3
				});
			}
		}
		return list2;
	}

	private static string ResolveCatalogFilePath()
	{
		// Override definitions live with the user's add-in data instead of inside
		// the packaged add-in, keeping environment-specific field choices editable.
		return Path.Combine(AddinConfiguration.UserDataDirectoryPath, "PlacementAttributeOverrides.json");
	}

	private static async Task<OverrideFieldSummary> SummarizeFieldAsync(IEnumerable<SimpleTemplate> templates, PlacementAttributeOverrideDefinition definition)
	{
		return await SummarizeFieldAsync(templates, definition, new Dictionary<string, FieldDomainSummary>(StringComparer.OrdinalIgnoreCase)).ConfigureAwait(continueOnCapturedContext: false);
	}

	private static async Task<OverrideFieldSummary> SummarizeFieldAsync(IEnumerable<SimpleTemplate> templates, PlacementAttributeOverrideDefinition definition, Dictionary<string, FieldDomainSummary> domainSummaryCache)
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
			FieldDomainSummary domainSummary = await GetFieldDomainSummaryAsync(template, definition.FieldName, definition.DomainName, domainSummaryCache).ConfigureAwait(continueOnCapturedContext: false);
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
		int count = configuredValues.Count;
		if (1 == 0)
		{
		}
		string text = count switch
		{
			0 => isApplicable ? "The field is available on matching templates even when the config JSON does not set a default." : "Not currently found on the loaded template targets.", 
			1 => "Configured default: " + configuredValues.First(), 
			_ => "Configured defaults: " + string.Join(", ", configuredValues.OrderBy<string, string>((string result) => result, StringComparer.OrdinalIgnoreCase)), 
		};
		if (1 == 0)
		{
		}
		string configuredValueSummary = text;
		return new OverrideFieldSummary
		{
			IsApplicable = isApplicable,
			ConfiguredValueSummary = configuredValueSummary,
			AvailableValues = availableValues.ToList(),
			FirstConfiguredValue = configuredValues.OrderBy<string, string>((string result) => result, StringComparer.OrdinalIgnoreCase).FirstOrDefault()
		};
	}

	private static async Task<FieldDomainSummary> GetFieldDomainSummaryAsync(SimpleTemplate template, string fieldName, string expectedDomainName, Dictionary<string, FieldDomainSummary> cache)
	{
		string cacheKey = BuildFieldDomainSummaryCacheKey(template, fieldName, expectedDomainName);
		if (!string.IsNullOrWhiteSpace(cacheKey) && cache != null && cache.TryGetValue(cacheKey, out var cachedSummary))
		{
			return cachedSummary;
		}
		MapMember target = GetMapMemberForTemplate(template);
		if (target == null)
		{
			return CacheFieldDomainSummary(cache, cacheKey, FieldDomainSummary.Empty);
		}
		return CacheFieldDomainSummary(cache, cacheKey, await QueuedTask.Run<FieldDomainSummary>((Func<FieldDomainSummary>)delegate
		{
			(TableDefinition Definition, Subtype Subtype) definitionAndSubtype = GetDefinitionAndSubtype(target, template);
			TableDefinition item = definitionAndSubtype.Definition;
			Subtype item2 = definitionAndSubtype.Subtype;
			Field val = ((item == null) ? null : item.GetFields()?.FirstOrDefault((Field candidate) => string.Equals(candidate.Name, fieldName, StringComparison.OrdinalIgnoreCase)));
			if (val == null)
			{
				return FieldDomainSummary.Empty;
			}
			Domain val2 = val.GetDomain(item2) ?? val.GetDomain((Subtype)null);
			if (val2 == null)
			{
				return FieldDomainSummary.Empty;
			}
			string b = TryGetDomainName(val2);
			if (!string.IsNullOrWhiteSpace(expectedDomainName) && !string.Equals(expectedDomainName, b, StringComparison.OrdinalIgnoreCase))
			{
				return FieldDomainSummary.Empty;
			}
			CodedValueDomain val3 = (CodedValueDomain)(object)((val2 is CodedValueDomain) ? val2 : null);
			return (val3 != null) ? new FieldDomainSummary
			{
				IsApplicable = true,
				AvailableValues = (from value in val3.GetCodedValuePairs().Values
					select Convert.ToString(value) into value
					where !string.IsNullOrWhiteSpace(value)
					select value).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToList()
			} : new FieldDomainSummary
			{
				IsApplicable = true
			};
		}, TaskCreationOptions.None).ConfigureAwait(continueOnCapturedContext: false));
	}

	private static async Task<OverrideValidationResult> ValidateOverrideAsync(PlacementAttributeOverrideDefinition definition, Field field, Subtype subtype, string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return OverrideValidationResult.Invalid;
		}
		return await QueuedTask.Run<OverrideValidationResult>((Func<OverrideValidationResult>)delegate
		{
			Domain val = field.GetDomain(subtype) ?? field.GetDomain((Subtype)null);
			if (val == null)
			{
				return new OverrideValidationResult(isValid: true, value.Trim());
			}
			string b = TryGetDomainName(val);
			if (!string.IsNullOrWhiteSpace(definition.DomainName) && !string.Equals(definition.DomainName, b, StringComparison.OrdinalIgnoreCase))
			{
				return OverrideValidationResult.Invalid;
			}
			CodedValueDomain val2 = (CodedValueDomain)(object)((val is CodedValueDomain) ? val : null);
			if (val2 != null)
			{
				string text = val2.GetCodedValuePairs().Values.Select((string candidate) => Convert.ToString(candidate)).FirstOrDefault((string candidate) => string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase));
				return string.IsNullOrWhiteSpace(text) ? OverrideValidationResult.Invalid : new OverrideValidationResult(isValid: true, text);
			}
			return new OverrideValidationResult(isValid: true, value.Trim());
		}, TaskCreationOptions.None).ConfigureAwait(continueOnCapturedContext: false);
	}

	private static async Task<Dictionary<string, object>> ApplySessionOverridesOnlyAsync(SimpleTemplate template, Dictionary<string, object> defaultFieldValues, Subtype subtype, List<Field> fields)
	{
		Dictionary<string, object> effectiveValues = new Dictionary<string, object>(defaultFieldValues ?? new Dictionary<string, object>(), StringComparer.OrdinalIgnoreCase);
		if (template == null || fields == null || fields.Count == 0)
		{
			return effectiveValues;
		}
		foreach (var sessionOverrideSelection in GetSessionOverrideSelections())
		{
			PlacementAttributeOverrideDefinition definition = sessionOverrideSelection.Definition;
			PlacementAttributeOverrideValue value = sessionOverrideSelection.Value;
			Field field = fields.FirstOrDefault((Field candidate) => string.Equals(candidate.Name, definition.FieldName, StringComparison.OrdinalIgnoreCase));
			if (field != null)
			{
				OverrideValidationResult validation = await ValidateOverrideAsync(definition, field, subtype, value.Value).ConfigureAwait(continueOnCapturedContext: false);
				if (validation.IsValid)
				{
					effectiveValues[field.Name] = validation.ConfigValue;
				}
			}
		}
		return effectiveValues;
	}

	private static IEnumerable<(PlacementAttributeOverrideDefinition Definition, PlacementAttributeOverrideValue Value)> GetSessionOverrideSelections()
	{
		Dictionary<string, PlacementAttributeOverrideValue> valuesByField = GetEnabledOverrides(AddinConfiguration.Settings?.SessionAttributeOverrides).ToDictionary<PlacementAttributeOverrideValue, string>((PlacementAttributeOverrideValue placementAttributeOverrideValue) => NormalizeFieldName(placementAttributeOverrideValue.FieldName), StringComparer.OrdinalIgnoreCase);
		foreach (PlacementAttributeOverrideDefinition definition in _definitions)
		{
			if (valuesByField.TryGetValue(definition.FieldName, out var value))
			{
				yield return (Definition: definition, Value: value);
			}
			value = null;
		}
	}

	private static IEnumerable<(PlacementAttributeOverrideDefinition Definition, PlacementAttributeOverrideValue Value)> GetActiveOverrideSelections()
	{
		return GetSessionOverrideSelections();
	}

	private static IEnumerable<PlacementAttributeOverrideValue> GetEnabledOverrides(IEnumerable<PlacementAttributeOverrideValue> overrides)
	{
		return from value in NormalizeOverrides(overrides)
			where value.Enabled && !string.IsNullOrWhiteSpace(value.Value)
			select value;
	}

	private static MapMember GetMapMemberForTemplate(SimpleTemplate template)
	{
		if (template == null || string.IsNullOrWhiteSpace(template.GroupLayer) || string.IsNullOrWhiteSpace(template.SubtypeLayer))
		{
			return null;
		}
		string item = template.GroupLayer.ToUpperInvariant();
		if (AddinConfiguration.GroupFeatureLayerNames.Contains(item))
		{
			return (MapMember)(object)MapMemberLookupService.GetFeatureLayerByName(template.SubtypeLayer, template.GroupLayer);
		}
		return (MapMember)(object)MapMemberLookupService.GetTableByName(template.SubtypeLayer, template.GroupLayer);
	}

	private static (TableDefinition Definition, Subtype Subtype) GetDefinitionAndSubtype(MapMember mapMember, SimpleTemplate template)
	{
		if (mapMember == null)
		{
			return (Definition: null, Subtype: null);
		}
		FeatureLayer val = (FeatureLayer)(object)((mapMember is FeatureLayer) ? mapMember : null);
		TableDefinition definition;
		if (val != null)
		{
			definition = (TableDefinition)(object)val.GetFeatureClass().GetDefinition();
		}
		else
		{
			StandaloneTable val2 = (StandaloneTable)(object)((mapMember is StandaloneTable) ? mapMember : null);
			if (val2 == null)
			{
				return (Definition: null, Subtype: null);
			}
			definition = val2.GetTable().GetDefinition();
		}
		string subtypeField = definition.GetSubtypeField();
		if (string.IsNullOrWhiteSpace(subtypeField))
		{
			return (Definition: definition, Subtype: null);
		}
		Dictionary<string, object> dictionary = template.DefaultFieldValues ?? new Dictionary<string, object>();
		string text = FindConfiguredFieldName(dictionary.Keys, subtypeField);
		if (text == null)
		{
			return (Definition: definition, Subtype: null);
		}
		string subtypeName = Convert.ToString(CommonFunctions.GetObjectValue(dictionary[text]));
		Subtype item = definition.GetSubtypes().FirstOrDefault((Subtype candidate) => string.Equals(candidate.GetName(), subtypeName, StringComparison.OrdinalIgnoreCase));
		return (Definition: definition, Subtype: item);
	}

	private static string FindConfiguredFieldName(IEnumerable<string> fieldNames, string expectedFieldName)
	{
		return (fieldNames ?? Enumerable.Empty<string>()).FirstOrDefault((string fieldName) => string.Equals(fieldName, expectedFieldName, StringComparison.OrdinalIgnoreCase));
	}

	private static string NormalizeFieldName(string fieldName)
	{
		return (fieldName ?? string.Empty).Trim().ToUpperInvariant();
	}

	private static string NormalizeTemplateKey(string templateKey)
	{
		return (templateKey ?? string.Empty).Trim();
	}

	private static string TryGetDomainName(Domain domain)
	{
		try
		{
			return (domain != null) ? domain.GetName() : null;
		}
		catch (Exception exception)
		{
			LogService.LogException("Could not resolve domain name while evaluating placement attribute overrides.", exception);
			return null;
		}
	}

	private static List<string> GetAvailableDomainValues(Field field, Subtype subtype)
	{
		Domain val = ((field != null) ? field.GetDomain(subtype) : null) ?? ((field != null) ? field.GetDomain((Subtype)null) : null);
		CodedValueDomain val2 = (CodedValueDomain)(object)((val is CodedValueDomain) ? val : null);
		if (val2 == null)
		{
			return new List<string>();
		}
		return (from value in val2.GetCodedValuePairs().Values
			select Convert.ToString(value) into value
			where !string.IsNullOrWhiteSpace(value)
			select value).Distinct<string>(StringComparer.OrdinalIgnoreCase).OrderBy<string, string>((string value) => value, StringComparer.OrdinalIgnoreCase).ToList();
	}

	private static string ConvertToEditorValue(object value)
	{
		return Convert.ToString(CommonFunctions.GetObjectValue(value)) ?? string.Empty;
	}

	private static Dictionary<string, Dictionary<string, string>> BuildFavouritePartValueMap(PlacementAttributeEditorModel editorModel)
	{
		Dictionary<string, Dictionary<string, string>> dictionary = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
		IEnumerable<PlacementAttributeEditorPartState> enumerable = editorModel?.Parts;
		foreach (PlacementAttributeEditorPartState item in enumerable ?? Enumerable.Empty<PlacementAttributeEditorPartState>())
		{
			Dictionary<string, string> dictionary2 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			IEnumerable<PlacementAttributeEditorFieldState> attributeFields = item.AttributeFields;
			foreach (PlacementAttributeEditorFieldState item2 in attributeFields ?? Enumerable.Empty<PlacementAttributeEditorFieldState>())
			{
				if (!string.IsNullOrWhiteSpace(item2.CurrentValue))
				{
					dictionary2[NormalizeFieldName(item2.FieldName)] = item2.CurrentValue.Trim();
				}
			}
			dictionary[item.PartKey ?? string.Empty] = dictionary2;
		}
		return dictionary;
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
				PlacementAttributeOverrideFavouriteCatalog placementAttributeOverrideFavouriteCatalog = JsonSerializer.Deserialize<PlacementAttributeOverrideFavouriteCatalog>(File.ReadAllText(FavouriteFilePath), _jsonOptions);
				if (placementAttributeOverrideFavouriteCatalog == null)
				{
					placementAttributeOverrideFavouriteCatalog = new PlacementAttributeOverrideFavouriteCatalog();
				}
				PlacementAttributeOverrideFavouriteCatalog placementAttributeOverrideFavouriteCatalog2 = placementAttributeOverrideFavouriteCatalog;
				if (placementAttributeOverrideFavouriteCatalog2.Favourites == null)
				{
					List<PlacementAttributeOverrideFavourite> list = (placementAttributeOverrideFavouriteCatalog2.Favourites = new List<PlacementAttributeOverrideFavourite>());
				}
				placementAttributeOverrideFavouriteCatalog.Favourites = placementAttributeOverrideFavouriteCatalog.Favourites.Where((PlacementAttributeOverrideFavourite favourite) => !string.IsNullOrWhiteSpace(favourite?.Id) && !string.IsNullOrWhiteSpace(favourite.TemplateKey)).ToList();
				return placementAttributeOverrideFavouriteCatalog;
			}
		}
		catch (Exception exception)
		{
			LogService.LogException("Placement override favourites could not be loaded.", exception);
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
				string contents = JsonSerializer.Serialize(catalog ?? new PlacementAttributeOverrideFavouriteCatalog(), _jsonOptions);
				AtomicFileService.WriteAllText(FavouriteFilePath, contents);
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
		Dictionary<string, Dictionary<string, object>> dictionary = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
		IEnumerable<PlacementAttributeEditorPartState> enumerable = editorModel?.Parts;
		foreach (PlacementAttributeEditorPartState item in enumerable ?? Enumerable.Empty<PlacementAttributeEditorPartState>())
		{
			Dictionary<string, object> dictionary2 = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
			IEnumerable<PlacementAttributeEditorFieldState> attributeFields = item.AttributeFields;
			foreach (PlacementAttributeEditorFieldState item2 in attributeFields ?? Enumerable.Empty<PlacementAttributeEditorFieldState>())
			{
				dictionary2[item2.FieldName] = (string.IsNullOrWhiteSpace(item2.CurrentValue) ? null : item2.CurrentValue.Trim());
			}
			if (dictionary2.Count > 0 && !string.IsNullOrWhiteSpace(item.PartKey))
			{
				dictionary[item.PartKey] = dictionary2;
			}
		}
		return dictionary;
	}

	private static void ApplyPendingPlacementValues(Dictionary<string, object> effectiveValues, string placementPartKey)
	{
		if (effectiveValues == null || string.IsNullOrWhiteSpace(placementPartKey))
		{
			return;
		}
		Dictionary<string, object> value = null;
		lock (_syncRoot)
		{
			if (!_pendingPlacementValuesByPart.TryGetValue(placementPartKey, out value))
			{
				return;
			}
			value = new Dictionary<string, object>(value, StringComparer.OrdinalIgnoreCase);
		}
		foreach (var (key, value2) in value)
		{
			effectiveValues[key] = value2;
		}
	}

	private static string BuildFieldDomainSummaryCacheKey(SimpleTemplate template, string fieldName, string expectedDomainName)
	{
		if (template == null || string.IsNullOrWhiteSpace(fieldName))
		{
			return null;
		}
		return string.Join("|", template.GroupLayer ?? string.Empty, template.SubtypeLayer ?? string.Empty, NormalizeFieldName(fieldName), (expectedDomainName ?? string.Empty).Trim());
	}

	private static FieldDomainSummary CacheFieldDomainSummary(Dictionary<string, FieldDomainSummary> cache, string cacheKey, FieldDomainSummary summary)
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
		List<string> list = (warnings ?? Enumerable.Empty<string>()).Where((string warning) => !string.IsNullOrWhiteSpace(warning)).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		return "Override notes:\n" + string.Join("\n", list.Select((string warning) => "- " + warning));
	}
}
