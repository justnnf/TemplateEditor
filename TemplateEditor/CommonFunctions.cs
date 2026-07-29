using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.UtilityNetwork;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using DataDomain = ArcGIS.Core.Data.Domain;
using DataSubtype = ArcGIS.Core.Data.Subtype;

namespace TemplateEditor;

/// <summary>
/// Cache for template lookups (SimpleTemplate and GroupTemplate) to enable O(1) lookups instead of O(n) scans.
/// Uses case-insensitive key comparison to match the StringComparison.OrdinalIgnoreCase used throughout the codebase.
/// Thread-safe with lazy initialization on first access.
/// </summary>
internal static class TemplateCache
{
	private static Dictionary<string, SimpleTemplate> _simpleTemplatesByName;
	private static Dictionary<string, GroupTemplate> _groupTemplatesByName;
	private static readonly object LockObject = new();
	private static bool _isInitialized = false;

	/// <summary>
	/// Initializes the template cache from the current TemplateConfig.
	/// Call this when template configuration is loaded or reloaded.
	/// </summary>
	public static void Initialize(TemplateConfig config)
	{
		lock (LockObject)
		{
			_simpleTemplatesByName = config?.SimpleTemplates?
				.GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
				.ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase)
				?? new(StringComparer.OrdinalIgnoreCase);

			_groupTemplatesByName = config?.GroupTemplates?
				.GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
				.ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase)
				?? new(StringComparer.OrdinalIgnoreCase);

			_isInitialized = true;
		}
	}

	/// <summary>
	/// Gets a SimpleTemplate by name using case-insensitive lookup.
	/// Returns null if not found or cache is not initialized.
	/// </summary>
	public static SimpleTemplate GetSimpleTemplate(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return null;
		}

		// Lazy initialization on first access if not already done
		if (!_isInitialized)
		{
			lock (LockObject)
			{
				if (!_isInitialized)
				{
					Initialize(AddinConfiguration.Templates);
				}
			}
		}

		lock (LockObject)
		{
			return _simpleTemplatesByName?.TryGetValue(name, out var template) == true
				? template
				: null;
		}
	}

	/// <summary>
	/// Gets a GroupTemplate by name using case-insensitive lookup.
	/// Returns null if not found or cache is not initialized.
	/// </summary>
	public static GroupTemplate GetGroupTemplate(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return null;
		}

		// Lazy initialization on first access if not already done
		if (!_isInitialized)
		{
			lock (LockObject)
			{
				if (!_isInitialized)
				{
					Initialize(AddinConfiguration.Templates);
				}
			}
		}

		lock (LockObject)
		{
			return _groupTemplatesByName?.TryGetValue(name, out var template) == true
				? template
				: null;
		}
	}

	/// <summary>
	/// Clears the cache. Call this if template configuration is reloaded during the session.
	/// </summary>
	public static void Clear()
	{
		lock (LockObject)
		{
			_simpleTemplatesByName = null;
			_groupTemplatesByName = null;
			_isInitialized = false;
		}
	}
}

internal static class CommonFunctions
{
	private static TemplateConfig GetLoadedTemplateConfigOrThrow()
	{
		TemplateConfig templates = AddinConfiguration.Templates;
		if (templates == null)
		{
			throw new InvalidOperationException("Template configuration is not loaded.");
		}
		templates.SimpleTemplates ??= new List<SimpleTemplate>();
		templates.GroupTemplates ??= new List<GroupTemplate>();
		return templates;
	}

	public static object GetObjectValue(object obj)
	{
		if (obj == null)
		{
			return null;
		}
		if (obj is not JsonElement jsonElement)
		{
			return obj;
		}
		return jsonElement.ValueKind switch
		{
			JsonValueKind.Number => jsonElement.TryGetInt64(out long longValue) ? longValue : jsonElement.GetDouble(),
			JsonValueKind.True => true,
			JsonValueKind.False => false,
			JsonValueKind.Null => null,
			_ => jsonElement.ToString()
		};
	}

	public static async Task<GeometryType> GetTemplateGeometryTypeAsync(DisplayTemplate template)
	{
		TemplateConfig templates = GetLoadedTemplateConfigOrThrow();
		string templateName = template?.Name ?? AddinConfiguration.SelectedTemplate?.Name;
		if (string.IsNullOrWhiteSpace(templateName))
		{
			return (GeometryType)GeometryTypeHelper.TableGeometryType;
		}
		if (template?.IsGroupChild == true)
		{
			SimpleTemplateReference childTemplateRef = GetGroupChildReference(template);
			SimpleTemplate childTemplate = templates.SimpleTemplates.FirstOrDefault((SimpleTemplate n) =>
				string.Equals(n.Name, childTemplateRef?.Name, StringComparison.OrdinalIgnoreCase));
			if (childTemplateRef == null || childTemplate == null)
			{
				throw new InvalidOperationException($"Template part '{templateName}' was not found.");
			}
			GeometryType configuredChildSketchType = GetConfiguredSketchGeometryType(childTemplateRef);
			if (childTemplateRef.SketchType != null)
			{
				return configuredChildSketchType;
			}
			if (HasConfiguredPlacementGeometry(childTemplateRef))
			{
				return (GeometryType)GeometryTypeHelper.PointGeometryType;
			}
			return await GetSimpleTemplateGeometryTypeAsync(childTemplate);
		}
		SimpleTemplate simpleTemplate = templates.SimpleTemplates.FirstOrDefault((SimpleTemplate n) =>
			string.Equals(n.Name, templateName, StringComparison.OrdinalIgnoreCase));
		if (simpleTemplate != null)
		{
			return await GetSimpleTemplateGeometryTypeAsync(simpleTemplate);
		}
		GroupTemplate groupTemplate = templates.GroupTemplates.FirstOrDefault((GroupTemplate n) =>
			string.Equals(n.Name, templateName, StringComparison.OrdinalIgnoreCase));
		SimpleTemplateReference simpleTemplateRef = groupTemplate?.SimpleTemplates?.FirstOrDefault((SimpleTemplateReference n) => n.FeatureId == 1);
		if (simpleTemplateRef == null)
		{
			throw new InvalidOperationException($"Template '{templateName}' does not have a sketch feature.");
		}
		GeometryType configuredSketchType = GetConfiguredSketchGeometryType(simpleTemplateRef);
		if (simpleTemplateRef.SketchType != null)
		{
			return configuredSketchType;
		}
		if (HasConfiguredPlacementGeometry(groupTemplate))
		{
			return (GeometryType)GeometryTypeHelper.PointGeometryType;
		}
		SimpleTemplate referencedTemplate = templates.SimpleTemplates.FirstOrDefault((SimpleTemplate n) =>
			string.Equals(n.Name, simpleTemplateRef.Name, StringComparison.OrdinalIgnoreCase));
		return referencedTemplate == null ? configuredSketchType : await GetSimpleTemplateGeometryTypeAsync(referencedTemplate);
	}

	private static GeometryType GetConfiguredSketchGeometryType(SimpleTemplateReference simpleTemplateRef)
	{
		return (GeometryType)(simpleTemplateRef?.SketchType?.ToUpperInvariant() switch
		{
			"LINE" => GeometryTypeHelper.PolylineGeometryType,
			"POLYGON" => GeometryTypeHelper.PolygonGeometryType,
			_ => GeometryTypeHelper.PointGeometryType
		});
	}

	private static SimpleTemplateReference GetGroupChildReference(DisplayTemplate childTemplate)
	{
		if (childTemplate == null || !childTemplate.IsGroupChild)
		{
			return null;
		}
		GroupTemplate groupTemplate = AddinConfiguration.Templates?.GroupTemplates?.FirstOrDefault((GroupTemplate group) =>
			string.Equals(group.Name, childTemplate.ParentTemplateName, StringComparison.OrdinalIgnoreCase));
		return groupTemplate?.SimpleTemplates?.FirstOrDefault((SimpleTemplateReference templateRef) =>
			templateRef.FeatureId == childTemplate.FeatureId &&
			string.Equals(templateRef.Name, childTemplate.Name, StringComparison.OrdinalIgnoreCase));
	}

	private static bool HasConfiguredPlacementGeometry(GroupTemplate groupTemplate)
	{
		return groupTemplate?.SimpleTemplates?.Any((SimpleTemplateReference n) => n.Location != null || n.Line != null || n.Polygon != null) == true;
	}

	private static bool HasConfiguredPlacementGeometry(SimpleTemplateReference templateRef)
	{
		return templateRef?.Location != null || templateRef?.Line != null || templateRef?.Polygon != null;
	}

	private static async Task<GeometryType> GetSimpleTemplateGeometryTypeAsync(SimpleTemplate simpleTemplate)
	{
		if (simpleTemplate == null || !IsFeatureLayerTemplate(simpleTemplate))
		{
			return (GeometryType)GeometryTypeHelper.TableGeometryType;
		}
		FeatureLayer layer = MapMemberLookupService.GetFeatureLayerByName(simpleTemplate.SubtypeLayer, simpleTemplate.GroupLayer);
		if (layer == null)
		{
			throw new InvalidOperationException($"Layer '{simpleTemplate.GroupLayer}/{simpleTemplate.SubtypeLayer}' was not found for template '{simpleTemplate.Name}'.");
		}
		return await QueuedTask.Run(() => layer.GetFeatureClass().GetDefinition().GetShapeType());
	}

	public static async Task<bool> CreateFeatures(Geometry sketchGeometry, double rotationDegrees = 0.0)
	{
		if (sketchGeometry == null)
		{
			EditorDockpaneViewModel.SetPlacementStatus(EditorDockpaneViewModel.ReadyPlacementStatus);
			DialogService.Show("A placement geometry is required before placing features.", "Template Editor");
			return false;
		}
		TemplateConfig templates = GetLoadedTemplateConfigOrThrow();
		DisplayTemplate selectedTemplate = AddinConfiguration.SelectedTemplate;
		string templateName = selectedTemplate?.Name;
		if (string.IsNullOrWhiteSpace(templateName))
		{
			EditorDockpaneViewModel.SetPlacementStatus(EditorDockpaneViewModel.ReadyPlacementStatus);
			DialogService.Show("Choose a template before placing features.", "Template Editor");
			return false;
		}
		PlacementAttributeOverrideService.BeginPlacement();
		DialogService.BeginPlacementProgress("Template Editor", "Preparing " + selectedTemplate.DisplayName + " for placement...");
		try
		{
			EditorDockpaneViewModel.SetPlacementStatus("Preparing " + selectedTemplate.DisplayName + ": checking the target version and placement options...");
			string defaultVersionMessage = await GetDefaultVersionPlacementBlockMessageAsync(selectedTemplate);
			if (defaultVersionMessage != null)
			{
				EditorDockpaneViewModel.SetPlacementStatus("Blocked: switch from DEFAULT to a named version before placing.");
				DialogService.Show(defaultVersionMessage, "Template Editor");
				return false;
			}
			if (selectedTemplate?.IsGroupChild == true)
			{
				return await CreateGroupChildFeature(selectedTemplate, sketchGeometry, rotationDegrees);
			}
			bool isSimpleTemplate = templates.SimpleTemplates.Any((SimpleTemplate n) => string.Equals(n.Name, templateName, StringComparison.OrdinalIgnoreCase));
			if (isSimpleTemplate)
			{
				SimpleTemplate simpleTemplate = templates.SimpleTemplates.FirstOrDefault((SimpleTemplate n) => string.Equals(n.Name, templateName, StringComparison.OrdinalIgnoreCase));
				if (!IsStructureJunctionObjectTemplate(simpleTemplate) && !IsFeatureLayerTemplate(simpleTemplate) && !ConfirmCreateNonSpatialTemplate(simpleTemplate))
				{
					return false;
				}
			}
			PlacementBuildResult placement = await BuildPlacementOperationAsync(templateName, sketchGeometry, isSimpleTemplate, PlacementOptions.Full, rotationDegrees);
			EditorDockpaneViewModel.SetPlacementStatus("Creating " + selectedTemplate.DisplayName + ": adding features, rows, and configured attributes...");
			string errorMessage = await ExecutePlacementOperationAsync(placement.Operation);
			if (errorMessage == null)
			{
				await PopulateFeatureInfoDetailsAsync(placement.FeatureInfos);
				ConfiguredAssociationResult associationResult = ConfiguredAssociationResult.Empty;
				if (placement.ApplyConfiguredAssociations)
				{
					EditorDockpaneViewModel.SetPlacementStatus("Associating " + selectedTemplate.DisplayName + ": creating " + placement.ConfiguredAssociations.Count + " configured association(s)...");
					associationResult = await ExecuteConfiguredAssociationsAsync(placement);
				}
				EditorDockpaneViewModel.SetPlacementStatus("Finishing " + selectedTemplate.DisplayName + ": checking split and containment options...");
				await FinalizePlacementAsync(placement.CreatedFeatures, applyPostPlacementEnhancements: true, associationResult.CreatedPairs);
				EditorDockpaneViewModel.PostPlacementSummary(
					BuildPlacementSummary(placement, associationResult),
					AppendPlacementWarnings(BuildPlacementSummaryDetails(associationResult)),
					associationResult.HasFailures || PlacementAttributeOverrideService.HasPlacementWarnings());
				return true;
			}
			EditorDockpaneViewModel.SetPlacementStatus("Placement failed. Choose a fallback option or cancel.");
			return await TryPlaceWithFallbacksAsync(templateName, sketchGeometry, isSimpleTemplate, errorMessage, rotationDegrees);
		}
		catch (OperationCanceledException)
		{
			EditorDockpaneViewModel.SetPlacementStatus(EditorDockpaneViewModel.ReadyPlacementStatus);
			return false;
		}
		catch (Exception ex)
		{
			EditorDockpaneViewModel.SetPlacementStatus("Placement failed. See message for details.");
			LogService.LogException("CreateFeatures failed.", ex);
			DialogService.Show("Template placement failed.\n\n" + ex.Message + "\n\nDetails were written to the Template Editor log.", "Template Editor");
			return false;
		}
		finally
		{
			DialogService.EndPlacementProgress();
			PlacementAttributeOverrideService.EndPlacementAttempt();
			EditorDockpaneViewModel.RefreshSettingsStatus();
		}
	}

	private static async Task<string> GetDefaultVersionPlacementBlockMessageAsync(DisplayTemplate selectedTemplate)
	{
		if (AddinConfiguration.Settings?.PreventDefaultVersionPlacement != true || selectedTemplate == null)
		{
			return null;
		}
		List<string> defaultVersionTargets = await QueuedTask.Run(() => GetDefaultVersionPlacementTargets(selectedTemplate));
		if (defaultVersionTargets.Count == 0)
		{
			return null;
		}
		string targets = string.Join("\n", defaultVersionTargets.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy((string target) => target, StringComparer.OrdinalIgnoreCase).Select((string target) => "- " + target));
		return "Template placement was blocked because one or more target feature service layers or tables are connected to DEFAULT.\n\nSwitch the map to a named version before placing templates.\n\nTarget(s):\n" + targets;
	}

	private static List<string> GetDefaultVersionPlacementTargets(DisplayTemplate selectedTemplate)
	{
		return GetPlacementTargetMapMembers(selectedTemplate)
			.Where((MapMember mapMember) => IsDefaultVersionConnection(mapMember))
			.Select((MapMember mapMember) => mapMember.Name)
			.Where((string name) => !string.IsNullOrWhiteSpace(name))
			.ToList();
	}

	private static IEnumerable<MapMember> GetPlacementTargetMapMembers(DisplayTemplate selectedTemplate)
	{
		foreach (SimpleTemplate template in GetPlacementTargetTemplates(selectedTemplate))
		{
			MapMember mapMember = GetMapMemberForTemplate(template);
			if (mapMember != null)
			{
				yield return mapMember;
			}
		}
	}

	private static IEnumerable<SimpleTemplate> GetPlacementTargetTemplates(DisplayTemplate selectedTemplate)
	{
		if (selectedTemplate?.IsGroupChild == true)
		{
			SimpleTemplateReference childTemplateRef = GetGroupChildReference(selectedTemplate);
			// ✅ Use cache for O(1) lookup instead of O(n) FirstOrDefault
			SimpleTemplate childTemplate = TemplateCache.GetSimpleTemplate(childTemplateRef?.Name);
			if (childTemplate != null)
			{
				yield return childTemplate;
			}
			yield break;
		}
		// ✅ Use cache for O(1) lookup instead of O(n) FirstOrDefault
		SimpleTemplate simpleTemplate = TemplateCache.GetSimpleTemplate(selectedTemplate?.Name);
		if (simpleTemplate != null)
		{
			yield return simpleTemplate;
			yield break;
		}
		// ✅ Use cache for O(1) lookup instead of O(n) FirstOrDefault
		GroupTemplate groupTemplate = TemplateCache.GetGroupTemplate(selectedTemplate?.Name);
		foreach (SimpleTemplateReference templateRef in groupTemplate?.SimpleTemplates ?? Enumerable.Empty<SimpleTemplateReference>())
		{
			// ✅ Use cache for O(1) lookup instead of O(n) FirstOrDefault
			SimpleTemplate template = TemplateCache.GetSimpleTemplate(templateRef.Name);
			if (template != null)
			{
				yield return template;
			}
		}
	}

	private static bool IsDefaultVersionConnection(MapMember mapMember)
	{
		if (mapMember == null)
		{
			return false;
		}
		try
		{
			return GetConnectionVersionNames(mapMember.GetDataConnection()).Any(IsDefaultVersionName);
		}
		catch (Exception ex)
		{
			LogService.LogException($"Could not inspect connection version information for map member '{mapMember.Name}'.", ex);
			return false;
		}
	}

	private static IEnumerable<string> GetConnectionVersionNames(object value)
	{
		return GetConnectionVersionNames(value, new HashSet<object>(ReferenceEqualityComparer.Instance));
	}

	private static IEnumerable<string> GetConnectionVersionNames(object value, HashSet<object> visited)
	{
		if (value == null || value is string || !visited.Add(value))
		{
			yield break;
		}
		Type type = value.GetType();
		foreach (System.Reflection.PropertyInfo property in type.GetProperties())
		{
			if (!property.CanRead || property.GetIndexParameters().Length > 0)
			{
				continue;
			}
			object propertyValue;
			try
			{
				propertyValue = property.GetValue(value);
			}
			catch (Exception ex)
			{
				LogService.LogException($"Could not inspect connection property '{property.Name}' on type '{type.FullName}'.", ex);
				continue;
			}
			if (propertyValue is string text)
			{
				if (string.Equals(property.Name, "GdbVersion", StringComparison.OrdinalIgnoreCase))
				{
					yield return text;
				}
				if (string.Equals(property.Name, "WorkspaceConnectionString", StringComparison.OrdinalIgnoreCase))
				{
					string version = GetVersionFromConnectionString(text);
					if (!string.IsNullOrWhiteSpace(version))
					{
						yield return version;
					}
				}
				continue;
			}
			if (propertyValue is System.Collections.IEnumerable enumerable && propertyValue is not string)
			{
				foreach (object item in enumerable)
				{
					foreach (string version in GetConnectionVersionNames(item, visited))
					{
						yield return version;
					}
				}
				continue;
			}
			if (propertyValue != null && propertyValue.GetType().Namespace?.StartsWith("ArcGIS.Core.CIM", StringComparison.Ordinal) == true)
			{
				foreach (string version in GetConnectionVersionNames(propertyValue, visited))
				{
					yield return version;
				}
			}
		}
	}

	private static string GetVersionFromConnectionString(string connectionString)
	{
		if (string.IsNullOrWhiteSpace(connectionString))
		{
			return null;
		}
		foreach (string part in connectionString.Split(';'))
		{
			string[] pieces = part.Split(new[] { '=' }, 2);
			if (pieces.Length == 2 && string.Equals(pieces[0].Trim(), "VERSION", StringComparison.OrdinalIgnoreCase))
			{
				return pieces[1].Trim();
			}
		}
		return null;
	}

	private static bool IsDefaultVersionName(string versionName)
	{
		if (string.IsNullOrWhiteSpace(versionName))
		{
			return false;
		}
		string normalized = versionName.Trim();
		return string.Equals(normalized, "DEFAULT", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(normalized, "SDE.DEFAULT", StringComparison.OrdinalIgnoreCase) ||
			normalized.EndsWith(".DEFAULT", StringComparison.OrdinalIgnoreCase);
	}

	private static async Task<bool> CreateGroupChildFeature(DisplayTemplate childTemplate, Geometry sketchGeometry, double rotationDegrees)
	{
		TemplateConfig templates = GetLoadedTemplateConfigOrThrow();
		SimpleTemplateReference childTemplateRef = GetGroupChildReference(childTemplate);
		// ✅ Use cache for O(1) lookup instead of O(n) FirstOrDefault
		SimpleTemplate template = TemplateCache.GetSimpleTemplate(childTemplateRef?.Name);
		if (childTemplateRef == null || template == null)
		{
			throw new InvalidOperationException($"Template part '{childTemplate?.DisplayName}' was not found.");
		}
		EditorDockpaneViewModel.SetPlacementStatus("Creating " + childTemplate.DisplayName + ": adding the selected template part...");
		EditOperation operation = new EditOperation
		{
			Name = "Create template part",
			ProgressMessage = "Creating template part...",
			ShowProgressor = true,
			SelectNewFeatures = true
		};
		List<PlacedFeatureContext> createdFeatures = new List<PlacedFeatureContext>();
		Geometry featureGeometry = CreateGeometryForSingleGroupPart(childTemplateRef, sketchGeometry, rotationDegrees);
		if (IsStructureJunctionObjectTemplate(template))
		{
			await CreateSJOAttachmentsForPoles(template, featureGeometry, operation, PlacementOptions.Full);
		}
		else if (IsFeatureLayerTemplate(template))
		{
			RowToken token = await CreateFeatureOrRowFromSimpleTemplate(template, featureGeometry, operation, includeDefaultAttributes: true, rotationDegrees, childTemplate.ParentTemplateName, childTemplate.FeatureId);
			TryTrackPlacedFeature(createdFeatures, template, featureGeometry, token, IsPlacementEnhancementCandidate(childTemplateRef));
		}
		else
		{
			await CreateTableRowWithAutoAssociationAsync(template, featureGeometry, operation, PlacementOptions.Full, childTemplate.ParentTemplateName, childTemplate.FeatureId);
		}
		string errorMessage = await ExecutePlacementOperationAsync(operation);
		if (errorMessage != null)
		{
			EditorDockpaneViewModel.SetPlacementStatus("Template part placement failed. See message for details.");
			DialogService.Show("Template part placement failed.\n\n" + CleanErrorMessage(errorMessage), "Template Editor");
			return false;
		}
		EditorDockpaneViewModel.SetPlacementStatus("Finishing " + childTemplate.DisplayName + ": checking split and containment options...");
		await FinalizePlacementAsync(createdFeatures, applyPostPlacementEnhancements: true);
		EditorDockpaneViewModel.PostPlacementSummary(
			$"Created template part '{childTemplate.DisplayName}'.",
			AppendPlacementWarnings(null),
			warning: PlacementAttributeOverrideService.HasPlacementWarnings());
		return true;
	}

	private static async Task<bool> TryPlaceWithFallbacksAsync(string templateName, Geometry sketchGeometry, bool isSimpleTemplate, string originalErrorMessage, double rotationDegrees)
	{
		if (DialogService.Show(
			"Template placement failed.\n\n" + CleanErrorMessage(originalErrorMessage) + "\n\nYou can retry without configured associations, or cancel placement.",
			"Template Editor",
			new DialogButtonChoice("Place Without Associations", MessageBoxResult.Yes, isPrimary: true),
			new DialogButtonChoice("Cancel", MessageBoxResult.No, isCancel: true)) == MessageBoxResult.Yes)
		{
			EditorDockpaneViewModel.SetPlacementStatus("Retrying " + templateName + ": placing without configured associations...");
			PlacementBuildResult placementWithoutAssociations = await BuildPlacementOperationAsync(templateName, sketchGeometry, isSimpleTemplate, PlacementOptions.WithoutAssociations, rotationDegrees);
			string associationFallbackError = await ExecutePlacementOperationAsync(placementWithoutAssociations.Operation);
			if (associationFallbackError == null)
			{
				await PopulateFeatureInfoDetailsAsync(placementWithoutAssociations.FeatureInfos);
				await FinalizePlacementAsync(placementWithoutAssociations.CreatedFeatures, applyPostPlacementEnhancements: false);
				EditorDockpaneViewModel.PostPlacementSummary(
					BuildPlacementSummary(placementWithoutAssociations),
					AppendPlacementWarnings("Template was placed without configured associations."),
					warning: true);
				return true;
			}
			originalErrorMessage = associationFallbackError;
		}
		if (DialogService.Show(
			"Template placement still failed.\n\n" + CleanErrorMessage(originalErrorMessage) + "\n\nYou can retry with only subtype and required attributes, or cancel placement.",
			"Template Editor",
			new DialogButtonChoice("Place Required Only", MessageBoxResult.Yes, isPrimary: true),
			new DialogButtonChoice("Cancel", MessageBoxResult.No, isCancel: true)) == MessageBoxResult.Yes)
		{
			EditorDockpaneViewModel.SetPlacementStatus("Retrying " + templateName + ": placing with only subtype and required attributes...");
			PlacementBuildResult minimalPlacement = await BuildPlacementOperationAsync(templateName, sketchGeometry, isSimpleTemplate, PlacementOptions.MinimalAttributes, rotationDegrees);
			string minimalError = await ExecutePlacementOperationAsync(minimalPlacement.Operation);
			if (minimalError == null)
			{
				await PopulateFeatureInfoDetailsAsync(minimalPlacement.FeatureInfos);
				await FinalizePlacementAsync(minimalPlacement.CreatedFeatures, applyPostPlacementEnhancements: false);
				EditorDockpaneViewModel.PostPlacementSummary(
					BuildPlacementSummary(minimalPlacement),
					AppendPlacementWarnings("Template was placed with only subtype/required attributes and without configured associations."),
					warning: true);
				return true;
			}
			originalErrorMessage = minimalError;
		}
		EditorDockpaneViewModel.SetPlacementStatus("Placement failed. See message for details.");
		DialogService.Show("Template placement failed.\n\n" + CleanErrorMessage(originalErrorMessage), "Template Editor");
		return false;
	}

	private static string BuildPlacementSummary(PlacementBuildResult placement, ConfiguredAssociationResult associationResult = null)
	{
		int featureCount = placement?.CreatedFeatures?.Count ?? 0;
		int featureInfoCount = placement?.FeatureInfos?.Count ?? 0;
		int associationCount = associationResult?.CreatedCount ?? (placement?.ConfiguredAssociations?.Count ?? 0);
		List<string> parts = new List<string>();
		if (featureCount > 0)
		{
			parts.Add($"{featureCount} feature(s)");
		}
		if (featureInfoCount > featureCount)
		{
			parts.Add($"{featureInfoCount - featureCount} non-spatial row(s)");
		}
		if (associationCount > 0 && placement.ApplyConfiguredAssociations)
		{
			parts.Add($"{associationCount} configured association(s)");
		}
		return parts.Count == 0 ? "Placement completed." : "Created " + string.Join(", ", parts) + ".";
	}

	private static string BuildPlacementSummaryDetails(ConfiguredAssociationResult associationResult)
	{
		if (associationResult?.HasFailures != true)
		{
			return null;
		}
		return $"{associationResult.FailedCount} configured association(s) could not be created. Review the diagnostics and verify the placed template before continuing.";
	}

	private static string AppendPlacementWarnings(string details)
	{
		string warnings = PlacementAttributeOverrideService.ConsumePlacementWarnings();
		if (string.IsNullOrWhiteSpace(warnings))
		{
			return details;
		}
		return string.IsNullOrWhiteSpace(details) ? warnings : details + "\n" + warnings;
	}

	private static async Task<PlacementBuildResult> BuildPlacementOperationAsync(string templateName, Geometry sketchGeometry, bool isSimpleTemplate, PlacementOptions options, double rotationDegrees)
	{
		TemplateConfig templates = GetLoadedTemplateConfigOrThrow();
		EditOperation operation = new EditOperation
		{
			Name = options.OperationName,
			ProgressMessage = "Placing template features...",
			ShowProgressor = true,
			SelectNewFeatures = true
		};
		List<PlacedFeatureContext> createdFeatures = new List<PlacedFeatureContext>();
		List<FeatureInfo> featureInfos = new List<FeatureInfo>();
		List<AssociationObject> configuredAssociations = new List<AssociationObject>();
		if (isSimpleTemplate)
		{
			SimpleTemplate template = templates.SimpleTemplates.FirstOrDefault((SimpleTemplate n) => string.Equals(n.Name, templateName, StringComparison.OrdinalIgnoreCase));
			if (template == null)
			{
				throw new InvalidOperationException($"Simple template '{templateName}' was not found.");
			}
			if (IsStructureJunctionObjectTemplate(template))
			{
				await CreateSJOAttachmentsForPoles(template, sketchGeometry, operation, options);
			}
			else if (IsFeatureLayerTemplate(template))
			{
				RowToken token = await CreateFeatureOrRowFromSimpleTemplate(template, sketchGeometry, operation, options.IncludeDefaultAttributes, rotationDegrees);
				TryTrackPlacedFeature(createdFeatures, template, sketchGeometry, token);
			}
			else
			{
				await CreateTableRowWithAutoAssociationAsync(template, sketchGeometry, operation, options);
			}
		}
		else
		{
			await BuildGroupPlacementOperationAsync(templateName, sketchGeometry, operation, createdFeatures, featureInfos, configuredAssociations, options, rotationDegrees);
		}
		return new PlacementBuildResult
		{
			Operation = operation,
			CreatedFeatures = createdFeatures,
			FeatureInfos = featureInfos,
			ConfiguredAssociations = configuredAssociations,
			ApplyConfiguredAssociations = !isSimpleTemplate && options.IncludeConfiguredAssociations
		};
	}

	private static async Task BuildGroupPlacementOperationAsync(string templateName, Geometry sketchGeometry, EditOperation operation, List<PlacedFeatureContext> createdFeatures, List<FeatureInfo> featureTokens, List<AssociationObject> configuredAssociations, PlacementOptions options, double rotationDegrees)
	{
		TemplateConfig templates = GetLoadedTemplateConfigOrThrow();
		GroupTemplate groupTemplate = templates.GroupTemplates.FirstOrDefault((GroupTemplate n) => string.Equals(n.Name, templateName, StringComparison.OrdinalIgnoreCase));
		if (groupTemplate == null)
		{
			throw new InvalidOperationException($"Group template '{templateName}' was not found.");
		}
		foreach (SimpleTemplateReference simpleTemplateRef in groupTemplate.SimpleTemplates)
		{
			SimpleTemplate template = AddinConfiguration.Templates.SimpleTemplates.FirstOrDefault((SimpleTemplate n) => string.Equals(n.Name, simpleTemplateRef.Name, StringComparison.OrdinalIgnoreCase));
			if (template == null)
			{
				throw new InvalidOperationException($"Group template '{groupTemplate.Name}' references missing simple template '{simpleTemplateRef.Name}'.");
			}
			Geometry featureGeometry = CreateGeometryForTemplate(simpleTemplateRef, sketchGeometry, rotationDegrees);
			RowToken token = await CreateFeatureOrRowFromSimpleTemplate(template, featureGeometry, operation, options.IncludeDefaultAttributes, rotationDegrees, groupTemplate.Name, simpleTemplateRef.FeatureId);
			TryTrackPlacedFeature(createdFeatures, template, featureGeometry, token, IsPlacementEnhancementCandidate(simpleTemplateRef));
			featureTokens.Add(new FeatureInfo
			{
				FeatureId = simpleTemplateRef.FeatureId,
				Token = token,
				Geometry = featureGeometry,
				Template = template,
				IsSpatialFeature = IsFeatureLayerTemplate(template)
			});
		}
		if (options.IncludeConfiguredAssociations)
		{
			configuredAssociations.AddRange(groupTemplate.Associations ?? new List<AssociationObject>());
		}
		return;
	}

	private static async Task<string> ExecutePlacementOperationAsync(EditOperation operation)
	{
		bool editSucceeded = await QueuedTask.Run(delegate
		{
			return operation.Execute();
		});
		return editSucceeded && operation.IsSucceeded ? null : operation.ErrorMessage;
	}

	private static async Task FinalizePlacementAsync(List<PlacedFeatureContext> createdFeatures, bool applyPostPlacementEnhancements, IReadOnlyList<ExistingAssociationPair> configuredAssociationPairs = null)
	{
		await PopulatePlacedFeatureDetails(createdFeatures);
		if (applyPostPlacementEnhancements)
		{
			await PlacementEnhancementService.ApplyPostPlacementEnhancementsAsync(createdFeatures, configuredAssociationPairs);
		}
	}

	private static bool TryBuildConfiguredAssociationPair(AssociationObject association, FeatureInfo fromInfo, FeatureInfo toInfo, out ExistingAssociationPair pair, out string failure)
	{
		pair = null;
		failure = null;
		// Optimize: FeatureInfo objects now passed in directly instead of searching through list
		if (fromInfo == null || toInfo == null)
		{
			failure = $"Feature {association.FromFeatureId} -> {association.ToFeatureId}: missing feature id.";
			return false;
		}
		AssociationType? associationType = GetAssociationType(association.Type);
		if (associationType == null)
		{
			failure = $"{FormatAssociationLabel(association, fromInfo, toInfo)}: Unsupported association type '{association.Type}'.";
			return false;
		}
		if (fromInfo.MapMember == null || toInfo.MapMember == null || fromInfo.ObjectID <= 0 || toInfo.ObjectID <= 0)
		{
			failure = $"{FormatAssociationLabel(association, fromInfo, toInfo)}: missing created feature or row identity.";
			return false;
		}
		pair = new ExistingAssociationPair
		{
			AssociationType = associationType.Value,
			FirstMember = fromInfo.MapMember,
			FirstObjectID = fromInfo.ObjectID,
			SecondMember = toInfo.MapMember,
			SecondObjectID = toInfo.ObjectID
		};
		return true;
	}

	private static AssociationType? GetAssociationType(string associationType)
	{
		if (string.IsNullOrWhiteSpace(associationType))
		{
			return null;
		}
		return associationType.ToUpperInvariant() switch
		{
			"CONTAINMENT" => AssociationType.Containment,
			"ATTACHMENT" => AssociationType.Attachment,
			"JUNCTIONJUNCTIONCONNECTIVITY" => UtilityNetworkAssociationTypes.JunctionJunctionConnectivity,
			"JUNCTIONEDGEOBJECTCONNECTIVITYFROMSIDE" => UtilityNetworkAssociationTypes.JunctionEdgeObjectFromSide,
			"JUNCTIONEDGEOBJECTCONNECTIVITYTOSIDE" => UtilityNetworkAssociationTypes.JunctionEdgeObjectToSide,
			"JUNCTIONEDGEOBJECTCONNECTIVITYMIDSPAN" => UtilityNetworkAssociationTypes.JunctionEdgeObjectMidspan,
			_ => null
		};
	}

	private static Task PopulateFeatureInfoDetailsAsync(List<FeatureInfo> featureInfos)
	{
		if (featureInfos == null || featureInfos.Count == 0)
		{
			return Task.CompletedTask;
		}
		foreach (FeatureInfo featureInfo in featureInfos)
		{
			featureInfo.MapMember = GetMapMemberForTemplate(featureInfo.Template);
			featureInfo.ObjectID = featureInfo.Token.ObjectID.GetValueOrDefault();
		}
		return Task.CompletedTask;
	}

	private static MapMember GetMapMemberForTemplate(SimpleTemplate template)
	{
		if (template == null)
		{
			return null;
		}
		if (IsFeatureLayerTemplate(template))
		{
			return MapMemberLookupService.GetFeatureLayerByName(template.SubtypeLayer, template.GroupLayer);
		}
		return MapMemberLookupService.GetTableByName(template.SubtypeLayer, template.GroupLayer);
	}

	private static async Task<ConfiguredAssociationResult> ExecuteConfiguredAssociationsAsync(PlacementBuildResult placement)
	{
		ConfiguredAssociationResult result = new ConfiguredAssociationResult
		{
			AttemptedCount = placement?.ConfiguredAssociations?.Count ?? 0
		};
		if (placement?.ConfiguredAssociations == null || placement.ConfiguredAssociations.Count == 0)
		{
			return result;
		}
		if (!string.Equals(AddinConfiguration.Settings?.ConfiguredAssociationPlacementMode, "Debug", StringComparison.OrdinalIgnoreCase))
		{
			await ExecuteConfiguredAssociationsFastAsync(placement, result);
			return result;
		}
		await ExecuteConfiguredAssociationsWithDiagnosticsAsync(placement, result);
		return result;
	}

	private static async Task ExecuteConfiguredAssociationsFastAsync(PlacementBuildResult placement, ConfiguredAssociationResult result)
	{
		List<ExistingAssociationPair> queuedPairs = new List<ExistingAssociationPair>();
		// Optimize: Build a dictionary for O(1) lookups instead of O(n) FirstOrDefault per association
		Dictionary<int, FeatureInfo> featureInfoById = new Dictionary<int, FeatureInfo>(placement.FeatureInfos.Count);
		foreach (FeatureInfo info in placement.FeatureInfos)
		{
			if (!featureInfoById.ContainsKey(info.FeatureId))
			{
				featureInfoById[info.FeatureId] = info;
			}
		}

		string errorMessage = await QueuedTask.Run(delegate
		{
			EditOperation operation = new EditOperation
			{
				Name = "Create template associations",
				ProgressMessage = "Creating template associations...",
				ShowProgressor = true
			};
			foreach (AssociationObject association in placement.ConfiguredAssociations)
			{
				// Optimize: Use dictionary lookup O(1) instead of FirstOrDefault O(n)
				featureInfoById.TryGetValue(association.FromFeatureId, out FeatureInfo fromInfo);
				featureInfoById.TryGetValue(association.ToFeatureId, out FeatureInfo toInfo);
				if (!TryBuildConfiguredAssociationPair(association, fromInfo, toInfo, out ExistingAssociationPair pair, out string failure))
				{
					result.Failures.Add(failure);
					continue;
				}
				AssociationDescription assocDesc = CreateAssociationDescription(association, fromInfo, toInfo);
				operation.Create(assocDesc);
				queuedPairs.Add(pair);
			}
			if (operation.IsEmpty)
			{
				return null;
			}
			return operation.Execute() && operation.IsSucceeded ? null : operation.ErrorMessage;
		});
		if (errorMessage != null)
		{
			result.Failures.Add("Batch association operation: " + CleanErrorMessage(errorMessage));
		}
		else
		{
			result.CreatedPairs.AddRange(queuedPairs);
		}
		if (result.Failures.Count > 0)
		{
			string displayedFailures = string.Join("\n", result.Failures.Take(8));
			string additionalFailureText = result.Failures.Count > 8 ? $"\n\n{result.Failures.Count - 8} more association failure(s) were not shown." : string.Empty;
			DialogService.Show(
				$"Template was placed, but one or more configured associations could not be created in Fast mode.\n\nSwitch Configured association mode to Debug for exact per-association diagnostics.\n\nIssue(s):\n{displayedFailures}{additionalFailureText}",
				"Template Editor - Association Diagnostics");
		}
	}

	private static async Task ExecuteConfiguredAssociationsWithDiagnosticsAsync(PlacementBuildResult placement, ConfiguredAssociationResult result)
	{
		// Optimize: Build a dictionary for O(1) lookups instead of O(n) FirstOrDefault per association
		Dictionary<int, FeatureInfo> featureInfoById = new Dictionary<int, FeatureInfo>(placement.FeatureInfos.Count);
		foreach (FeatureInfo info in placement.FeatureInfos)
		{
			if (!featureInfoById.ContainsKey(info.FeatureId))
			{
				featureInfoById[info.FeatureId] = info;
			}
		}

		foreach (AssociationObject association in placement.ConfiguredAssociations)
		{
			// Optimize: Use dictionary lookup O(1) instead of FirstOrDefault O(n)
			featureInfoById.TryGetValue(association.FromFeatureId, out FeatureInfo fromInfo);
			featureInfoById.TryGetValue(association.ToFeatureId, out FeatureInfo toInfo);
			if (!TryBuildConfiguredAssociationPair(association, fromInfo, toInfo, out ExistingAssociationPair pair, out string failure))
			{
				result.Failures.Add(failure);
				continue;
			}
			string errorMessage = await ExecuteSingleConfiguredAssociationAsync(association, fromInfo, toInfo);
			if (errorMessage != null)
			{
				result.Failures.Add($"{FormatAssociationLabel(association, fromInfo, toInfo)}: {CleanErrorMessage(errorMessage)}");
			}
			else
			{
				result.CreatedPairs.Add(pair);
			}
		}
		if (result.Failures.Count > 0)
		{
			string displayedFailures = string.Join("\n", result.Failures.Take(8));
			string additionalFailureText = result.Failures.Count > 8 ? $"\n\n{result.Failures.Count - 8} more association failure(s) were not shown." : string.Empty;
			DialogService.Show(
				$"Template was placed, but it is incomplete.\n\n{result.Failures.Count} configured association(s) could not be created. Inspect the newly placed features and verify their associations before continuing.\n\nFailed association(s):\n{displayedFailures}{additionalFailureText}",
				"Template Editor - Association Diagnostics");
		}
	}

	private static async Task<string> ExecuteSingleConfiguredAssociationAsync(AssociationObject association, FeatureInfo fromInfo, FeatureInfo toInfo)
	{
		return await QueuedTask.Run(delegate
		{
			AssociationDescription assocDesc = CreateAssociationDescription(association, fromInfo, toInfo);
			if (assocDesc == null)
			{
				return $"Unsupported association type '{association.Type}'.";
			}
			EditOperation operation = new EditOperation
			{
				Name = "Create template association",
				ProgressMessage = "Creating template association...",
				ShowProgressor = true
			};
			operation.Create(assocDesc);
			return operation.Execute() && operation.IsSucceeded ? null : operation.ErrorMessage;
		});
	}

	private static string FormatAssociationLabel(AssociationObject association, FeatureInfo fromInfo, FeatureInfo toInfo)
	{
		return $"{association.Type} {fromInfo.FeatureId} ({fromInfo.Template?.Name}) -> {toInfo.FeatureId} ({toInfo.Template?.Name})";
	}

	private static string CleanErrorMessage(string errorMessage)
	{
		return string.IsNullOrWhiteSpace(errorMessage) ? "No error details were returned by ArcGIS Pro." : errorMessage;
	}

	private static void TryTrackPlacedFeature(List<PlacedFeatureContext> createdFeatures, SimpleTemplate template, Geometry geometry, RowToken token, bool allowPlacementEnhancements = true)
	{
		if (createdFeatures == null || template == null || geometry == null || token == null)
		{
			return;
		}
		if (string.IsNullOrWhiteSpace(template.GroupLayer) || !AddinConfiguration.GroupFeatureLayerNames.Contains(template.GroupLayer.ToUpperInvariant()))
		{
			return;
		}
		createdFeatures.Add(new PlacedFeatureContext
		{
			Template = template,
			Geometry = geometry,
			Token = token,
			AllowPlacementEnhancements = allowPlacementEnhancements
		});
	}

	private static bool IsPlacementEnhancementCandidate(SimpleTemplateReference templateRef)
	{
		if (templateRef == null)
		{
			return true;
		}
		if (templateRef.Line != null && AddinConfiguration.Settings?.EnableConfiguredLinePartSplits != true)
		{
			return false;
		}
		if (templateRef.Polygon != null)
		{
			return false;
		}
		return true;
	}

	private static bool IsFeatureLayerTemplate(SimpleTemplate template)
	{
		return template != null && !string.IsNullOrWhiteSpace(template.GroupLayer) && AddinConfiguration.GroupFeatureLayerNames.Contains(template.GroupLayer.ToUpperInvariant());
	}

	private static bool IsStructureJunctionObjectTemplate(SimpleTemplate template)
	{
		if (template == null)
		{
			return false;
		}
		Dictionary<string, object> defaultFieldValues = template.DefaultFieldValues ?? new Dictionary<string, object>();
		string assetGroup = defaultFieldValues.TryGetValue("ASSETGROUP", out object assetGroupValue) ? Convert.ToString(GetObjectValue(assetGroupValue), CultureInfo.InvariantCulture) : null;
		string configuredAssetType = defaultFieldValues.TryGetValue("ASSETTYPE", out object assetTypeValue) ? Convert.ToString(GetObjectValue(assetTypeValue), CultureInfo.InvariantCulture) : null;
		return IsStructureJunctionObjectName(template.GroupLayer) ||
			IsStructureJunctionObjectName(template.SubtypeLayer) ||
			IsStructureJunctionObjectName(template.Name) ||
			(string.Equals(assetGroup, "Framing", StringComparison.OrdinalIgnoreCase) && string.Equals(configuredAssetType, "Framing", StringComparison.OrdinalIgnoreCase)) ||
			(string.Equals(assetGroup, "Pole Link", StringComparison.OrdinalIgnoreCase) && string.Equals(configuredAssetType, "Pole Link", StringComparison.OrdinalIgnoreCase));
	}

	private static bool IsStructureJunctionObjectName(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return false;
		}
		string normalized = value.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
		return normalized.IndexOf("StructureJunctionObject", StringComparison.OrdinalIgnoreCase) >= 0 ||
			normalized.IndexOf("SJO", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static async Task PopulatePlacedFeatureDetails(List<PlacedFeatureContext> createdFeatures)
	{
		if (createdFeatures == null || createdFeatures.Count == 0)
		{
			return;
		}
		foreach (PlacedFeatureContext createdFeature in createdFeatures)
		{
			createdFeature.Layer = MapMemberLookupService.GetFeatureLayerByName(createdFeature.Template.SubtypeLayer, createdFeature.Template.GroupLayer);
			createdFeature.ObjectID = createdFeature.Token.ObjectID.GetValueOrDefault();
		}
		await QueuedTask.Run(delegate
		{
			foreach (PlacedFeatureContext createdFeature in createdFeatures)
			{
				if (createdFeature.Layer == null || createdFeature.ObjectID <= 0)
				{
					continue;
				}
				QueryFilter queryFilter = new QueryFilter
				{
					ObjectIDs = new List<long> { createdFeature.ObjectID }
				};
				using RowCursor rowCursor = createdFeature.Layer.Search(queryFilter);
				if (rowCursor.MoveNext())
				{
					using Feature feature = (Feature)rowCursor.Current;
					createdFeature.Geometry = feature.GetShape();
				}
			}
		});
	}

	private static async Task CreateTableRowWithAutoAssociationAsync(SimpleTemplate template, Geometry sketchGeometry, EditOperation operation, PlacementOptions options, string parentTemplateName = null, int featureId = 0)
	{
		if (!options.IncludeConfiguredAssociations)
		{
			if (!ConfirmCreateNonSpatialWithoutAssociations(template))
			{
				throw new OperationCanceledException();
			}
			await CreateFeatureOrRowFromSimpleTemplate(template, sketchGeometry, operation, options.IncludeDefaultAttributes, parentTemplateName: parentTemplateName, featureId: featureId);
			return;
		}

		List<(FeatureLayer Layer, long ObjectID, string Label, string OwningGroup)> selectedCandidates = await GetSelectedFeaturesForTableAssociationAsync();
		if (selectedCandidates.Count == 0)
		{
			if (!ConfirmCreateNonSpatialWithoutAssociations(template))
			{
				throw new OperationCanceledException();
			}
			await CreateFeatureOrRowFromSimpleTemplate(template, sketchGeometry, operation, options.IncludeDefaultAttributes, parentTemplateName: parentTemplateName, featureId: featureId);
			return;
		}

		var candidatesWithRules = selectedCandidates
			.Select(c => (Candidate: c, Rules: FindGroupTemplateAssociations(template, c.Layer.Name, c.OwningGroup)))
			.Where(c => c.Rules.Count > 0)
			.ToList();

		if (candidatesWithRules.Count == 0)
		{
			if (!ConfirmCreateNonSpatialWithoutAssociations(template))
			{
				throw new OperationCanceledException();
			}
			await CreateFeatureOrRowFromSimpleTemplate(template, sketchGeometry, operation, options.IncludeDefaultAttributes, parentTemplateName: parentTemplateName, featureId: featureId);
			return;
		}

		string associationSummary = candidatesWithRules.Count == 1
			? $"{candidatesWithRules[0].Candidate.Label}:\n{BuildTableAssociationSummary(candidatesWithRules[0].Rules)}"
			: string.Join("\n", candidatesWithRules.Select(pair => $"{pair.Candidate.Label}:\n{BuildTableAssociationSummary(pair.Rules)}"));

		MessageBoxResult result = DialogService.Show(
			$"Create the following associations?\n\n{associationSummary}",
			"Template Editor",
			new DialogButtonChoice("Create Associations", MessageBoxResult.Yes, isPrimary: true),
			new DialogButtonChoice("Skip Associations", MessageBoxResult.No, isCancel: true));

		if (result != MessageBoxResult.Yes && !ConfirmCreateNonSpatialWithoutAssociations(template))
		{
			throw new OperationCanceledException();
		}

		RowToken rowToken = await CreateFeatureOrRowFromSimpleTemplate(template, sketchGeometry, operation, options.IncludeDefaultAttributes, parentTemplateName: parentTemplateName, featureId: featureId);

		if (result == MessageBoxResult.Yes)
		{
			RowHandle rowHandle = new RowHandle(rowToken);
			foreach (var (candidate, rules) in candidatesWithRules)
			{
				RowHandle selectedHandle = new RowHandle((MapMember)(object)candidate.Layer, candidate.ObjectID);
				foreach (var (rule, isReversed) in rules)
				{
					AssociationDescription assocDesc = CreateTableAssociationDescription(rule, isReversed, selectedHandle, rowHandle);
					if (assocDesc != null)
					{
						operation.Create(assocDesc);
					}
				}
			}
		}
	}

	private static bool ConfirmCreateNonSpatialWithoutAssociations(SimpleTemplate template)
	{
		MessageBoxResult result = DialogService.Show(
			$"Create non-spatial record '{template?.Name}' without any associations?",
			"Template Editor",
			new DialogButtonChoice("Create Without Associations", MessageBoxResult.Yes, isPrimary: true),
			new DialogButtonChoice("Cancel", MessageBoxResult.No, isCancel: true));
		return result == MessageBoxResult.Yes;
	}

	private static bool ConfirmCreateNonSpatialTemplate(SimpleTemplate template)
	{
		MessageBoxResult result = DialogService.Show(
			$"Template '{template?.Name}' creates a non-spatial record.\n\nContinue?",
			"Template Editor",
			new DialogButtonChoice("Continue", MessageBoxResult.Yes, isPrimary: true),
			new DialogButtonChoice("Cancel", MessageBoxResult.No, isCancel: true));
		return result == MessageBoxResult.Yes;
	}

	private static List<(AssociationObject Rule, bool IsReversed)> FindGroupTemplateAssociations(
		SimpleTemplate tableTemplate, string selectedLayerName, string selectedOwningGroup)
	{
		foreach (GroupTemplate groupTemplate in AddinConfiguration.Templates.GroupTemplates ?? Enumerable.Empty<GroupTemplate>())
		{
			SimpleTemplateReference tableRef = groupTemplate.SimpleTemplates?.FirstOrDefault(r =>
				string.Equals(r.Name, tableTemplate.Name, StringComparison.OrdinalIgnoreCase));
			if (tableRef == null)
			{
				continue;
			}

			// Collect all associations that involve this table template, noting which FeatureId is the spatial partner.
			var tableAssociations = new List<(AssociationObject Rule, bool IsReversed, int SpatialFeatureId)>();
			foreach (AssociationObject a in groupTemplate.Associations ?? Enumerable.Empty<AssociationObject>())
			{
				if (a.ToFeatureId == tableRef.FeatureId)
					tableAssociations.Add((a, false, a.FromFeatureId));
				else if (a.FromFeatureId == tableRef.FeatureId)
					tableAssociations.Add((a, true, a.ToFeatureId));
			}

			if (tableAssociations.Count == 0)
			{
				continue;
			}

			// For each unique spatial partner, check whether the selected feature matches.
			foreach (int spatialFeatureId in tableAssociations.Select(a => a.SpatialFeatureId).Distinct())
			{
				SimpleTemplateReference spatialRef = groupTemplate.SimpleTemplates?.FirstOrDefault(r =>
					r.FeatureId == spatialFeatureId);
				if (spatialRef == null)
				{
					continue;
				}

				SimpleTemplate spatialTemplate = AddinConfiguration.Templates.SimpleTemplates?.FirstOrDefault(t =>
					string.Equals(t.Name, spatialRef.Name, StringComparison.OrdinalIgnoreCase));
				if (spatialTemplate == null || !IsFeatureLayerTemplate(spatialTemplate))
				{
					continue;
				}

				bool groupMatches = string.Equals(selectedOwningGroup, spatialTemplate.GroupLayer, StringComparison.OrdinalIgnoreCase);
				bool layerMatches = spatialTemplate.SubtypeLayer != null
					? string.Equals(selectedLayerName, spatialTemplate.SubtypeLayer, StringComparison.OrdinalIgnoreCase)
					: string.Equals(selectedLayerName, spatialTemplate.GroupLayer, StringComparison.OrdinalIgnoreCase);

				if (!groupMatches || !layerMatches)
				{
					continue;
				}

				var matched = tableAssociations
					.Where(a => a.SpatialFeatureId == spatialFeatureId)
					.Select(a => (a.Rule, a.IsReversed))
					.ToList();

				if (matched.Count > 0)
				{
					return matched;
				}
			}
		}
		return new List<(AssociationObject, bool)>();
	}

	private static AssociationDescription CreateTableAssociationDescription(
		AssociationObject assoc, bool isReversed, RowHandle selectedHandle, RowHandle rowHandle)
	{
		RowHandle fromHandle = isReversed ? rowHandle : selectedHandle;
		RowHandle toHandle = isReversed ? selectedHandle : rowHandle;
		int terminal = isReversed ? assoc.ToTerminal : assoc.FromTerminal;

		return assoc.Type?.ToUpperInvariant() switch
		{
			"CONTAINMENT" => new AssociationDescription(AssociationType.Containment, fromHandle, toHandle, isReversed),
			"ATTACHMENT" => new AssociationDescription(AssociationType.Attachment, fromHandle, toHandle),
			"JUNCTIONJUNCTIONCONNECTIVITY" => terminal > 0
				? new AssociationDescription(UtilityNetworkAssociationTypes.JunctionJunctionConnectivity, fromHandle, (long)terminal, toHandle)
				: new AssociationDescription(UtilityNetworkAssociationTypes.JunctionJunctionConnectivity, fromHandle, toHandle),
			_ => null
		};
	}

	private static string BuildTableAssociationSummary(IEnumerable<(AssociationObject Rule, bool IsReversed)> rules)
	{
		var lines = rules.Select(r => r.Rule.Type?.ToUpperInvariant() switch
		{
			"CONTAINMENT" => "  • Containment",
			"ATTACHMENT" => "  • Structural Attachment",
			"JUNCTIONJUNCTIONCONNECTIVITY" => (r.IsReversed ? r.Rule.ToTerminal : r.Rule.FromTerminal) > 0
				? $"  • JJC (terminal {(r.IsReversed ? r.Rule.ToTerminal : r.Rule.FromTerminal)})"
				: "  • JJC",
			_ => $"  • {r.Rule.Type}"
		});
		return string.Join("\n", lines);
	}

	private static async Task<List<(FeatureLayer Layer, long ObjectID, string Label, string OwningGroup)>> GetSelectedFeaturesForTableAssociationAsync()
	{
		return await QueuedTask.Run(delegate
		{
			var result = new List<(FeatureLayer, long, string, string)>();
			if (MapView.Active == null)
			{
				return result;
			}

			var configuredGroups = new HashSet<string>(
				AddinConfiguration.GroupFeatureLayerNames ?? Enumerable.Empty<string>(),
				StringComparer.OrdinalIgnoreCase);

			foreach (FeatureLayer layer in MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>())
			{
				string owningGroup = MapMemberLookupService.GetOwningGroupName(layer);
				bool isConfigured = configuredGroups.Contains(layer.Name.ToUpperInvariant())
					|| (!string.IsNullOrWhiteSpace(owningGroup) && configuredGroups.Contains(owningGroup.ToUpperInvariant()));
				if (!isConfigured)
				{
					continue;
				}

				string layerLabel = string.IsNullOrWhiteSpace(owningGroup) || owningGroup.Equals(layer.Name, StringComparison.OrdinalIgnoreCase)
					? layer.Name
					: $"{owningGroup}/{layer.Name}";

				foreach (long oid in ((BasicFeatureLayer)layer).GetSelection().GetObjectIDs())
				{
					result.Add((layer, oid, $"{layerLabel} (OID {oid})", owningGroup));
				}
			}
			return result;
		});
	}

	private static async Task CreateSJOAttachmentsForPoles(SimpleTemplate template, Geometry sketchGeometry, EditOperation operation, PlacementOptions options)
	{
		if (options.IncludeConfiguredAssociations)
		{
			List<(FeatureLayer Layer, long ObjectID, string Label, string OwningGroup)> polesToProcess = await GetSelectedPoleCandidatesForSjoAsync();
			if (polesToProcess.Count > 0)
			{
				MessageBoxResult result = DialogService.Show(
					$"The SJO can be created as attachments for {polesToProcess.Count} selected Pole(s).",
					"Template Editor",
					new DialogButtonChoice("Create Attachments", MessageBoxResult.Yes, isPrimary: true),
					new DialogButtonChoice("Create SJO Only", MessageBoxResult.No, isCancel: true));
				if (result == MessageBoxResult.Yes)
				{
					foreach ((FeatureLayer layer, long objectId, string _, string _) in polesToProcess)
					{
						RowHandle poleHandle = new RowHandle((MapMember)(object)layer, objectId);
						RowHandle sjoHandle = new RowHandle(await CreateFeatureOrRowFromSimpleTemplate(template, sketchGeometry, operation, options.IncludeDefaultAttributes));
						AssociationDescription assocDesc = new AssociationDescription(AssociationType.Attachment, poleHandle, sjoHandle);
						operation.Create(assocDesc);
					}
					return;
				}
				else
				{
					await CreateFeatureOrRowFromSimpleTemplate(template, sketchGeometry, operation, options.IncludeDefaultAttributes);
				}
			}
			else
			{
				DialogService.Show($"No selected Pole features were found for SJO structural attachment.\n\nTemplate: {template?.Name}\nGroupLayer: {template?.GroupLayer}\nSubtypeLayer: {template?.SubtypeLayer}\n\nThe SJO will be created without an attachment.", "Template Editor");
				await CreateFeatureOrRowFromSimpleTemplate(template, sketchGeometry, operation, options.IncludeDefaultAttributes);
			}
		}
		else
		{
			await CreateFeatureOrRowFromSimpleTemplate(template, sketchGeometry, operation, options.IncludeDefaultAttributes);
		}
	}

	private static async Task<List<(FeatureLayer Layer, long ObjectID, string Label, string OwningGroup)>> GetSelectedPoleCandidatesForSjoAsync()
	{
		List<(FeatureLayer Layer, long ObjectID, string Label, string OwningGroup)> poles = new List<(FeatureLayer, long, string, string)>();
		FeatureLayer poleLayer = MapMemberLookupService.GetFeatureLayerByName("Pole", "StructureJunction");
		if (poleLayer != null)
		{
			List<long> selectedPoleOids = null;
			await QueuedTask.Run((Action)delegate
			{
				selectedPoleOids = ((BasicFeatureLayer)poleLayer).GetSelection().GetObjectIDs().ToList();
			}, TaskCreationOptions.None);
			foreach (long objectId in selectedPoleOids)
			{
				poles.Add((poleLayer, objectId, $"StructureJunction/Pole (OID {objectId})", "StructureJunction"));
			}
			if (poles.Count > 0)
			{
				return poles;
			}
		}
		List<(FeatureLayer Layer, long ObjectID, string Label, string OwningGroup)> selectedCandidates = await GetSelectedFeatureCandidatesAsync();
		foreach ((FeatureLayer layer, long objectId, string label, string owningGroup) in selectedCandidates)
		{
			if (IsStructureJunctionPoleLayer(layer, owningGroup) || await IsSupportedPoleAssetTypeAsync(layer, objectId))
			{
				poles.Add((layer, objectId, label, owningGroup));
			}
		}
		return poles;
	}

	private static async Task<List<(FeatureLayer Layer, long ObjectID, string Label, string OwningGroup)>> GetSelectedFeatureCandidatesAsync()
	{
		return await QueuedTask.Run(delegate
		{
			List<(FeatureLayer, long, string, string)> result = new List<(FeatureLayer, long, string, string)>();
			if (MapView.Active == null)
			{
				return result;
			}
			foreach (FeatureLayer layer in MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>())
			{
				List<long> objectIds = ((BasicFeatureLayer)layer).GetSelection().GetObjectIDs().ToList();
				if (objectIds.Count == 0)
				{
					continue;
				}
				string owningGroup = MapMemberLookupService.GetOwningGroupName(layer);
				string layerLabel = string.IsNullOrWhiteSpace(owningGroup) || owningGroup.Equals(layer.Name, StringComparison.OrdinalIgnoreCase)
					? layer.Name
					: $"{owningGroup}/{layer.Name}";
				foreach (long objectId in objectIds)
				{
					result.Add((layer, objectId, $"{layerLabel} (OID {objectId})", owningGroup));
				}
			}
			return result;
		});
	}

	private static bool IsStructureJunctionPoleLayer(FeatureLayer layer, string owningGroup)
	{
		if (layer == null)
		{
			return false;
		}
		return string.Equals(owningGroup, "StructureJunction", StringComparison.OrdinalIgnoreCase) &&
			(layer.Name.IndexOf("Pole", StringComparison.OrdinalIgnoreCase) >= 0 ||
			 layer.Name.IndexOf("StructureJunction", StringComparison.OrdinalIgnoreCase) >= 0);
	}

	private static async Task<bool> IsSupportedPoleAssetTypeAsync(FeatureLayer layer, long objectId)
	{
		int assetType = 0;
		try
		{
			ArcGIS.Desktop.Editing.Attributes.Inspector inspector = new ArcGIS.Desktop.Editing.Attributes.Inspector();
			await QueuedTask.Run((Action)delegate
			{
				inspector.Load((MapMember)(object)layer, objectId);
				assetType = Convert.ToInt32(inspector["ASSETTYPE"], CultureInfo.InvariantCulture);
			}, TaskCreationOptions.None);
		}
		catch (Exception ex)
		{
			LogService.LogException($"Could not inspect ASSETTYPE for pole candidate OID {objectId} on layer '{layer?.Name}'.", ex);
			return false;
		}
		return assetType == 791 || assetType == 793 || assetType == 795 || assetType == 796;
	}

	private static Geometry CreateGeometryForTemplate(SimpleTemplateReference template, Geometry sketchGeometry, double rotationDegrees = 0.0)
	{
		if (template.Location != null)
		{
			MapPoint anchorPoint = (MapPoint)sketchGeometry;
			return (Geometry)(object)CreateMapPoint(anchorPoint, template.Location, rotationDegrees);
		}
		if (template.Line != null)
		{
			MapPoint anchorPoint2 = (MapPoint)sketchGeometry;
			List<MapPoint> points = template.Line.Select((List<double> n) => CreateMapPoint(anchorPoint2, n, rotationDegrees)).ToList();
			return (Geometry)(object)PolylineBuilderEx.CreatePolyline((IEnumerable<MapPoint>)points, anchorPoint2.SpatialReference);
		}
		if (template.Polygon != null)
		{
			MapPoint anchorPoint3 = (MapPoint)sketchGeometry;
			List<MapPoint> points2 = template.Polygon.Select((List<double> n) => CreateMapPoint(anchorPoint3, n, rotationDegrees)).ToList();
			return (Geometry)(object)PolygonBuilderEx.CreatePolygon((IEnumerable<MapPoint>)points2, anchorPoint3.SpatialReference);
		}
		return sketchGeometry;
	}

	private static Geometry CreateGeometryForSingleGroupPart(SimpleTemplateReference template, Geometry sketchGeometry, double rotationDegrees = 0.0)
	{
		if (template?.Polygon != null)
		{
			return CreateGeometryForTemplate(template, sketchGeometry, rotationDegrees);
		}
		return sketchGeometry;
	}

	// Optimize: Cache preview symbols to avoid recreating them on every mouse move
	private static CIMSymbolReference _cachedPreviewPointSymbol;
	private static CIMSymbolReference _cachedPreviewLineSymbol;
	private static CIMSymbolReference _cachedPreviewPolygonSymbol;

	internal static List<PreviewOverlayGraphic> CreatePreviewGraphics(MapPoint anchorPoint, double rotationDegrees = 0.0)
	{
		DisplayTemplate selectedTemplate = AddinConfiguration.SelectedTemplate;
		string templateName = selectedTemplate?.Name;
		if (anchorPoint == null || string.IsNullOrWhiteSpace(templateName))
		{
			return new List<PreviewOverlayGraphic>();
		}

		// Optimize: Cache symbols instead of recreating on every call
		if (_cachedPreviewPointSymbol == null)
		{
			_cachedPreviewPointSymbol = CreatePreviewPointSymbol();
			_cachedPreviewLineSymbol = SymbolFactory.Instance.ConstructLineSymbol(ColorFactory.Instance.CreateRGBColor(222.0, 123.0, 207.0, 70.0), 2.0, SimpleLineStyle.Dash).MakeSymbolReference();
			_cachedPreviewPolygonSymbol = SymbolFactory.Instance.ConstructPolygonSymbol(ColorFactory.Instance.CreateRGBColor(222.0, 123.0, 207.0, 18.0), SimpleFillStyle.Solid, SymbolFactory.Instance.ConstructStroke(ColorFactory.Instance.CreateRGBColor(0.0, 133.0, 202.0, 70.0), 2.0, SimpleLineStyle.Dash)).MakeSymbolReference();
		}

		List<PreviewOverlayGraphic> graphics = new List<PreviewOverlayGraphic>();
		if (selectedTemplate.IsGroupChild)
		{
			SimpleTemplateReference childTemplateRef = GetGroupChildReference(selectedTemplate);
			// Optimize: Use TemplateCache for O(1) lookup instead of O(n) FirstOrDefault
			SimpleTemplate childTemplate = TemplateCache.GetSimpleTemplate(childTemplateRef?.Name);
			if (childTemplateRef == null || childTemplate == null || !IsFeatureLayerTemplate(childTemplate))
			{
				return graphics;
			}
			AddPreviewGraphicForTemplateReference(graphics, childTemplateRef, childTemplate, anchorPoint, rotationDegrees, _cachedPreviewPointSymbol, _cachedPreviewLineSymbol, _cachedPreviewPolygonSymbol, useAllConfiguredGeometry: false);
			return graphics;
		}
		// Optimize: Use TemplateCache for O(1) lookup instead of O(n) FirstOrDefault
		GroupTemplate groupTemplate = TemplateCache.GetGroupTemplate(templateName);
		if (groupTemplate?.SimpleTemplates == null)
		{
			// Optimize: Use TemplateCache for O(1) lookup instead of O(n) FirstOrDefault
			SimpleTemplate simpleTemplate = TemplateCache.GetSimpleTemplate(templateName);
			if (HasConfiguredSimpleGeometry(simpleTemplate))
			{
				Geometry geometry = CreateGeometryForSimpleTemplate(simpleTemplate, anchorPoint, rotationDegrees);
				AddPreviewGraphic(graphics, geometry, _cachedPreviewPointSymbol, _cachedPreviewLineSymbol, _cachedPreviewPolygonSymbol);
			}
			else if (IsSimplePointTemplate(simpleTemplate))
			{
				graphics.Add(new PreviewOverlayGraphic(anchorPoint, _cachedPreviewPointSymbol));
			}
			return graphics;
		}
		if (!HasConfiguredPlacementGeometry(groupTemplate))
		{
			return graphics;
		}
		foreach (SimpleTemplateReference simpleTemplateRef in groupTemplate.SimpleTemplates)
		{
			// Optimize: Use TemplateCache for O(1) lookup instead of O(n) FirstOrDefault inside loop
			SimpleTemplate template = TemplateCache.GetSimpleTemplate(simpleTemplateRef.Name);
			if (!IsFeatureLayerTemplate(template))
			{
				continue;
			}
			AddPreviewGraphicForTemplateReference(graphics, simpleTemplateRef, template, anchorPoint, rotationDegrees, _cachedPreviewPointSymbol, _cachedPreviewLineSymbol, _cachedPreviewPolygonSymbol, useAllConfiguredGeometry: true);
		}
		return graphics;
	}

	private static void AddPreviewGraphicForTemplateReference(List<PreviewOverlayGraphic> graphics, SimpleTemplateReference templateRef, SimpleTemplate template, MapPoint anchorPoint, double rotationDegrees, CIMSymbolReference pointSymbol, CIMSymbolReference lineSymbol, CIMSymbolReference polygonSymbol, bool useAllConfiguredGeometry)
	{
		if (templateRef == null || template == null)
		{
			return;
		}
		if (useAllConfiguredGeometry && (templateRef.Location != null || templateRef.Line != null || templateRef.Polygon != null))
		{
			Geometry geometry = CreateGeometryForTemplate(templateRef, (Geometry)(object)anchorPoint, rotationDegrees);
			AddPreviewGraphic(graphics, geometry, pointSymbol, lineSymbol, polygonSymbol);
			return;
		}
		if (!useAllConfiguredGeometry && templateRef.Polygon != null)
		{
			Geometry geometry = CreateGeometryForTemplate(templateRef, (Geometry)(object)anchorPoint, rotationDegrees);
			AddPreviewGraphic(graphics, geometry, pointSymbol, lineSymbol, polygonSymbol);
			return;
		}
		if (IsSimplePointTemplate(template))
		{
			graphics.Add(new PreviewOverlayGraphic(anchorPoint, pointSymbol));
		}
	}

	private static bool HasConfiguredSimpleGeometry(SimpleTemplate template)
	{
		return IsFeatureLayerTemplate(template) && template.Geometry != null && template.Geometry.Count >= 3;
	}

	private static Geometry CreateGeometryForSimpleTemplate(SimpleTemplate template, MapPoint anchorPoint, double rotationDegrees)
	{
		List<MapPoint> points = template.Geometry.Select((List<double> n) => CreateMapPoint(anchorPoint, n, rotationDegrees)).ToList();
		return (Geometry)(object)PolygonBuilderEx.CreatePolygon((IEnumerable<MapPoint>)points, anchorPoint.SpatialReference);
	}

	private static void AddPreviewGraphic(List<PreviewOverlayGraphic> graphics, Geometry geometry, CIMSymbolReference pointSymbol, CIMSymbolReference lineSymbol, CIMSymbolReference polygonSymbol)
	{
		if (geometry is MapPoint mapPoint)
		{
			graphics.Add(new PreviewOverlayGraphic(mapPoint, pointSymbol));
		}
		else if (geometry is Polyline polyline)
		{
			graphics.Add(new PreviewOverlayGraphic(polyline, lineSymbol));
		}
		else if (geometry is Polygon polygon)
		{
			graphics.Add(new PreviewOverlayGraphic(polygon, polygonSymbol));
		}
	}

	private static CIMSymbolReference CreatePreviewPointSymbol()
	{
		CIMColor fillColor = ColorFactory.Instance.CreateRGBColor(222.0, 123.0, 207.0, 70.0);
		CIMColor outlineColor = ColorFactory.Instance.CreateRGBColor(0.0, 133.0, 202.0, 70.0);
		CIMPolygonSymbol markerSymbol = SymbolFactory.Instance.ConstructPolygonSymbol(fillColor, SimpleFillStyle.Solid, SymbolFactory.Instance.ConstructStroke(outlineColor, 1.5, SimpleLineStyle.Solid));
		CIMPointSymbol pointSymbol = SymbolFactory.Instance.ConstructPointSymbol(fillColor, 9.0, SimpleMarkerStyle.Circle);
		foreach (CIMSymbolLayer layer in pointSymbol.SymbolLayers ?? Array.Empty<CIMSymbolLayer>())
		{
			if (layer is CIMVectorMarker vectorMarker && vectorMarker.MarkerGraphics != null)
			{
				foreach (CIMMarkerGraphic markerGraphic in vectorMarker.MarkerGraphics)
				{
					markerGraphic.Symbol = markerSymbol;
				}
			}
		}
		return pointSymbol.MakeSymbolReference();
	}

	private static bool IsSimplePointTemplate(SimpleTemplate template)
	{
		if (!IsFeatureLayerTemplate(template))
		{
			return false;
		}
		FeatureLayer layer = MapMemberLookupService.GetFeatureLayerByName(template.SubtypeLayer, template.GroupLayer);
		return layer != null && GeometryTypeHelper.IsPoint(layer.GetFeatureClass().GetDefinition().GetShapeType());
	}

	private static MapPoint CreateMapPoint(MapPoint anchorPoint, List<double> templatePoint, double rotationDegrees = 0.0)
	{
		if (anchorPoint == null || templatePoint == null || templatePoint.Count < 2)
		{
			throw new InvalidOperationException("Template geometry points must include X and Y offsets.");
		}
		double xOffset = templatePoint[0];
		double yOffset = templatePoint[1];
		ApplyMirrorMode(ref xOffset, ref yOffset);
		if (Math.Abs(rotationDegrees) > 0.0001)
		{
			double radians = rotationDegrees * Math.PI / 180.0;
			double cos = Math.Cos(radians);
			double sin = Math.Sin(radians);
			double rotatedX = xOffset * cos - yOffset * sin;
			double rotatedY = xOffset * sin + yOffset * cos;
			xOffset = rotatedX;
			yOffset = rotatedY;
		}
		return MapPointBuilderEx.CreateMapPoint(anchorPoint.X + xOffset, anchorPoint.Y + yOffset, anchorPoint.SpatialReference);
	}

	private static void ApplyMirrorMode(ref double xOffset, ref double yOffset)
	{
		switch (AddinConfiguration.PlacementMirrorMode)
		{
		case PlacementMirrorMode.Horizontal:
			xOffset = -xOffset;
			break;
		case PlacementMirrorMode.Vertical:
			yOffset = -yOffset;
			break;
		case PlacementMirrorMode.Both:
			xOffset = -xOffset;
			yOffset = -yOffset;
			break;
		}
	}

	private static AssociationDescription CreateAssociationDescription(AssociationObject association, FeatureInfo fromInfo, FeatureInfo toInfo)
	{
		RowHandle fromHandle = CreateRowHandle(fromInfo);
		RowHandle toHandle = CreateRowHandle(toInfo);
		AssociationDescription description = null;
		switch (association.Type.ToUpper())
		{
		case "CONTAINMENT":
			description = new AssociationDescription(AssociationType.Containment, fromHandle, toHandle, toInfo.IsSpatialFeature);
			break;
		case "ATTACHMENT":
			description = new AssociationDescription(AssociationType.Attachment, fromHandle, toHandle);
			break;
		case "JUNCTIONJUNCTIONCONNECTIVITY":
			description = ((association.FromTerminal == 0) ? new AssociationDescription(UtilityNetworkAssociationTypes.JunctionJunctionConnectivity, fromHandle, toHandle) : new AssociationDescription(UtilityNetworkAssociationTypes.JunctionJunctionConnectivity, fromHandle, (long)association.FromTerminal, toHandle));
			break;
		case "JUNCTIONEDGEOBJECTCONNECTIVITYFROMSIDE":
			description = new AssociationDescription(UtilityNetworkAssociationTypes.JunctionEdgeObjectFromSide, fromHandle, toHandle);
			break;
		case "JUNCTIONEDGEOBJECTCONNECTIVITYTOSIDE":
			description = new AssociationDescription(UtilityNetworkAssociationTypes.JunctionEdgeObjectToSide, fromHandle, toHandle);
			break;
		case "JUNCTIONEDGEOBJECTCONNECTIVITYMIDSPAN":
			description = new AssociationDescription(UtilityNetworkAssociationTypes.JunctionEdgeObjectMidspan, fromHandle, toHandle);
			break;
		}
		return description;
	}

	private static RowHandle CreateRowHandle(FeatureInfo featureInfo)
	{
		if (featureInfo.MapMember != null && featureInfo.ObjectID > 0)
		{
			return new RowHandle(featureInfo.MapMember, featureInfo.ObjectID);
		}
		return new RowHandle(featureInfo.Token);
	}

	private static async Task<RowToken> CreateFeatureOrRowFromSimpleTemplate(SimpleTemplate template, Geometry geometry, EditOperation operation, bool includeDefaultAttributes, double rotationDegrees = 0.0, string parentTemplateName = null, int featureId = 0)
	{
		RowToken token;
		if (template == null)
		{
			throw new InvalidOperationException("Template configuration references a missing simple template.");
		}
		string placementPartKey = PlacementAttributeOverrideService.BuildPlacementPartKey(template, parentTemplateName, featureId);
		if (AddinConfiguration.GroupFeatureLayerNames.Contains(template.GroupLayer.ToUpperInvariant()))
		{
			FeatureLayer layer = MapMemberLookupService.GetFeatureLayerByName(template.SubtypeLayer, template.GroupLayer);
			if (layer == null)
			{
				throw new InvalidOperationException($"Layer '{template.GroupLayer}/{template.SubtypeLayer}' was not found for template '{template.Name}'.");
			}
			token = await CreateFeature(layer, geometry, template, operation, includeDefaultAttributes, rotationDegrees, placementPartKey);
		}
		else
		{
			StandaloneTable table = MapMemberLookupService.GetTableByName(template.SubtypeLayer, template.GroupLayer);
			if (table == null)
			{
				throw new InvalidOperationException($"Table '{template.GroupLayer}/{template.SubtypeLayer}' was not found for template '{template.Name}'.");
			}
			token = await CreateTableRow(table, template, operation, includeDefaultAttributes, placementPartKey);
		}
		return token;
	}

	private static async Task<RowToken> CreateFeature(FeatureLayer layer, Geometry geometry, SimpleTemplate template, EditOperation operation, bool includeDefaultAttributes, double rotationDegrees = 0.0, string placementPartKey = null)
	{
		Dictionary<string, object> defaultFieldValues = template.DefaultFieldValues ?? new Dictionary<string, object>();
		string subtypeField = null;
		List<Field> fields = null;
		await QueuedTask.Run((Action)delegate
		{
			subtypeField = ((TableDefinition)layer.GetFeatureClass().GetDefinition()).GetSubtypeField();
			fields = ((TableDefinition)layer.GetFeatureClass().GetDefinition()).GetFields().ToList();
		}, TaskCreationOptions.None);
		DataSubtype subtype = null;
		if (!string.IsNullOrEmpty(subtypeField))
		{
			await QueuedTask.Run((Action)delegate
			{
				List<DataSubtype> source = ((TableDefinition)layer.GetFeatureClass().GetDefinition()).GetSubtypes().ToList();
				if (!defaultFieldValues.TryGetValue(subtypeField, out object subtypeConfigValue))
				{
					throw new InvalidOperationException($"Template '{template.Name}' is missing subtype field '{subtypeField}'.");
				}
				string subtypeDesc = Convert.ToString(GetObjectValue(subtypeConfigValue), CultureInfo.InvariantCulture);
				subtype = source.FirstOrDefault((DataSubtype n) => n.GetName() == subtypeDesc);
			}, TaskCreationOptions.None);
		}
		if (template.Geometry != null)
		{
			MapPoint anchorPoint = (MapPoint)geometry;
			List<MapPoint> points = template.Geometry.Select((List<double> n) => CreateMapPoint(anchorPoint, n, rotationDegrees)).ToList();
			geometry = (Geometry)(object)PolygonBuilderEx.CreatePolygon((IEnumerable<MapPoint>)points, anchorPoint.SpatialReference);
		}
		Dictionary<string, object> effectiveFieldValues = await PlacementAttributeOverrideService.ApplyOverridesAsync(template, defaultFieldValues, subtype, fields, placementPartKey);
		Dictionary<string, object> attributes = new Dictionary<string, object> { ["SHAPE"] = geometry };
		foreach (string fieldName in GetAttributeFieldsToApply(effectiveFieldValues, subtypeField, includeDefaultAttributes, fields, rotationDegrees))
		{
			Dictionary<string, object> dictionary = attributes;
			string key = fieldName;
			dictionary[key] = await GetDatabaseFieldValueFromConfigValue(effectiveFieldValues, subtype, fields, fieldName, rotationDegrees);
		}
		return operation.Create((MapMember)(object)layer, attributes);
	}

	private static async Task<RowToken> CreateTableRow(StandaloneTable table, SimpleTemplate template, EditOperation operation, bool includeDefaultAttributes, string placementPartKey = null)
	{
		Dictionary<string, object> defaultFieldValues = template.DefaultFieldValues ?? new Dictionary<string, object>();
		string subtypeField = null;
		List<Field> fields = null;
		await QueuedTask.Run((Action)delegate
		{
			subtypeField = table.GetTable().GetDefinition().GetSubtypeField();
			fields = table.GetTable().GetDefinition().GetFields()
				.ToList();
		}, TaskCreationOptions.None);
		DataSubtype subtype = null;
		if (!string.IsNullOrEmpty(subtypeField))
		{
			await QueuedTask.Run((Action)delegate
			{
				List<DataSubtype> source = table.GetTable().GetDefinition().GetSubtypes()
					.ToList();
				if (!defaultFieldValues.TryGetValue(subtypeField, out object subtypeConfigValue))
				{
					throw new InvalidOperationException($"Template '{template.Name}' is missing subtype field '{subtypeField}'.");
				}
				string subtypeDesc = Convert.ToString(GetObjectValue(subtypeConfigValue), CultureInfo.InvariantCulture);
				subtype = source.FirstOrDefault((DataSubtype n) => n.GetName() == subtypeDesc);
			}, TaskCreationOptions.None);
		}
		Dictionary<string, object> effectiveFieldValues = await PlacementAttributeOverrideService.ApplyOverridesAsync(template, defaultFieldValues, subtype, fields, placementPartKey);
		Dictionary<string, object> attributes = new Dictionary<string, object>();
		foreach (string fieldName in GetAttributeFieldsToApply(effectiveFieldValues, subtypeField, includeDefaultAttributes))
		{
			Dictionary<string, object> dictionary = attributes;
			string key = fieldName;
			dictionary[key] = await GetDatabaseFieldValueFromConfigValue(effectiveFieldValues, subtype, fields, fieldName);
		}
		return operation.Create((MapMember)(object)table, attributes);
	}

	private static IEnumerable<string> GetAttributeFieldsToApply(Dictionary<string, object> defaultFieldValues, string subtypeField, bool includeDefaultAttributes, List<Field> fields = null, double rotationDegrees = 0.0)
	{
		IEnumerable<string> attributeFields;
		if (includeDefaultAttributes)
		{
			attributeFields = defaultFieldValues.Keys;
		}
		else if (string.IsNullOrWhiteSpace(subtypeField))
		{
			attributeFields = Enumerable.Empty<string>();
		}
		else
		{
			attributeFields = defaultFieldValues.Keys.Where((string fieldName) => string.Equals(fieldName, subtypeField, StringComparison.OrdinalIgnoreCase));
		}
		if (!ShouldApplySymbolRotation(rotationDegrees))
		{
			return attributeFields;
		}
		IEnumerable<string> rotationFields = (fields ?? new List<Field>())
			.Where((Field field) => IsSymbolRotationField(field?.Name))
			.Select((Field field) => field.Name);
		return attributeFields.Concat(rotationFields)
			.GroupBy((string fieldName) => NormalizeFieldIdentifier(fieldName))
			.Select((IGrouping<string, string> group) => group.First());
	}

	private static async Task<object> GetDatabaseFieldValueFromConfigValue(Dictionary<string, object> defaultFieldValues, DataSubtype subtype, List<Field> fields, string fieldName, double rotationDegrees = 0.0)
	{
		object fieldValue = null;
		bool hasConfiguredFieldValue = defaultFieldValues.TryGetValue(fieldName, out object rawConfigFieldValue);
		object configFieldValue = GetObjectValue(rawConfigFieldValue);
		Field field = fields.FirstOrDefault((Field n) => string.Equals(n.Name, fieldName, StringComparison.OrdinalIgnoreCase));
		if (field == null)
		{
			throw new InvalidOperationException($"Field '{fieldName}' was not found.");
		}
		configFieldValue = GetSymbolRotationFieldValue(configFieldValue, hasConfiguredFieldValue, field, fieldName, rotationDegrees);
		await QueuedTask.Run((Action)delegate
		{
			if (subtype != null)
			{
				DataDomain domain = field.GetDomain(subtype);
				if (domain != null)
				{
					fieldValue = GetCodedDomainValue(domain, configFieldValue);
				}
				else
				{
					domain = field.GetDomain((DataSubtype)null);
					if (domain != null)
					{
						fieldValue = GetCodedDomainValue(domain, configFieldValue);
					}
					else
					{
						fieldValue = ConvertValueToFieldType(field, configFieldValue, subtype);
					}
				}
			}
			else
			{
				DataDomain domain2 = field.GetDomain((DataSubtype)null);
				if (domain2 != null)
				{
					fieldValue = GetCodedDomainValue(domain2, configFieldValue);
				}
				else
				{
					fieldValue = ConvertValueToFieldType(field, configFieldValue, subtype);
				}
			}
		}, TaskCreationOptions.None);
		return fieldValue;
	}

	private static object GetSymbolRotationFieldValue(object configFieldValue, bool hasConfiguredFieldValue, Field field, string configFieldName, double rotationDegrees)
	{
		if (!ShouldApplySymbolRotation(rotationDegrees))
		{
			return configFieldValue;
		}
		if (!IsSymbolRotationField(field?.Name) && !IsSymbolRotationField(configFieldName))
		{
			return configFieldValue;
		}
		bool useDefaultRotation = !hasConfiguredFieldValue || IsBlankValue(configFieldValue);
		double? templateRotation = useDefaultRotation ? GetDefaultSymbolRotationWhenMissing() : TryGetDouble(configFieldValue);
		if (!templateRotation.HasValue)
		{
			return configFieldValue;
		}
		double mirroredRotation = ApplyMirrorModeToSymbolRotation(templateRotation.Value);
		double adjustedRotation = NormalizeSymbolRotation(mirroredRotation + rotationDegrees);
		string source = useDefaultRotation ? "default missing-field value" : "template value";
		LogService.Write($"Adjusted symbol rotation field '{configFieldName}' from {templateRotation.Value:0.######} ({source}) to {adjustedRotation:0.######} (placement rotation {rotationDegrees:0.######}, mirror {AddinConfiguration.PlacementMirrorMode}).");
		return FormatSymbolRotationFieldValue(adjustedRotation, configFieldValue, field);
	}

	private static object ConvertValueToFieldType(Field field, object value, DataSubtype subtype)
	{
		value = GetObjectValue(value);
		if (value == null)
		{
			return null;
		}
		string text = value as string;
		if ((field.FieldType == FieldType.Integer || field.FieldType == FieldType.SmallInteger || field.FieldType == FieldType.BigInteger) &&
			!string.IsNullOrWhiteSpace(text) &&
			subtype != null &&
			string.Equals(text.Trim(), subtype.GetName(), StringComparison.OrdinalIgnoreCase))
		{
			object subtypeCode = subtype.GetCode();
			if (field.FieldType == FieldType.BigInteger)
			{
				return Convert.ToInt64(subtypeCode, CultureInfo.InvariantCulture);
			}
			return Convert.ToInt32(subtypeCode, CultureInfo.InvariantCulture);
		}
		if (field.FieldType == FieldType.BigInteger)
		{
			return value is long longValue ? longValue : long.Parse(text, CultureInfo.InvariantCulture);
		}
		if (field.FieldType == FieldType.Integer || field.FieldType == FieldType.SmallInteger)
		{
			return value is int intValue ? intValue : int.Parse(text, CultureInfo.InvariantCulture);
		}
		if (field.FieldType == FieldType.Single)
		{
			return value is float floatValue ? floatValue : float.Parse(text, CultureInfo.InvariantCulture);
		}
		if (field.FieldType == FieldType.Double)
		{
			return value is double doubleValue ? doubleValue : double.Parse(text, CultureInfo.InvariantCulture);
		}
		return value;
	}

	private static bool IsSymbolRotationField(string fieldName)
	{
		if (string.IsNullOrWhiteSpace(fieldName))
		{
			return false;
		}
		string normalizedFieldName = NormalizeFieldIdentifier(fieldName);
		List<string> rotationFieldNames = AddinConfiguration.Settings?.SymbolRotationFieldNames;
		IEnumerable<string> configuredNames = rotationFieldNames?.Count > 0
			? rotationFieldNames
			: new[] { "ROTATION", "SYMBOLROTATION", "SYMBOL_ROTATION", "ANGLE" };
		return configuredNames.Any((string rotationFieldName) =>
			string.Equals(rotationFieldName, fieldName, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(NormalizeFieldIdentifier(rotationFieldName), normalizedFieldName, StringComparison.OrdinalIgnoreCase));
	}

	private static bool ShouldApplySymbolRotation(double rotationDegrees)
	{
		return Math.Abs(rotationDegrees) > 0.0001 || AddinConfiguration.PlacementMirrorMode != PlacementMirrorMode.None;
	}

	private static bool IsBlankValue(object value)
	{
		return value == null || value is string text && string.IsNullOrWhiteSpace(text);
	}

	private static double GetDefaultSymbolRotationWhenMissing()
	{
		return AddinConfiguration.Settings?.DefaultSymbolRotationWhenMissing ?? 90.0;
	}

	private static string NormalizeFieldIdentifier(string fieldName)
	{
		return new string((fieldName ?? string.Empty)
			.Where(char.IsLetterOrDigit)
			.Select(char.ToUpperInvariant)
			.ToArray());
	}

	private static double? TryGetDouble(object value)
	{
		value = GetObjectValue(value);
		if (value == null)
		{
			return 0.0;
		}
		switch (value)
		{
		case double doubleValue:
			return doubleValue;
		case float floatValue:
			return floatValue;
		case decimal decimalValue:
			return (double)decimalValue;
		case int intValue:
			return intValue;
		case long longValue:
			return longValue;
		case short shortValue:
			return shortValue;
		case byte byteValue:
			return (int)byteValue;
		case string text:
			if (string.IsNullOrWhiteSpace(text))
			{
				return 0.0;
			}
			if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double invariantValue))
			{
				return invariantValue;
			}
			if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double currentValue))
			{
				return currentValue;
			}
			break;
		}
		return null;
	}

	private static double NormalizeSymbolRotation(double degrees)
	{
		degrees %= 360.0;
		if (degrees < 0.0)
		{
			degrees += 360.0;
		}
		return degrees;
	}

	private static double ApplyMirrorModeToSymbolRotation(double degrees)
	{
		return AddinConfiguration.PlacementMirrorMode switch
		{
			PlacementMirrorMode.Horizontal => 180.0 - degrees,
			PlacementMirrorMode.Vertical => 0.0 - degrees,
			PlacementMirrorMode.Both => degrees + 180.0,
			_ => degrees
		};
	}

	private static object FormatSymbolRotationFieldValue(double degrees, object templateValue, Field field)
	{
		if (templateValue is string)
		{
			return degrees.ToString("0.######", CultureInfo.InvariantCulture);
		}
		if (field.FieldType == FieldType.BigInteger || field.FieldType == FieldType.Integer || field.FieldType == FieldType.SmallInteger)
		{
			return Convert.ToInt32(Math.Round(degrees, MidpointRounding.AwayFromZero));
		}
		if (field.FieldType == FieldType.Single || field.FieldType == FieldType.Double)
		{
			return degrees;
		}
		return degrees.ToString("0.######", CultureInfo.InvariantCulture);
	}

	private static object GetCodedDomainValue(DataDomain domain, object configFieldValue)
	{
		if (configFieldValue == null)
		{
			return null;
		}
		return (domain is CodedValueDomain) ? ((CodedValueDomain)domain).GetCodedValue(configFieldValue.ToString()) : configFieldValue;
	}

	public static async Task<string> ValidateConfiguration()
	{
		TemplateConfig templates = GetLoadedTemplateConfigOrThrow();
		string message = null;
		List<string> errors = new List<string>();
		foreach (SimpleTemplate template in templates.SimpleTemplates)
		{
			string error = ValidateLayerOrTableName(template);
			if (error != null)
			{
				errors.Add(error);
			}
		}
		if (errors.Count == 0)
		{
			foreach (SimpleTemplate template2 in templates.SimpleTemplates)
			{
				string error2 = await ValidateLayerOrTableFields(template2);
				if (error2 != null)
				{
					errors.Add(error2);
				}
			}
		}
		if (errors.Count == 0)
		{
			foreach (SimpleTemplate template3 in templates.SimpleTemplates)
			{
				string error3 = await ValidateSubtypeAndDomains(template3);
				if (error3 != null)
				{
					errors.Add(error3);
				}
			}
		}
		if (errors.Count == 0)
		{
			List<string> simpleTemplateNames = templates.SimpleTemplates.Select((SimpleTemplate n) => n.Name.ToUpper()).ToList();
			foreach (GroupTemplate groupTemplate in templates.GroupTemplates)
			{
				List<string> invalidTemplateNames = (from n in groupTemplate.SimpleTemplates
					select n.Name.ToUpper() into n
					where !simpleTemplateNames.Contains(n)
					select n).ToList();
				if (invalidTemplateNames.Count > 0)
				{
					string error4 = $"Group template {groupTemplate.Name} contains references to invalid simple template(s): {string.Join(", ", invalidTemplateNames)}.";
					errors.Add(error4);
				}
			}
		}
		if (errors.Count == 0)
		{
			foreach (GroupTemplate groupTemplate2 in templates.GroupTemplates)
			{
				int distinctFeatureIdCount = groupTemplate2.SimpleTemplates.Select((SimpleTemplateReference n) => n.FeatureId).Distinct().Count();
				if (distinctFeatureIdCount != groupTemplate2.SimpleTemplates.Count)
				{
					string error5 = "Group template " + groupTemplate2.Name + " has non-unique Feature IDs across its simple templates.";
					errors.Add(error5);
				}
			}
		}
		if (errors.Count == 0)
		{
			foreach (GroupTemplate groupTemplate in templates.GroupTemplates)
			{
				if (groupTemplate.SimpleTemplates?.Any((SimpleTemplateReference templateRef) => templateRef.FeatureId == 1) != true)
				{
					errors.Add($"Group template {groupTemplate.Name} must include a simple template reference with FeatureId 1 to define the sketch feature.");
				}
			}
		}
		if (errors.Count == 0)
		{
			foreach (GroupTemplate groupTemplate3 in templates.GroupTemplates)
			{
				foreach (SimpleTemplateReference templateRef in groupTemplate3.SimpleTemplates)
				{
					string error6 = await ValidateGeometry(template: templates.SimpleTemplates.FirstOrDefault((SimpleTemplate n) => n.Name == templateRef.Name), groupTemplate: groupTemplate3, templateRef: templateRef);
					if (error6 != null)
					{
						errors.Add(error6);
					}
				}
			}
		}
		if (errors.Count == 0)
		{
			foreach (GroupTemplate groupTemplate4 in templates.GroupTemplates)
			{
				List<int> featureIds = (groupTemplate4.SimpleTemplates ?? new List<SimpleTemplateReference>()).Select((SimpleTemplateReference n) => n.FeatureId).ToList();
				foreach (AssociationObject assoc in groupTemplate4.Associations ?? new List<AssociationObject>())
				{
					if (!featureIds.Contains(assoc.FromFeatureId) || !featureIds.Contains(assoc.ToFeatureId))
					{
						string error7 = "Group template " + groupTemplate4.Name + " has associations with invalid FeatureIDs.";
						errors.Add(error7);
					}
				}
			}
		}
		if (errors.Count > 0)
		{
			message = string.Join("\n", errors);
		}
		return message;
	}

	private static string ValidateLayerOrTableName(SimpleTemplate template)
	{
		string error = null;
		if (template == null)
		{
			return "Template configuration contains a null simple template.";
		}
		if (string.IsNullOrWhiteSpace(template.GroupLayer))
		{
			return $"Template '{template.Name}' is missing GroupLayer.";
		}
		if (AddinConfiguration.GroupFeatureLayerNames.Contains(template.GroupLayer.ToUpper()))
		{
			FeatureLayer layer = MapMemberLookupService.GetFeatureLayerByName(template.SubtypeLayer, template.GroupLayer);
			if (layer == null)
			{
				error = $"Group layer/subtype layer {template.GroupLayer}/{template.SubtypeLayer} does not exist in the map ({template.Name}).";
			}
		}
		else
		{
			StandaloneTable table = MapMemberLookupService.GetTableByName(template.SubtypeLayer, template.GroupLayer);
			if (table == null)
			{
				error = $"Group table/subtype table {template.GroupLayer}/{template.SubtypeLayer} does not exist in the map ({template.Name}).";
			}
		}
		return error;
	}

	private static async Task<string> ValidateLayerOrTableFields(SimpleTemplate template)
	{
		string error = null;
		List<string> fieldNamesInDatabase = null;
		string subtypeField = null;
		bool isFeatureLayer = AddinConfiguration.GroupFeatureLayerNames.Contains(template.GroupLayer.ToUpper());
		if (isFeatureLayer)
		{
			FeatureLayer layer = MapMemberLookupService.GetFeatureLayerByName(template.SubtypeLayer, template.GroupLayer);
			if (layer == null)
			{
				return $"Group layer/subtype layer {template.GroupLayer}/{template.SubtypeLayer} does not exist in the map ({template.Name}).";
			}
			await QueuedTask.Run((Action)delegate
			{
				fieldNamesInDatabase = (from n in ((TableDefinition)layer.GetFeatureClass().GetDefinition()).GetFields()
					select n.Name.ToUpper()).ToList();
				subtypeField = ((TableDefinition)layer.GetFeatureClass().GetDefinition()).GetSubtypeField()?.ToUpperInvariant();
			}, TaskCreationOptions.None);
		}
		else
		{
			StandaloneTable table = MapMemberLookupService.GetTableByName(template.SubtypeLayer, template.GroupLayer);
			if (table == null)
			{
				return $"Group table/subtype table {template.GroupLayer}/{template.SubtypeLayer} does not exist in the map ({template.Name}).";
			}
			await QueuedTask.Run((Action)delegate
			{
				fieldNamesInDatabase = (from n in table.GetTable().GetDefinition().GetFields()
					select n.Name.ToUpper()).ToList();
				subtypeField = table.GetTable().GetDefinition().GetSubtypeField()?.ToUpperInvariant();
			}, TaskCreationOptions.None);
		}
		Dictionary<string, object> defaultFieldValues = template.DefaultFieldValues ?? new Dictionary<string, object>();
		List<string> defaultFieldNames = defaultFieldValues.Keys.Select((string n) => n.ToUpper()).ToList();
		List<string> invalidFields = defaultFieldNames.Where((string n) => !fieldNamesInDatabase.Contains(n)).ToList();
		if (invalidFields.Count > 0)
		{
			string layerType = (isFeatureLayer ? "LAYER" : "TABLE");
			error = $"{layerType} {template.GroupLayer}/{template.SubtypeLayer}: field(s) {string.Join(", ", invalidFields)} are not valid ({template.Name}).";
		}
		if (!string.IsNullOrEmpty(subtypeField) && !defaultFieldNames.Contains(subtypeField))
		{
			string layerType2 = (isFeatureLayer ? "LAYER" : "TABLE");
			string delimiter = ((error != null) ? "\n" : "");
			error += $"{delimiter}{layerType2} {template.GroupLayer}/{template.SubtypeLayer}: missing subtype field {subtypeField} ({template.Name}).";
		}
		return error;
	}

	private static async Task<string> ValidateSubtypeAndDomains(SimpleTemplate template)
	{
		string error = null;
		List<DataSubtype> subtypes = null;
		string subtypeField = null;
		List<Field> fields = null;
		bool isFeatureLayer = AddinConfiguration.GroupFeatureLayerNames.Contains(template.GroupLayer.ToUpper());
		if (isFeatureLayer)
		{
			FeatureLayer layer = MapMemberLookupService.GetFeatureLayerByName(template.SubtypeLayer, template.GroupLayer);
			if (layer == null)
			{
				return $"Group layer/subtype layer {template.GroupLayer}/{template.SubtypeLayer} does not exist in the map ({template.Name}).";
			}
			await QueuedTask.Run((Action)delegate
			{
				FeatureClassDefinition definition = layer.GetFeatureClass().GetDefinition();
				subtypes = ((TableDefinition)definition).GetSubtypes().ToList();
				subtypeField = ((TableDefinition)definition).GetSubtypeField()?.ToUpperInvariant();
				fields = ((TableDefinition)definition).GetFields().ToList();
			}, TaskCreationOptions.None);
		}
		else
		{
			StandaloneTable table = MapMemberLookupService.GetTableByName(template.SubtypeLayer, template.GroupLayer);
			if (table == null)
			{
				return $"Group table/subtype table {template.GroupLayer}/{template.SubtypeLayer} does not exist in the map ({template.Name}).";
			}
			await QueuedTask.Run((Action)delegate
			{
				TableDefinition definition = table.GetTable().GetDefinition();
				subtypes = definition.GetSubtypes().ToList();
				subtypeField = definition.GetSubtypeField()?.ToUpperInvariant();
				fields = definition.GetFields().ToList();
			}, TaskCreationOptions.None);
		}
		if (subtypes.Count > 0)
		{
			List<string> subtypeDescs = null;
			await QueuedTask.Run((Action)delegate
			{
				subtypeDescs = subtypes.Select((DataSubtype n) => n.GetName()).ToList();
			}, TaskCreationOptions.None);
			Dictionary<string, object> defaultFieldValues = template.DefaultFieldValues ?? new Dictionary<string, object>();
			if (string.IsNullOrWhiteSpace(subtypeField) || !defaultFieldValues.ContainsKey(subtypeField))
			{
				string layerType = (isFeatureLayer ? "LAYER" : "TABLE");
				return $"{layerType} {template.GroupLayer}/{template.SubtypeLayer}: missing subtype field {subtypeField} ({template.Name}).";
			}
			string subtypeDesc = Convert.ToString(GetObjectValue(defaultFieldValues[subtypeField]), CultureInfo.InvariantCulture);
			if (!subtypeDescs.Contains(subtypeDesc))
			{
				string layerType = (isFeatureLayer ? "LAYER" : "TABLE");
				error = $"{layerType} {template.GroupLayer}/{template.SubtypeLayer}: invalid {subtypeField} (subtype) value: {subtypeDesc}.";
			}
			else
			{
				List<string> fieldErrors = new List<string>();
				string subtypeValue = Convert.ToString(GetObjectValue(defaultFieldValues[subtypeField]), CultureInfo.InvariantCulture);
				DataSubtype subtype = null;
				await QueuedTask.Run((Action)delegate
				{
					subtype = subtypes.FirstOrDefault((DataSubtype n) => n.GetName() == subtypeValue);
				}, TaskCreationOptions.None);
				List<string> defaultFields = defaultFieldValues.Keys.Where((string n) => n.ToUpper() != subtypeField).ToList();
				foreach (string fieldName in defaultFields)
				{
					Field field = fields.FirstOrDefault((Field n) => string.Equals(n.Name, fieldName, StringComparison.OrdinalIgnoreCase));
					if (field == null)
					{
						fieldErrors.Add(fieldName.ToUpper() + ": field not found");
						continue;
					}
					DataDomain domain = null;
					await QueuedTask.Run((Action)delegate
					{
						domain = field.GetDomain(subtype);
					}, TaskCreationOptions.None);
					if (domain != null)
					{
						await CheckValueAgainstDomain(domain, template, fieldName, fieldErrors);
						continue;
					}
					await QueuedTask.Run((Action)delegate
					{
						domain = field.GetDomain((DataSubtype)null);
					}, TaskCreationOptions.None);
					if (domain != null)
					{
						await CheckValueAgainstDomain(domain, template, fieldName, fieldErrors);
					}
					else
					{
						CheckValueAgainstFieldType(template, field, fieldErrors);
					}
				}
				if (fieldErrors.Count > 0)
				{
					error = $"{template.GroupLayer}/{template.SubtypeLayer}: invalid field values ({string.Join(", ", fieldErrors)}) ({template.Name}).";
				}
			}
		}
		else
		{
			List<string> fieldErrors2 = new List<string>();
			Dictionary<string, object> defaultFieldValues = template.DefaultFieldValues ?? new Dictionary<string, object>();
			List<string> defaultFields2 = defaultFieldValues.Keys.ToList();
			foreach (string fieldName2 in defaultFields2)
			{
				Field field2 = fields.FirstOrDefault((Field n) => string.Equals(n.Name, fieldName2, StringComparison.OrdinalIgnoreCase));
				if (field2 == null)
				{
					fieldErrors2.Add(fieldName2.ToUpper() + ": field not found");
					continue;
				}
				DataDomain domain2 = null;
				await QueuedTask.Run((Action)delegate
				{
					domain2 = field2.GetDomain((DataSubtype)null);
				}, TaskCreationOptions.None);
				if (domain2 != null)
				{
					await CheckValueAgainstDomain(domain2, template, fieldName2, fieldErrors2);
				}
				else
				{
					CheckValueAgainstFieldType(template, field2, fieldErrors2);
				}
			}
			if (fieldErrors2.Count > 0)
			{
				error = $"{template.GroupLayer}/{template.SubtypeLayer}: invalid field values ({string.Join(", ", fieldErrors2)}) ({template.Name}).";
			}
		}
		return error;
	}

	private static async Task CheckValueAgainstDomain(DataDomain domain, SimpleTemplate template, string fieldName, List<string> fieldErrors)
	{
		if (domain is CodedValueDomain)
		{
			CodedValueDomain codedDomain = (CodedValueDomain)domain;
			List<string> domainDescs = null;
			await QueuedTask.Run((Action)delegate
			{
				domainDescs = codedDomain.GetCodedValuePairs().Values.Select((string n) => n.ToString()).ToList();
			}, TaskCreationOptions.None);
			string fieldValue = GetObjectValue(template.DefaultFieldValues[fieldName]).ToString();
			if (!domainDescs.Contains(fieldValue))
			{
				fieldErrors.Add(fieldName.ToUpper() + ": " + fieldValue);
			}
			return;
		}
		RangeDomain rangeDomain = (RangeDomain)domain;
		double minValue = 0.0;
		double maxValue = 0.0;
		await QueuedTask.Run((Action)delegate
		{
			minValue = Convert.ToDouble(rangeDomain.GetMinValue());
			maxValue = Convert.ToDouble(rangeDomain.GetMaxValue());
		}, TaskCreationOptions.None);
		double fieldValue2 = Convert.ToDouble(GetObjectValue(template.DefaultFieldValues[fieldName]));
		if (fieldValue2 < minValue || fieldValue2 > maxValue)
		{
			fieldErrors.Add($"{fieldName.ToUpper()}: {fieldValue2}");
		}
	}

	private static void CheckValueAgainstFieldType(SimpleTemplate template, Field field, List<string> fieldErrors)
	{
		string fieldValue = GetObjectValue(template.DefaultFieldValues[field.Name])?.ToString();
		bool isValid = true;
		if (field.FieldType == FieldType.BigInteger || field.FieldType == FieldType.Integer || field.FieldType == FieldType.SmallInteger)
		{
			isValid = int.TryParse(fieldValue, out var _);
		}
		else if (field.FieldType == FieldType.Single || field.FieldType == FieldType.Double)
		{
			isValid = double.TryParse(fieldValue, out var _);
		}
		if (!isValid)
		{
			fieldErrors.Add(field.Name.ToUpper() + ": " + fieldValue);
		}
	}

	private static async Task<string> ValidateGeometry(GroupTemplate groupTemplate, SimpleTemplateReference templateRef, SimpleTemplate template)
	{
		string error = null;
		if (template == null)
		{
			return $"Group template {groupTemplate?.Name} references missing simple template {templateRef?.Name}.";
		}
		if (AddinConfiguration.GroupFeatureLayerNames.Contains(template.GroupLayer.ToUpper()))
		{
			FeatureLayer layer = MapMemberLookupService.GetFeatureLayerByName(template.SubtypeLayer, template.GroupLayer);
			if (layer == null)
			{
				return $"Group layer/subtype layer {template.GroupLayer}/{template.SubtypeLayer} does not exist in the map ({template.Name}).";
			}
			GeometryType geometryType = (GeometryType)0;
			await QueuedTask.Run((Action)delegate
			{
				geometryType = layer.GetFeatureClass().GetDefinition().GetShapeType();
			}, TaskCreationOptions.None);
			if (templateRef.SketchType != null)
			{
				string sketchType = templateRef.SketchType.ToUpper();
				if (sketchType != "LINE" && sketchType != "POLYGON")
				{
					error = $"Group template {groupTemplate.Name} has invalid sketchType property for simple template {templateRef.Name}.";
				}
			}
			else if (GeometryTypeHelper.IsPoint(geometryType))
			{
				if (templateRef.Location == null)
				{
					error = $"Group template {groupTemplate.Name} has invalid Location property for simple template {templateRef.Name}.";
				}
			}
			else if (GeometryTypeHelper.IsPolyline(geometryType))
			{
				if (templateRef.Line == null)
				{
					error = $"Group template/simple template {groupTemplate.Name}/{templateRef.Name} has invalid Line property.";
				}
				else if (templateRef.Line.Count < 2)
				{
					error = $"Group template/simple template {groupTemplate.Name}/{templateRef.Name} has invalid number of points for the Line.";
				}
			}
			else if (GeometryTypeHelper.IsPolygon(geometryType))
			{
				if (templateRef.Polygon == null)
				{
					error = $"Group template {groupTemplate.Name} has invalid Polygon property for simple template {templateRef.Name}.";
				}
				else if (templateRef.Polygon.Count < 3)
				{
					error = $"Group template/simple template {groupTemplate.Name}/{templateRef.Name} has invalid number of points for the Polygon.";
				}
			}
		}
		return error;
	}

	private sealed class PlacementBuildResult
	{
		public EditOperation Operation { get; set; }

		public List<PlacedFeatureContext> CreatedFeatures { get; set; }

		public List<FeatureInfo> FeatureInfos { get; set; }

		public List<AssociationObject> ConfiguredAssociations { get; set; }

		public bool ApplyConfiguredAssociations { get; set; }
	}

	private sealed class ConfiguredAssociationResult
	{
		public static ConfiguredAssociationResult Empty => new ConfiguredAssociationResult();

		public int AttemptedCount { get; set; }

		public List<ExistingAssociationPair> CreatedPairs { get; } = new List<ExistingAssociationPair>();

		public List<string> Failures { get; } = new List<string>();

		public int CreatedCount => CreatedPairs.Count;

		public int FailedCount => Math.Max(Failures.Count, AttemptedCount - CreatedCount);

		public bool HasFailures => FailedCount > 0;
	}

	private sealed class PlacementOptions
	{
		public static PlacementOptions Full { get; } = new PlacementOptions
		{
			OperationName = "Create template features",
			IncludeConfiguredAssociations = true,
			IncludeDefaultAttributes = true
		};

		public static PlacementOptions WithoutAssociations { get; } = new PlacementOptions
		{
			OperationName = "Create template features without associations",
			IncludeConfiguredAssociations = false,
			IncludeDefaultAttributes = true
		};

		public static PlacementOptions MinimalAttributes { get; } = new PlacementOptions
		{
			OperationName = "Create template features with minimal attributes",
			IncludeConfiguredAssociations = false,
			IncludeDefaultAttributes = false
		};

		public string OperationName { get; private set; }

		public bool IncludeConfiguredAssociations { get; private set; }

		public bool IncludeDefaultAttributes { get; private set; }
	}

}
