using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.UtilityNetwork;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Editing.Attributes;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Domain = ArcGIS.Core.Data.Domain;
using Subtype = ArcGIS.Core.Data.Subtype;
using CodedValueDomain = ArcGIS.Core.Data.CodedValueDomain;
using RangeDomain = ArcGIS.Core.Data.RangeDomain;

namespace TemplateEditor;

internal static class CommonFunctions
{
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

	private static readonly Dictionary<string, bool> _simplePointTemplateCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

	private static readonly object _simplePointTemplateCacheLock = new object();

	private static CIMSymbolReference _cachedPreviewPointSymbol;

	private static CIMSymbolReference _cachedPreviewLineSymbol;

	private static CIMSymbolReference _cachedPreviewPolygonSymbol;

	internal static void ClearPreviewGeometryCache()
	{
		lock (_simplePointTemplateCacheLock)
		{
			_simplePointTemplateCache.Clear();
		}
	}

	private static TemplateConfig GetLoadedTemplateConfigOrThrow()
	{
		TemplateConfig templates = AddinConfiguration.Templates;
		if (templates == null)
		{
			throw new InvalidOperationException("Template configuration is not loaded.");
		}
		TemplateConfig templateConfig = templates;
		if (templateConfig.SimpleTemplates == null)
		{
			List<SimpleTemplate> list = (templateConfig.SimpleTemplates = new List<SimpleTemplate>());
		}
		templateConfig = templates;
		if (templateConfig.GroupTemplates == null)
		{
			List<GroupTemplate> list3 = (templateConfig.GroupTemplates = new List<GroupTemplate>());
		}
		return templates;
	}

	public static object GetObjectValue(object obj)
	{
		if (obj == null)
		{
			return null;
		}
		if (!(obj is JsonElement jsonElement) || 1 == 0)
		{
			return obj;
		}
		JsonValueKind valueKind = jsonElement.ValueKind;
		if (1 == 0)
		{
		}
		object result = valueKind switch
		{
			JsonValueKind.Number => jsonElement.TryGetInt64(out var value) ? ((double)value) : jsonElement.GetDouble(), 
			JsonValueKind.True => true, 
			JsonValueKind.False => false, 
			JsonValueKind.Null => null, 
			_ => jsonElement.ToString(), 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	public static async Task<GeometryType> GetTemplateGeometryTypeAsync(DisplayTemplate template)
	{
		TemplateConfig templates = GetLoadedTemplateConfigOrThrow();
		string templateName = template?.Name ?? AddinConfiguration.SelectedTemplate?.Name;
		if (string.IsNullOrWhiteSpace(templateName))
		{
			return (GeometryType)0;
		}
		if (template?.IsGroupChild ?? false)
		{
			SimpleTemplateReference childTemplateRef = GetGroupChildReference(template);
			SimpleTemplate childTemplate = templates.SimpleTemplates.FirstOrDefault((SimpleTemplate n) => string.Equals(n.Name, childTemplateRef?.Name, StringComparison.OrdinalIgnoreCase));
			if (childTemplateRef == null || childTemplate == null)
			{
				throw new InvalidOperationException("Template part '" + templateName + "' was not found.");
			}
			GeometryType configuredChildSketchType = GetConfiguredSketchGeometryType(childTemplateRef);
			if (childTemplateRef.SketchType != null)
			{
				return configuredChildSketchType;
			}
			if (HasConfiguredPlacementGeometry(childTemplateRef))
			{
				return (GeometryType)513;
			}
			return await GetSimpleTemplateGeometryTypeAsync(childTemplate);
		}
		SimpleTemplate simpleTemplate = templates.SimpleTemplates.FirstOrDefault((SimpleTemplate n) => string.Equals(n.Name, templateName, StringComparison.OrdinalIgnoreCase));
		if (simpleTemplate != null)
		{
			return await GetSimpleTemplateGeometryTypeAsync(simpleTemplate);
		}
		GroupTemplate groupTemplate = templates.GroupTemplates.FirstOrDefault((GroupTemplate n) => string.Equals(n.Name, templateName, StringComparison.OrdinalIgnoreCase));
		SimpleTemplateReference simpleTemplateRef = groupTemplate?.SimpleTemplates?.FirstOrDefault((SimpleTemplateReference n) => n.FeatureId == 1);
		if (simpleTemplateRef == null)
		{
			throw new InvalidOperationException("Template '" + templateName + "' does not have a sketch feature.");
		}
		GeometryType configuredSketchType = GetConfiguredSketchGeometryType(simpleTemplateRef);
		if (simpleTemplateRef.SketchType != null)
		{
			return configuredSketchType;
		}
		if (HasConfiguredPlacementGeometry(groupTemplate))
		{
			return (GeometryType)513;
		}
		SimpleTemplate referencedTemplate = templates.SimpleTemplates.FirstOrDefault((SimpleTemplate n) => string.Equals(n.Name, simpleTemplateRef.Name, StringComparison.OrdinalIgnoreCase));
		return (referencedTemplate != null) ? (await GetSimpleTemplateGeometryTypeAsync(referencedTemplate)) : configuredSketchType;
	}

	private static GeometryType GetConfiguredSketchGeometryType(SimpleTemplateReference simpleTemplateRef)
	{
		string text = simpleTemplateRef?.SketchType?.ToUpperInvariant();
		if (1 == 0)
		{
		}
		GeometryType result = ((text == "LINE") ? ((GeometryType)25607) : ((!(text == "POLYGON")) ? ((GeometryType)513) : ((GeometryType)27656)));
		if (1 == 0)
		{
		}
		return result;
	}

	private static SimpleTemplateReference GetGroupChildReference(DisplayTemplate childTemplate)
	{
		if (childTemplate == null || !childTemplate.IsGroupChild)
		{
			return null;
		}
		return (AddinConfiguration.Templates?.GroupTemplates?.FirstOrDefault((GroupTemplate group) => string.Equals(group.Name, childTemplate.ParentTemplateName, StringComparison.OrdinalIgnoreCase)))?.SimpleTemplates?.FirstOrDefault((SimpleTemplateReference templateRef) => templateRef.FeatureId == childTemplate.FeatureId && string.Equals(templateRef.Name, childTemplate.Name, StringComparison.OrdinalIgnoreCase));
	}

	private static bool HasConfiguredPlacementGeometry(GroupTemplate groupTemplate)
	{
		return groupTemplate != null && groupTemplate.SimpleTemplates?.Any((SimpleTemplateReference n) => n.Location != null || n.Line != null || n.Polygon != null) == true;
	}

	private static bool HasConfiguredPlacementGeometry(SimpleTemplateReference templateRef)
	{
		return templateRef?.Location != null || templateRef?.Line != null || templateRef?.Polygon != null;
	}

	private static async Task<GeometryType> GetSimpleTemplateGeometryTypeAsync(SimpleTemplate simpleTemplate)
	{
		if (simpleTemplate == null || !IsFeatureLayerTemplate(simpleTemplate))
		{
			return (GeometryType)0;
		}
		FeatureLayer layer = await MapMemberLookupService.GetFeatureLayerByNameAsync(simpleTemplate.SubtypeLayer, simpleTemplate.GroupLayer);
		if (layer == null)
		{
			throw new InvalidOperationException($"Layer '{simpleTemplate.GroupLayer}/{simpleTemplate.SubtypeLayer}' was not found for template '{simpleTemplate.Name}'.");
		}
		return await QueuedTask.Run<GeometryType>((Func<GeometryType>)delegate
		{
			return GetFeatureLayerShapeType(layer);
		}, TaskCreationOptions.None);
	}

	private static GeometryType GetFeatureLayerShapeType(FeatureLayer layer)
	{
		FeatureClass featureClass = layer?.GetFeatureClass();
		try
		{
			FeatureClassDefinition definition = featureClass?.GetDefinition();
			try
			{
				return (definition != null) ? definition.GetShapeType() : (GeometryType)0;
			}
			finally
			{
				((IDisposable)definition)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)featureClass)?.Dispose();
		}
	}

	public static async Task<bool> CreateFeatures(Geometry sketchGeometry, double rotationDegrees = 0.0, MapPoint splitPointOverride = null)
	{
		// Placement is staged: validate the request, build one edit operation,
		// execute it, then run associations and post-placement enhancements.
		if (sketchGeometry == null)
		{
			EditorDockpaneViewModel.SetPlacementStatus("Ready. Please select a template to place.");
			DialogService.Show("A placement geometry is required before placing features.", "Template Editor");
			return false;
		}
		TemplateConfig templates = GetLoadedTemplateConfigOrThrow();
		DisplayTemplate selectedTemplate = AddinConfiguration.SelectedTemplate;
		string templateName = selectedTemplate?.Name;
		if (string.IsNullOrWhiteSpace(templateName))
		{
			EditorDockpaneViewModel.SetPlacementStatus("Ready. Please select a template to place.");
			DialogService.Show("Choose a template before placing features.", "Template Editor");
			return false;
		}
		PlacementAttributeOverrideService.BeginPlacement();
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
			if (selectedTemplate?.IsGroupChild ?? false)
			{
				return await CreateGroupChildFeature(selectedTemplate, sketchGeometry, rotationDegrees, splitPointOverride);
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
			PlacementBuildResult placement = await BuildPlacementOperationAsync(templateName, sketchGeometry, isSimpleTemplate, PlacementOptions.Full, rotationDegrees, splitPointOverride);
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
				EditorDockpaneViewModel.PostPlacementSummary(BuildPlacementSummary(placement, associationResult), AppendPlacementWarnings(BuildPlacementSummaryDetails(associationResult)), associationResult.HasFailures || PlacementAttributeOverrideService.HasPlacementWarnings());
				return true;
			}
			EditorDockpaneViewModel.SetPlacementStatus("Placement failed. Choose a fallback option or cancel.");
			return await TryPlaceWithFallbacksAsync(templateName, sketchGeometry, isSimpleTemplate, errorMessage, rotationDegrees);
		}
		catch (OperationCanceledException)
		{
			EditorDockpaneViewModel.SetPlacementStatus("Ready. Please select a template to place.");
			return false;
		}
		catch (Exception ex2)
		{
			Exception ex3 = ex2;
			EditorDockpaneViewModel.SetPlacementStatus("Placement failed. See message for details.");
			LogService.LogException("CreateFeatures failed.", ex3);
			DialogService.Show("Template placement failed.\n\n" + ex3.Message + "\n\nDetails were written to the Template Editor log.", "Template Editor");
			return false;
		}
		finally
		{
			PlacementAttributeOverrideService.EndPlacementAttempt();
			EditorDockpaneViewModel.RefreshSettingsStatus();
		}
	}

	private static async Task<string> GetDefaultVersionPlacementBlockMessageAsync(DisplayTemplate selectedTemplate)
	{
		TemplateEditorSettings settings = AddinConfiguration.Settings;
		if (settings == null || !settings.PreventDefaultVersionPlacement || selectedTemplate == null)
		{
			return null;
		}
		List<string> defaultVersionTargets = await QueuedTask.Run<List<string>>((Func<List<string>>)(() => GetDefaultVersionPlacementTargets(selectedTemplate)), TaskCreationOptions.None);
		if (defaultVersionTargets.Count == 0)
		{
			return null;
		}
		string targets = string.Join("\n", from target in defaultVersionTargets.Distinct<string>(StringComparer.OrdinalIgnoreCase).OrderBy<string, string>((string target) => target, StringComparer.OrdinalIgnoreCase)
			select "- " + target);
		return "Template placement was blocked because one or more target feature service layers or tables are connected to DEFAULT.\n\nSwitch the map to a named version before placing templates.\n\nTarget(s):\n" + targets;
	}

	private static List<string> GetDefaultVersionPlacementTargets(DisplayTemplate selectedTemplate)
	{
		return (from mapMember in GetPlacementTargetMapMembers(selectedTemplate)
			where IsDefaultVersionConnection(mapMember)
			select mapMember.Name into name
			where !string.IsNullOrWhiteSpace(name)
			select name).ToList();
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
		if (selectedTemplate?.IsGroupChild ?? false)
		{
			SimpleTemplate childTemplate = TemplateCache.GetSimpleTemplate(GetGroupChildReference(selectedTemplate)?.Name);
			if (childTemplate != null)
			{
				yield return childTemplate;
			}
			yield break;
		}
		SimpleTemplate simpleTemplate = TemplateCache.GetSimpleTemplate(selectedTemplate?.Name);
		if (simpleTemplate != null)
		{
			yield return simpleTemplate;
			yield break;
		}
		IEnumerable<SimpleTemplateReference> enumerable = TemplateCache.GetGroupTemplate(selectedTemplate?.Name)?.SimpleTemplates;
		foreach (SimpleTemplateReference templateRef in enumerable ?? Enumerable.Empty<SimpleTemplateReference>())
		{
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
		catch (Exception exception)
		{
			LogService.LogException("Could not inspect connection version information for map member '" + mapMember.Name + "'.", exception);
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
		PropertyInfo[] properties = type.GetProperties();
		foreach (PropertyInfo property in properties)
		{
			if (!property.CanRead || property.GetIndexParameters().Length != 0)
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
				LogService.LogException(exception: ex, context: $"Could not inspect connection property '{property.Name}' on type '{type.FullName}'.");
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
			IEnumerable enumerable = propertyValue as IEnumerable;
			if (enumerable != null && !(propertyValue is string))
			{
				foreach (object item in enumerable)
				{
					foreach (string connectionVersionName in GetConnectionVersionNames(item, visited))
					{
						yield return connectionVersionName;
					}
				}
			}
			else
			{
				if (propertyValue == null || !(propertyValue.GetType().Namespace?.StartsWith("ArcGIS.Core.CIM", StringComparison.Ordinal) ?? false))
				{
					continue;
				}
				foreach (string connectionVersionName2 in GetConnectionVersionNames(propertyValue, visited))
				{
					yield return connectionVersionName2;
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
		string[] array = connectionString.Split(';');
		foreach (string text in array)
		{
			string[] array2 = text.Split(new char[1] { '=' }, 2);
			if (array2.Length == 2 && string.Equals(array2[0].Trim(), "VERSION", StringComparison.OrdinalIgnoreCase))
			{
				return array2[1].Trim();
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
		string text = versionName.Trim();
		return string.Equals(text, "DEFAULT", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "SDE.DEFAULT", StringComparison.OrdinalIgnoreCase) || text.EndsWith(".DEFAULT", StringComparison.OrdinalIgnoreCase);
	}

	private static async Task<bool> CreateGroupChildFeature(DisplayTemplate childTemplate, Geometry sketchGeometry, double rotationDegrees, MapPoint splitPointOverride = null)
	{
		GetLoadedTemplateConfigOrThrow();
		SimpleTemplateReference childTemplateRef = GetGroupChildReference(childTemplate);
		SimpleTemplate template = TemplateCache.GetSimpleTemplate(childTemplateRef?.Name);
		if (childTemplateRef == null || template == null)
		{
			throw new InvalidOperationException("Template part '" + childTemplate?.DisplayName + "' was not found.");
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
		else if (!IsFeatureLayerTemplate(template))
		{
			await CreateTableRowWithAutoAssociationAsync(template, featureGeometry, operation, PlacementOptions.Full, childTemplate.ParentTemplateName, childTemplate.FeatureId);
		}
		else
		{
			TryTrackPlacedFeature(createdFeatures, template, featureGeometry, await CreateFeatureOrRowFromSimpleTemplate(template, featureGeometry, operation, includeDefaultAttributes: true, rotationDegrees, childTemplate.ParentTemplateName, childTemplate.FeatureId), IsPlacementEnhancementCandidate(childTemplateRef), splitPointOverride);
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
		EditorDockpaneViewModel.PostPlacementSummary("Created template part '" + childTemplate.DisplayName + "'.", AppendPlacementWarnings(null), PlacementAttributeOverrideService.HasPlacementWarnings());
		return true;
	}

	private static async Task<bool> TryPlaceWithFallbacksAsync(string templateName, Geometry sketchGeometry, bool isSimpleTemplate, string originalErrorMessage, double rotationDegrees)
	{
		// Fallbacks let the user keep the core feature placement when optional
		// configured associations or defaults are what caused ArcGIS to reject it.
		if (DialogService.Show("Template placement failed.\n\n" + CleanErrorMessage(originalErrorMessage) + "\n\nYou can retry without configured associations, or cancel placement.", "Template Editor", new DialogButtonChoice("Place Without Associations", MessageBoxResult.Yes, isPrimary: true), new DialogButtonChoice("Cancel", MessageBoxResult.No, isPrimary: false, isCancel: true)) == MessageBoxResult.Yes)
		{
			EditorDockpaneViewModel.SetPlacementStatus("Retrying " + templateName + ": placing without configured associations...");
			PlacementBuildResult placementWithoutAssociations = await BuildPlacementOperationAsync(templateName, sketchGeometry, isSimpleTemplate, PlacementOptions.WithoutAssociations, rotationDegrees);
			string associationFallbackError = await ExecutePlacementOperationAsync(placementWithoutAssociations.Operation);
			if (associationFallbackError == null)
			{
				await PopulateFeatureInfoDetailsAsync(placementWithoutAssociations.FeatureInfos);
				await FinalizePlacementAsync(placementWithoutAssociations.CreatedFeatures, applyPostPlacementEnhancements: false);
				EditorDockpaneViewModel.PostPlacementSummary(BuildPlacementSummary(placementWithoutAssociations), AppendPlacementWarnings("Template was placed without configured associations."), warning: true);
				return true;
			}
			originalErrorMessage = associationFallbackError;
		}
		if (DialogService.Show("Template placement still failed.\n\n" + CleanErrorMessage(originalErrorMessage) + "\n\nYou can retry with only subtype and required attributes, or cancel placement.", "Template Editor", new DialogButtonChoice("Place Required Only", MessageBoxResult.Yes, isPrimary: true), new DialogButtonChoice("Cancel", MessageBoxResult.No, isPrimary: false, isCancel: true)) == MessageBoxResult.Yes)
		{
			EditorDockpaneViewModel.SetPlacementStatus("Retrying " + templateName + ": placing with only subtype and required attributes...");
			PlacementBuildResult minimalPlacement = await BuildPlacementOperationAsync(templateName, sketchGeometry, isSimpleTemplate, PlacementOptions.MinimalAttributes, rotationDegrees);
			string minimalError = await ExecutePlacementOperationAsync(minimalPlacement.Operation);
			if (minimalError == null)
			{
				await PopulateFeatureInfoDetailsAsync(minimalPlacement.FeatureInfos);
				await FinalizePlacementAsync(minimalPlacement.CreatedFeatures, applyPostPlacementEnhancements: false);
				EditorDockpaneViewModel.PostPlacementSummary(BuildPlacementSummary(minimalPlacement), AppendPlacementWarnings("Template was placed with only subtype/required attributes and without configured associations."), warning: true);
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
		int valueOrDefault = (placement?.CreatedFeatures?.Count).GetValueOrDefault();
		int valueOrDefault2 = (placement?.FeatureInfos?.Count).GetValueOrDefault();
		int num = associationResult?.CreatedCount ?? (placement?.ConfiguredAssociations?.Count).GetValueOrDefault();
		List<string> list = new List<string>();
		if (valueOrDefault > 0)
		{
			list.Add($"{valueOrDefault} feature(s)");
		}
		if (valueOrDefault2 > valueOrDefault)
		{
			list.Add($"{valueOrDefault2 - valueOrDefault} non-spatial row(s)");
		}
		if (num > 0 && placement.ApplyConfiguredAssociations)
		{
			list.Add($"{num} configured association(s)");
		}
		return (list.Count == 0) ? "Placement completed." : ("Created " + string.Join(", ", list) + ".");
	}

	private static string BuildPlacementSummaryDetails(ConfiguredAssociationResult associationResult)
	{
		if (associationResult == null || !associationResult.HasFailures)
		{
			return null;
		}
		return $"{associationResult.FailedCount} configured association(s) could not be created. Review the diagnostics and verify the placed template before continuing.";
	}

	private static string AppendPlacementWarnings(string details)
	{
		string text = PlacementAttributeOverrideService.ConsumePlacementWarnings();
		if (string.IsNullOrWhiteSpace(text))
		{
			return details;
		}
		return string.IsNullOrWhiteSpace(details) ? text : (details + "\n" + text);
	}

	private static async Task<PlacementBuildResult> BuildPlacementOperationAsync(string templateName, Geometry sketchGeometry, bool isSimpleTemplate, PlacementOptions options, double rotationDegrees, MapPoint splitPointOverride = null)
	{
		// Build first, execute later. The returned feature info tokens are resolved
		// after execution and then used to create utility-network associations.
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
		if (!isSimpleTemplate)
		{
			await BuildGroupPlacementOperationAsync(templateName, sketchGeometry, operation, createdFeatures, featureInfos, configuredAssociations, options, rotationDegrees, splitPointOverride);
		}
		else
		{
			SimpleTemplate template = templates.SimpleTemplates.FirstOrDefault((SimpleTemplate n) => string.Equals(n.Name, templateName, StringComparison.OrdinalIgnoreCase));
			if (template == null)
			{
				throw new InvalidOperationException("Simple template '" + templateName + "' was not found.");
			}
			if (IsStructureJunctionObjectTemplate(template))
			{
				await CreateSJOAttachmentsForPoles(template, sketchGeometry, operation, options);
			}
			else if (!IsFeatureLayerTemplate(template))
			{
				await CreateTableRowWithAutoAssociationAsync(template, sketchGeometry, operation, options);
			}
			else
			{
				TryTrackPlacedFeature(createdFeatures, template, sketchGeometry, await CreateFeatureOrRowFromSimpleTemplate(template, sketchGeometry, operation, options.IncludeDefaultAttributes, rotationDegrees), allowPlacementEnhancements: true, splitPointOverride);
			}
		}
		return new PlacementBuildResult
		{
			Operation = operation,
			CreatedFeatures = createdFeatures,
			FeatureInfos = featureInfos,
			ConfiguredAssociations = configuredAssociations,
			ApplyConfiguredAssociations = (!isSimpleTemplate && options.IncludeConfiguredAssociations)
		};
	}

	private static async Task BuildGroupPlacementOperationAsync(string templateName, Geometry sketchGeometry, EditOperation operation, List<PlacedFeatureContext> createdFeatures, List<FeatureInfo> featureTokens, List<AssociationObject> configuredAssociations, PlacementOptions options, double rotationDegrees, MapPoint splitPointOverride)
	{
		TemplateConfig templates = GetLoadedTemplateConfigOrThrow();
		GroupTemplate groupTemplate = templates.GroupTemplates.FirstOrDefault((GroupTemplate n) => string.Equals(n.Name, templateName, StringComparison.OrdinalIgnoreCase));
		if (groupTemplate == null)
		{
			throw new InvalidOperationException("Group template '" + templateName + "' was not found.");
		}
		foreach (SimpleTemplateReference simpleTemplateRef in groupTemplate.SimpleTemplates)
		{
			// FeatureId is the bridge between each generated row/feature and the
			// association definitions in the group template JSON.
			SimpleTemplate template = AddinConfiguration.Templates.SimpleTemplates.FirstOrDefault((SimpleTemplate n) => string.Equals(n.Name, simpleTemplateRef.Name, StringComparison.OrdinalIgnoreCase));
			if (template == null)
			{
				throw new InvalidOperationException($"Group template '{groupTemplate.Name}' references missing simple template '{simpleTemplateRef.Name}'.");
			}
			Geometry featureGeometry = CreateGeometryForTemplate(simpleTemplateRef, sketchGeometry, rotationDegrees);
			RowToken token = await CreateFeatureOrRowFromSimpleTemplate(template, featureGeometry, operation, options.IncludeDefaultAttributes, rotationDegrees, groupTemplate.Name, simpleTemplateRef.FeatureId);
			TryTrackPlacedFeature(createdFeatures, template, featureGeometry, token, IsPlacementEnhancementCandidate(simpleTemplateRef), splitPointOverride);
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
	}

	private static async Task<string> ExecutePlacementOperationAsync(EditOperation operation)
	{
		return (await QueuedTask.Run<bool>((Func<bool>)(() => operation.Execute()), TaskCreationOptions.None) && operation.IsSucceeded) ? null : operation.ErrorMessage;
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
		if (fromInfo == null || toInfo == null)
		{
			failure = $"Feature {association.FromFeatureId} -> {association.ToFeatureId}: missing feature id.";
			return false;
		}
		AssociationType? associationType = GetAssociationType(association.Type);
		if (!associationType.HasValue)
		{
			failure = FormatAssociationLabel(association, fromInfo, toInfo) + ": Unsupported association type '" + association.Type + "'.";
			return false;
		}
		if (fromInfo.MapMember == null || toInfo.MapMember == null || fromInfo.ObjectID <= 0 || toInfo.ObjectID <= 0)
		{
			failure = FormatAssociationLabel(association, fromInfo, toInfo) + ": missing created feature or row identity.";
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
		string text = associationType.ToUpperInvariant();
		if (1 == 0)
		{
		}
		AssociationType? result = text switch
		{
			"CONTAINMENT" => (AssociationType)2, 
			"ATTACHMENT" => (AssociationType)3, 
			"JUNCTIONJUNCTIONCONNECTIVITY" => (AssociationType)1, 
			"JUNCTIONEDGEOBJECTCONNECTIVITYFROMSIDE" => (AssociationType)4, 
			"JUNCTIONEDGEOBJECTCONNECTIVITYTOSIDE" => (AssociationType)6, 
			"JUNCTIONEDGEOBJECTCONNECTIVITYMIDSPAN" => (AssociationType)5, 
			_ => null, 
		};
		if (1 == 0)
		{
		}
		return result;
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
			return (MapMember)(object)MapMemberLookupService.GetFeatureLayerByName(template.SubtypeLayer, template.GroupLayer);
		}
		return (MapMember)(object)MapMemberLookupService.GetTableByName(template.SubtypeLayer, template.GroupLayer);
	}

	private static async Task<ConfiguredAssociationResult> ExecuteConfiguredAssociationsAsync(PlacementBuildResult placement)
	{
		ConfiguredAssociationResult result = new ConfiguredAssociationResult
		{
			AttemptedCount = (placement?.ConfiguredAssociations?.Count).GetValueOrDefault()
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
		// Fast mode batches valid associations into one edit operation. If the batch
		// fails, Debug mode can isolate the individual association that failed.
		List<ExistingAssociationPair> queuedPairs = new List<ExistingAssociationPair>();
		Dictionary<int, FeatureInfo> featureInfoById = new Dictionary<int, FeatureInfo>(placement.FeatureInfos.Count);
		foreach (FeatureInfo info in placement.FeatureInfos)
		{
			if (!featureInfoById.ContainsKey(info.FeatureId))
			{
				featureInfoById[info.FeatureId] = info;
			}
		}
		string errorMessage = await QueuedTask.Run<string>((Func<string>)delegate
		{
			EditOperation val = new EditOperation
			{
				Name = "Create template associations",
				ProgressMessage = "Creating template associations...",
				ShowProgressor = true
			};
			foreach (AssociationObject configuredAssociation in placement.ConfiguredAssociations)
			{
				featureInfoById.TryGetValue(configuredAssociation.FromFeatureId, out var value);
				featureInfoById.TryGetValue(configuredAssociation.ToFeatureId, out var value2);
				if (!TryBuildConfiguredAssociationPair(configuredAssociation, value, value2, out var pair, out var failure))
				{
					result.Failures.Add(failure);
				}
				else
				{
					AssociationDescription val2 = CreateAssociationDescription(configuredAssociation, value, value2);
					val.Create(val2);
					queuedPairs.Add(pair);
				}
			}
			return val.IsEmpty ? null : ((val.Execute() && val.IsSucceeded) ? null : val.ErrorMessage);
		}, TaskCreationOptions.None);
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
			string additionalFailureText = ((result.Failures.Count > 8) ? $"\n\n{result.Failures.Count - 8} more association failure(s) were not shown." : string.Empty);
			DialogService.Show("Template was placed, but one or more configured associations could not be created in Fast mode.\n\nSwitch Configured association mode to Debug for exact per-association diagnostics.\n\nIssue(s):\n" + displayedFailures + additionalFailureText, "Template Editor - Association Diagnostics");
		}
	}

	private static async Task ExecuteConfiguredAssociationsWithDiagnosticsAsync(PlacementBuildResult placement, ConfiguredAssociationResult result)
	{
		// Debug mode runs associations one at a time so diagnostics can name the
		// exact from/to FeatureId pair ArcGIS rejected.
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
			featureInfoById.TryGetValue(association.FromFeatureId, out var fromInfo);
			featureInfoById.TryGetValue(association.ToFeatureId, out var toInfo);
			if (!TryBuildConfiguredAssociationPair(association, fromInfo, toInfo, out var pair, out var failure))
			{
				result.Failures.Add(failure);
				continue;
			}
			string errorMessage = await ExecuteSingleConfiguredAssociationAsync(association, fromInfo, toInfo);
			if (errorMessage != null)
			{
				result.Failures.Add(FormatAssociationLabel(association, fromInfo, toInfo) + ": " + CleanErrorMessage(errorMessage));
			}
			else
			{
				result.CreatedPairs.Add(pair);
			}
			fromInfo = null;
			toInfo = null;
			pair = null;
			failure = null;
		}
		if (result.Failures.Count > 0)
		{
			string displayedFailures = string.Join("\n", result.Failures.Take(8));
			string additionalFailureText = ((result.Failures.Count > 8) ? $"\n\n{result.Failures.Count - 8} more association failure(s) were not shown." : string.Empty);
			DialogService.Show($"Template was placed, but it is incomplete.\n\n{result.Failures.Count} configured association(s) could not be created. Inspect the newly placed features and verify their associations before continuing.\n\nFailed association(s):\n{displayedFailures}{additionalFailureText}", "Template Editor - Association Diagnostics");
		}
	}

	private static async Task<string> ExecuteSingleConfiguredAssociationAsync(AssociationObject association, FeatureInfo fromInfo, FeatureInfo toInfo)
	{
		return await QueuedTask.Run<string>((Func<string>)delegate
		{
			AssociationDescription val = CreateAssociationDescription(association, fromInfo, toInfo);
			if (val == null)
			{
				return "Unsupported association type '" + association.Type + "'.";
			}
			EditOperation val2 = new EditOperation
			{
				Name = "Create template association",
				ProgressMessage = "Creating template association...",
				ShowProgressor = true
			};
			val2.Create(val);
			return (val2.Execute() && val2.IsSucceeded) ? null : val2.ErrorMessage;
		}, TaskCreationOptions.None);
	}

	private static string FormatAssociationLabel(AssociationObject association, FeatureInfo fromInfo, FeatureInfo toInfo)
	{
		return $"{association.Type} {fromInfo.FeatureId} ({fromInfo.Template?.Name}) -> {toInfo.FeatureId} ({toInfo.Template?.Name})";
	}

	private static string CleanErrorMessage(string errorMessage)
	{
		return string.IsNullOrWhiteSpace(errorMessage) ? "No error details were returned by ArcGIS Pro." : errorMessage;
	}

	private static void TryTrackPlacedFeature(List<PlacedFeatureContext> createdFeatures, SimpleTemplate template, Geometry geometry, RowToken token, bool allowPlacementEnhancements = true, MapPoint splitPointOverride = null)
	{
		if (createdFeatures != null && template != null && geometry != null && token != null && !string.IsNullOrWhiteSpace(template.GroupLayer) && AddinConfiguration.GroupFeatureLayerNames.Contains(template.GroupLayer.ToUpperInvariant()))
		{
			createdFeatures.Add(new PlacedFeatureContext
			{
				Template = template,
				Geometry = geometry,
				Token = token,
				AllowPlacementEnhancements = allowPlacementEnhancements,
				SplitPointOverride = splitPointOverride
			});
		}
	}

	private static bool IsPlacementEnhancementCandidate(SimpleTemplateReference templateRef)
	{
		if (templateRef == null)
		{
			return true;
		}
		if (templateRef.Line != null)
		{
			TemplateEditorSettings settings = AddinConfiguration.Settings;
			if (settings == null || !settings.EnableConfiguredLinePartSplits)
			{
				return false;
			}
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
		Dictionary<string, object> dictionary = template.DefaultFieldValues ?? new Dictionary<string, object>();
		string a = (dictionary.TryGetValue("ASSETGROUP", out var value) ? Convert.ToString(GetObjectValue(value), CultureInfo.InvariantCulture) : null);
		string a2 = (dictionary.TryGetValue("ASSETTYPE", out var value2) ? Convert.ToString(GetObjectValue(value2), CultureInfo.InvariantCulture) : null);
		return IsStructureJunctionObjectName(template.GroupLayer) || IsStructureJunctionObjectName(template.SubtypeLayer) || IsStructureJunctionObjectName(template.Name) || (string.Equals(a, "Framing", StringComparison.OrdinalIgnoreCase) && string.Equals(a2, "Framing", StringComparison.OrdinalIgnoreCase)) || (string.Equals(a, "Pole Link", StringComparison.OrdinalIgnoreCase) && string.Equals(a2, "Pole Link", StringComparison.OrdinalIgnoreCase));
	}

	private static bool IsStructureJunctionObjectName(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return false;
		}
		string text = value.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
		return text.IndexOf("StructureJunctionObject", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("SJO", StringComparison.OrdinalIgnoreCase) >= 0;
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
		await QueuedTask.Run((Action)delegate
		{
			foreach (PlacedFeatureContext createdFeature2 in createdFeatures)
			{
				if (createdFeature2.Layer != null && createdFeature2.ObjectID > 0)
				{
					QueryFilter val = new QueryFilter
					{
						ObjectIDs = new List<long> { createdFeature2.ObjectID }
					};
					RowCursor val2 = ((BasicFeatureLayer)createdFeature2.Layer).Search(val, (TimeRange)null, (RangeExtent)null, (CIMFloorFilterSettings)null);
					try
					{
						if (val2.MoveNext())
						{
							Feature val3 = (Feature)val2.Current;
							try
							{
								createdFeature2.Geometry = val3.GetShape();
							}
							finally
							{
								((IDisposable)val3)?.Dispose();
							}
						}
					}
					finally
					{
						((IDisposable)val2)?.Dispose();
					}
				}
			}
		}, TaskCreationOptions.None);
	}

	private static async Task CreateTableRowWithAutoAssociationAsync(SimpleTemplate template, Geometry sketchGeometry, EditOperation operation, PlacementOptions options, string parentTemplateName = null, int featureId = 0)
	{
		if (!options.IncludeConfiguredAssociations)
		{
			if (!ConfirmCreateNonSpatialWithoutAssociations(template))
			{
				throw new OperationCanceledException();
			}
			await CreateFeatureOrRowFromSimpleTemplate(template, sketchGeometry, operation, options.IncludeDefaultAttributes, 0.0, parentTemplateName, featureId);
			return;
		}
		List<(FeatureLayer Layer, long ObjectID, string Label, string OwningGroup)> selectedCandidates = await GetSelectedFeaturesForTableAssociationAsync();
		if (selectedCandidates.Count == 0)
		{
			if (!ConfirmCreateNonSpatialWithoutAssociations(template))
			{
				throw new OperationCanceledException();
			}
			await CreateFeatureOrRowFromSimpleTemplate(template, sketchGeometry, operation, options.IncludeDefaultAttributes, 0.0, parentTemplateName, featureId);
			return;
		}
		List<((FeatureLayer Layer, long ObjectID, string Label, string OwningGroup) Candidate, List<(AssociationObject Rule, bool IsReversed)> Rules)> candidatesWithRules = (from c in selectedCandidates
			select (Candidate: c, Rules: FindGroupTemplateAssociations(template, ((MapMember)c.Layer).Name, c.OwningGroup)) into c
			where c.Rules.Count > 0
			select c).ToList();
		if (candidatesWithRules.Count == 0)
		{
			if (!ConfirmCreateNonSpatialWithoutAssociations(template))
			{
				throw new OperationCanceledException();
			}
			await CreateFeatureOrRowFromSimpleTemplate(template, sketchGeometry, operation, options.IncludeDefaultAttributes, 0.0, parentTemplateName, featureId);
			return;
		}
		string associationSummary = ((candidatesWithRules.Count == 1) ? (candidatesWithRules[0].Candidate.Label + ":\n" + BuildTableAssociationSummary(candidatesWithRules[0].Rules)) : string.Join("\n", candidatesWithRules.Select((((FeatureLayer Layer, long ObjectID, string Label, string OwningGroup) Candidate, List<(AssociationObject Rule, bool IsReversed)> Rules) pair) => pair.Candidate.Label + ":\n" + BuildTableAssociationSummary(pair.Rules))));
		MessageBoxResult result = DialogService.Show("Create the following associations?\n\n" + associationSummary, "Template Editor", new DialogButtonChoice("Create Associations", MessageBoxResult.Yes, isPrimary: true), new DialogButtonChoice("Skip Associations", MessageBoxResult.No, isPrimary: false, isCancel: true));
		if (result != MessageBoxResult.Yes && !ConfirmCreateNonSpatialWithoutAssociations(template))
		{
			throw new OperationCanceledException();
		}
		RowToken rowToken = await CreateFeatureOrRowFromSimpleTemplate(template, sketchGeometry, operation, options.IncludeDefaultAttributes, 0.0, parentTemplateName, featureId);
		if (result != MessageBoxResult.Yes)
		{
			return;
		}
		RowHandle rowHandle = new RowHandle(rowToken);
		foreach (var item in candidatesWithRules)
		{
			(FeatureLayer Layer, long ObjectID, string Label, string OwningGroup) candidate = item.Candidate;
			List<(AssociationObject Rule, bool IsReversed)> rules = item.Rules;
			RowHandle selectedHandle = new RowHandle((MapMember)candidate.Layer, candidate.ObjectID);
			foreach (var item2 in rules)
			{
				AssociationObject rule = item2.Rule;
				bool isReversed = item2.IsReversed;
				AssociationDescription assocDesc = CreateTableAssociationDescription(rule, isReversed, selectedHandle, rowHandle);
				if (assocDesc != null)
				{
					operation.Create(assocDesc);
				}
			}
		}
	}

	private static bool ConfirmCreateNonSpatialWithoutAssociations(SimpleTemplate template)
	{
		MessageBoxResult messageBoxResult = DialogService.Show("Create non-spatial record '" + template?.Name + "' without any associations?", "Template Editor", new DialogButtonChoice("Create Without Associations", MessageBoxResult.Yes, isPrimary: true), new DialogButtonChoice("Cancel", MessageBoxResult.No, isPrimary: false, isCancel: true));
		return messageBoxResult == MessageBoxResult.Yes;
	}

	private static bool ConfirmCreateNonSpatialTemplate(SimpleTemplate template)
	{
		MessageBoxResult messageBoxResult = DialogService.Show("Template '" + template?.Name + "' creates a non-spatial record.\n\nContinue?", "Template Editor", new DialogButtonChoice("Continue", MessageBoxResult.Yes, isPrimary: true), new DialogButtonChoice("Cancel", MessageBoxResult.No, isPrimary: false, isCancel: true));
		return messageBoxResult == MessageBoxResult.Yes;
	}

	private static List<(AssociationObject Rule, bool IsReversed)> FindGroupTemplateAssociations(SimpleTemplate tableTemplate, string selectedLayerName, string selectedOwningGroup)
	{
		IEnumerable<GroupTemplate> groupTemplates = AddinConfiguration.Templates.GroupTemplates;
		foreach (GroupTemplate item in groupTemplates ?? Enumerable.Empty<GroupTemplate>())
		{
			SimpleTemplateReference simpleTemplateReference = item.SimpleTemplates?.FirstOrDefault((SimpleTemplateReference r) => string.Equals(r.Name, tableTemplate.Name, StringComparison.OrdinalIgnoreCase));
			if (simpleTemplateReference == null)
			{
				continue;
			}
			List<(AssociationObject Rule, bool IsReversed, int SpatialFeatureId)> list = new List<(AssociationObject Rule, bool IsReversed, int SpatialFeatureId)>();
			IEnumerable<AssociationObject> associations = item.Associations;
			foreach (AssociationObject item2 in associations ?? Enumerable.Empty<AssociationObject>())
			{
				if (item2.ToFeatureId == simpleTemplateReference.FeatureId)
				{
					list.Add((item2, false, item2.FromFeatureId));
				}
				else if (item2.FromFeatureId == simpleTemplateReference.FeatureId)
				{
					list.Add((item2, true, item2.ToFeatureId));
				}
			}
			if (list.Count == 0)
			{
				continue;
			}
			foreach (int spatialFeatureId in list.Select<(AssociationObject, bool, int), int>(((AssociationObject Rule, bool IsReversed, int SpatialFeatureId) a) => a.SpatialFeatureId).Distinct())
			{
				SimpleTemplateReference spatialRef = item.SimpleTemplates?.FirstOrDefault((SimpleTemplateReference r) => r.FeatureId == spatialFeatureId);
				if (spatialRef == null)
				{
					continue;
				}
				SimpleTemplate simpleTemplate = AddinConfiguration.Templates.SimpleTemplates?.FirstOrDefault((SimpleTemplate t) => string.Equals(t.Name, spatialRef.Name, StringComparison.OrdinalIgnoreCase));
				if (simpleTemplate == null || !IsFeatureLayerTemplate(simpleTemplate))
				{
					continue;
				}
				bool flag = string.Equals(selectedOwningGroup, simpleTemplate.GroupLayer, StringComparison.OrdinalIgnoreCase);
				bool flag2 = ((simpleTemplate.SubtypeLayer != null) ? string.Equals(selectedLayerName, simpleTemplate.SubtypeLayer, StringComparison.OrdinalIgnoreCase) : string.Equals(selectedLayerName, simpleTemplate.GroupLayer, StringComparison.OrdinalIgnoreCase));
				if (flag && flag2)
				{
					List<(AssociationObject, bool)> list2 = (from a in list
						where a.SpatialFeatureId == spatialFeatureId
						select (Rule: a.Rule, IsReversed: a.IsReversed)).ToList();
					if (list2.Count > 0)
					{
						return list2;
					}
				}
			}
		}
		return new List<(AssociationObject, bool)>();
	}

	private static AssociationDescription CreateTableAssociationDescription(AssociationObject assoc, bool isReversed, RowHandle selectedHandle, RowHandle rowHandle)
	{
		RowHandle val = (isReversed ? rowHandle : selectedHandle);
		RowHandle val2 = (isReversed ? selectedHandle : rowHandle);
		int num = (isReversed ? assoc.ToTerminal : assoc.FromTerminal);
		string text = assoc.Type?.ToUpperInvariant();
		if (1 == 0)
		{
		}
		AssociationDescription result = (AssociationDescription)(text switch
		{
			"CONTAINMENT" => (object)new AssociationDescription((AssociationType)2, val, val2, isReversed), 
			"ATTACHMENT" => (object)new AssociationDescription((AssociationType)3, val, val2), 
			"JUNCTIONJUNCTIONCONNECTIVITY" => (object)((num > 0) ? new AssociationDescription((AssociationType)1, val, (long)num, val2) : new AssociationDescription((AssociationType)1, val, val2)), 
			_ => null, 
		});
		if (1 == 0)
		{
		}
		return result;
	}

	private static string BuildTableAssociationSummary(IEnumerable<(AssociationObject Rule, bool IsReversed)> rules)
	{
		IEnumerable<string> values = rules.Select(delegate((AssociationObject Rule, bool IsReversed) r)
		{
			string text = r.Rule.Type?.ToUpperInvariant();
			if (1 == 0)
			{
			}
			string result = text switch
			{
				"CONTAINMENT" => "  • Containment", 
				"ATTACHMENT" => "  • Structural Attachment", 
				"JUNCTIONJUNCTIONCONNECTIVITY" => ((r.IsReversed ? r.Rule.ToTerminal : r.Rule.FromTerminal) > 0) ? $"  • JJC (terminal {(r.IsReversed ? r.Rule.ToTerminal : r.Rule.FromTerminal)})" : "  • JJC", 
				_ => "  • " + r.Rule.Type, 
			};
			if (1 == 0)
			{
			}
			return result;
		});
		return string.Join("\n", values);
	}

	private static async Task<List<(FeatureLayer Layer, long ObjectID, string Label, string OwningGroup)>> GetSelectedFeaturesForTableAssociationAsync()
	{
		return await QueuedTask.Run<List<(FeatureLayer, long, string, string)>>((Func<List<(FeatureLayer, long, string, string)>>)delegate
		{
			List<(FeatureLayer, long, string, string)> list = new List<(FeatureLayer, long, string, string)>();
			if (MapView.Active == null)
			{
				return list;
			}
			IEnumerable<string> groupFeatureLayerNames = AddinConfiguration.GroupFeatureLayerNames;
			HashSet<string> hashSet = new HashSet<string>(groupFeatureLayerNames ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
			foreach (FeatureLayer item in MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>())
			{
				string owningGroupName = MapMemberLookupService.GetOwningGroupName(item);
				if (hashSet.Contains(((MapMember)item).Name.ToUpperInvariant()) || (!string.IsNullOrWhiteSpace(owningGroupName) && hashSet.Contains(owningGroupName.ToUpperInvariant())))
				{
					string value = ((string.IsNullOrWhiteSpace(owningGroupName) || owningGroupName.Equals(((MapMember)item).Name, StringComparison.OrdinalIgnoreCase)) ? ((MapMember)item).Name : (owningGroupName + "/" + ((MapMember)item).Name));
					foreach (long objectID in ((BasicFeatureLayer)item).GetSelection().GetObjectIDs())
					{
						list.Add((item, objectID, $"{value} (OID {objectID})", owningGroupName));
					}
				}
			}
			return list;
		}, TaskCreationOptions.None);
	}

	private static async Task CreateSJOAttachmentsForPoles(SimpleTemplate template, Geometry sketchGeometry, EditOperation operation, PlacementOptions options)
	{
		if (options.IncludeConfiguredAssociations)
		{
			List<(FeatureLayer Layer, long ObjectID, string Label, string OwningGroup)> polesToProcess = await GetSelectedPoleCandidatesForSjoAsync();
			if (polesToProcess.Count > 0)
			{
				MessageBoxResult result = DialogService.Show($"The SJO can be created as attachments for {polesToProcess.Count} selected Pole(s).", "Template Editor", new DialogButtonChoice("Create Attachments", MessageBoxResult.Yes, isPrimary: true), new DialogButtonChoice("Create SJO Only", MessageBoxResult.No, isPrimary: false, isCancel: true));
				if (result == MessageBoxResult.Yes)
				{
					foreach (var item in polesToProcess)
					{
						FeatureLayer layer = item.Layer;
						long objectId = item.ObjectID;
						RowHandle poleHandle = new RowHandle((MapMember)layer, objectId);
						RowHandle sjoHandle = new RowHandle(await CreateFeatureOrRowFromSimpleTemplate(template, sketchGeometry, operation, options.IncludeDefaultAttributes));
						AssociationDescription assocDesc = new AssociationDescription((AssociationType)3, poleHandle, sjoHandle);
						operation.Create(assocDesc);
					}
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
		foreach (var item in await GetSelectedFeatureCandidatesAsync())
		{
			FeatureLayer layer = item.Item1;
			long objectId2 = item.Item2;
			string label = item.Item3;
			string owningGroup = item.Item4;
			bool flag = IsStructureJunctionPoleLayer(layer, owningGroup);
			bool flag2 = flag;
			if (!flag2)
			{
				flag2 = await IsSupportedPoleAssetTypeAsync(layer, objectId2);
			}
			if (flag2)
			{
				poles.Add((layer, objectId2, label, owningGroup));
			}
		}
		return poles;
	}

	private static async Task<List<(FeatureLayer Layer, long ObjectID, string Label, string OwningGroup)>> GetSelectedFeatureCandidatesAsync()
	{
		return await QueuedTask.Run<List<(FeatureLayer, long, string, string)>>((Func<List<(FeatureLayer, long, string, string)>>)delegate
		{
			List<(FeatureLayer, long, string, string)> list = new List<(FeatureLayer, long, string, string)>();
			if (MapView.Active == null)
			{
				return list;
			}
			foreach (FeatureLayer item in MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>())
			{
				List<long> list2 = ((BasicFeatureLayer)item).GetSelection().GetObjectIDs().ToList();
				if (list2.Count != 0)
				{
					string owningGroupName = MapMemberLookupService.GetOwningGroupName(item);
					string value = ((string.IsNullOrWhiteSpace(owningGroupName) || owningGroupName.Equals(((MapMember)item).Name, StringComparison.OrdinalIgnoreCase)) ? ((MapMember)item).Name : (owningGroupName + "/" + ((MapMember)item).Name));
					foreach (long item2 in list2)
					{
						list.Add((item, item2, $"{value} (OID {item2})", owningGroupName));
					}
				}
			}
			return list;
		}, TaskCreationOptions.None);
	}

	private static bool IsStructureJunctionPoleLayer(FeatureLayer layer, string owningGroup)
	{
		if (layer == null)
		{
			return false;
		}
		return string.Equals(owningGroup, "StructureJunction", StringComparison.OrdinalIgnoreCase) && (((MapMember)layer).Name.IndexOf("Pole", StringComparison.OrdinalIgnoreCase) >= 0 || ((MapMember)layer).Name.IndexOf("StructureJunction", StringComparison.OrdinalIgnoreCase) >= 0);
	}

	private static async Task<bool> IsSupportedPoleAssetTypeAsync(FeatureLayer layer, long objectId)
	{
		int assetType = 0;
		try
		{
			Inspector inspector = new Inspector();
			await QueuedTask.Run((Action)delegate
			{
				inspector.Load((MapMember)layer, objectId);
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
			return (Geometry)CreateMapPoint(anchorPoint, template.Location, rotationDegrees);
		}
		if (template.Line != null)
		{
			MapPoint anchorPoint2 = (MapPoint)sketchGeometry;
			List<MapPoint> list = template.Line.Select((List<double> n) => CreateMapPoint(anchorPoint2, n, rotationDegrees)).ToList();
			return (Geometry)PolylineBuilderEx.CreatePolyline((IEnumerable<MapPoint>)list, ((Geometry)anchorPoint2).SpatialReference);
		}
		if (template.Polygon != null)
		{
			MapPoint anchorPoint3 = (MapPoint)sketchGeometry;
			List<MapPoint> list2 = template.Polygon.Select((List<double> n) => CreateMapPoint(anchorPoint3, n, rotationDegrees)).ToList();
			return (Geometry)PolygonBuilderEx.CreatePolygon((IEnumerable<MapPoint>)list2, ((Geometry)anchorPoint3).SpatialReference);
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

	internal static List<PreviewOverlayGraphic> CreatePreviewGraphics(MapPoint anchorPoint, double rotationDegrees = 0.0, bool primaryPartOnly = false)
	{
		DisplayTemplate selectedTemplate = AddinConfiguration.SelectedTemplate;
		string text = selectedTemplate?.Name;
		if (anchorPoint == null || string.IsNullOrWhiteSpace(text))
		{
			return new List<PreviewOverlayGraphic>();
		}
		if (_cachedPreviewPointSymbol == null)
		{
			_cachedPreviewPointSymbol = CreatePreviewPointSymbol();
			_cachedPreviewLineSymbol = SymbolExtensionMethods.MakeSymbolReference((CIMSymbol)(object)SymbolFactory.Instance.ConstructLineSymbol(ColorFactory.Instance.CreateRGBColor(222.0, 123.0, 207.0, 70.0), 2.0, (SimpleLineStyle)1));
			_cachedPreviewPolygonSymbol = SymbolExtensionMethods.MakeSymbolReference((CIMSymbol)(object)SymbolFactory.Instance.ConstructPolygonSymbol(ColorFactory.Instance.CreateRGBColor(222.0, 123.0, 207.0, 18.0), (SimpleFillStyle)0, SymbolFactory.Instance.ConstructStroke(ColorFactory.Instance.CreateRGBColor(0.0, 133.0, 202.0, 70.0), 2.0, (SimpleLineStyle)1)));
		}
		List<PreviewOverlayGraphic> list = new List<PreviewOverlayGraphic>();
		if (selectedTemplate.IsGroupChild)
		{
			SimpleTemplateReference groupChildReference = GetGroupChildReference(selectedTemplate);
			SimpleTemplate simpleTemplate = TemplateCache.GetSimpleTemplate(groupChildReference?.Name);
			if (groupChildReference == null || simpleTemplate == null || !IsFeatureLayerTemplate(simpleTemplate))
			{
				return list;
			}
			AddPreviewGraphicForTemplateReference(list, groupChildReference, simpleTemplate, anchorPoint, rotationDegrees, _cachedPreviewPointSymbol, _cachedPreviewLineSymbol, _cachedPreviewPolygonSymbol, useAllConfiguredGeometry: false);
			return list;
		}
		GroupTemplate groupTemplate = TemplateCache.GetGroupTemplate(text);
		if (groupTemplate?.SimpleTemplates == null)
		{
			SimpleTemplate simpleTemplate2 = TemplateCache.GetSimpleTemplate(text);
			if (HasConfiguredSimpleGeometry(simpleTemplate2))
			{
				Geometry geometry = CreateGeometryForSimpleTemplate(simpleTemplate2, anchorPoint, rotationDegrees);
				AddPreviewGraphic(list, geometry, _cachedPreviewPointSymbol, _cachedPreviewLineSymbol, _cachedPreviewPolygonSymbol);
			}
			else if (IsSimplePointTemplate(simpleTemplate2))
			{
				list.Add(new PreviewOverlayGraphic((Geometry)(object)anchorPoint, _cachedPreviewPointSymbol));
			}
			return list;
		}
		if (!HasConfiguredPlacementGeometry(groupTemplate))
		{
			return list;
		}
		IEnumerable<SimpleTemplateReference> enumerable = groupTemplate.SimpleTemplates;
		if (primaryPartOnly)
		{
			SimpleTemplateReference simpleTemplateReference = groupTemplate.SimpleTemplates.FirstOrDefault((SimpleTemplateReference templateRef) => templateRef.FeatureId == 1) ?? groupTemplate.SimpleTemplates.FirstOrDefault();
			IEnumerable<SimpleTemplateReference> enumerable3;
			if (simpleTemplateReference != null)
			{
				IEnumerable<SimpleTemplateReference> enumerable2 = new SimpleTemplateReference[1] { simpleTemplateReference };
				enumerable3 = enumerable2;
			}
			else
			{
				enumerable3 = Enumerable.Empty<SimpleTemplateReference>();
			}
			enumerable = enumerable3;
		}
		foreach (SimpleTemplateReference item in enumerable)
		{
			SimpleTemplate simpleTemplate3 = TemplateCache.GetSimpleTemplate(item.Name);
			if (IsFeatureLayerTemplate(simpleTemplate3))
			{
				AddPreviewGraphicForTemplateReference(list, item, simpleTemplate3, anchorPoint, rotationDegrees, _cachedPreviewPointSymbol, _cachedPreviewLineSymbol, _cachedPreviewPolygonSymbol, useAllConfiguredGeometry: true);
			}
		}
		return list;
	}

	private static void AddPreviewGraphicForTemplateReference(List<PreviewOverlayGraphic> graphics, SimpleTemplateReference templateRef, SimpleTemplate template, MapPoint anchorPoint, double rotationDegrees, CIMSymbolReference pointSymbol, CIMSymbolReference lineSymbol, CIMSymbolReference polygonSymbol, bool useAllConfiguredGeometry)
	{
		if (templateRef != null && template != null)
		{
			if (useAllConfiguredGeometry && (templateRef.Location != null || templateRef.Line != null || templateRef.Polygon != null))
			{
				Geometry geometry = CreateGeometryForTemplate(templateRef, (Geometry)anchorPoint, rotationDegrees);
				AddPreviewGraphic(graphics, geometry, pointSymbol, lineSymbol, polygonSymbol);
			}
			else if (!useAllConfiguredGeometry && templateRef.Polygon != null)
			{
				Geometry geometry2 = CreateGeometryForTemplate(templateRef, (Geometry)anchorPoint, rotationDegrees);
				AddPreviewGraphic(graphics, geometry2, pointSymbol, lineSymbol, polygonSymbol);
			}
			else if (IsSimplePointTemplate(template))
			{
				graphics.Add(new PreviewOverlayGraphic((Geometry)(object)anchorPoint, pointSymbol));
			}
		}
	}

	private static bool HasConfiguredSimpleGeometry(SimpleTemplate template)
	{
		return IsFeatureLayerTemplate(template) && template.Geometry != null && template.Geometry.Count >= 3;
	}

	private static Geometry CreateGeometryForSimpleTemplate(SimpleTemplate template, MapPoint anchorPoint, double rotationDegrees)
	{
		List<MapPoint> list = template.Geometry.Select((List<double> n) => CreateMapPoint(anchorPoint, n, rotationDegrees)).ToList();
		return (Geometry)PolygonBuilderEx.CreatePolygon((IEnumerable<MapPoint>)list, ((Geometry)anchorPoint).SpatialReference);
	}

	private static void AddPreviewGraphic(List<PreviewOverlayGraphic> graphics, Geometry geometry, CIMSymbolReference pointSymbol, CIMSymbolReference lineSymbol, CIMSymbolReference polygonSymbol)
	{
		MapPoint val = (MapPoint)(object)((geometry is MapPoint) ? geometry : null);
		if (val != null)
		{
			graphics.Add(new PreviewOverlayGraphic((Geometry)(object)val, pointSymbol));
			return;
		}
		Polyline val2 = (Polyline)(object)((geometry is Polyline) ? geometry : null);
		if (val2 != null)
		{
			graphics.Add(new PreviewOverlayGraphic((Geometry)(object)val2, lineSymbol));
			return;
		}
		Polygon val3 = (Polygon)(object)((geometry is Polygon) ? geometry : null);
		if (val3 != null)
		{
			graphics.Add(new PreviewOverlayGraphic((Geometry)(object)val3, polygonSymbol));
		}
	}

	private static CIMSymbolReference CreatePreviewPointSymbol()
	{
		CIMColor val = ColorFactory.Instance.CreateRGBColor(222.0, 123.0, 207.0, 70.0);
		CIMColor val2 = ColorFactory.Instance.CreateRGBColor(0.0, 133.0, 202.0, 70.0);
		CIMPolygonSymbol symbol = SymbolFactory.Instance.ConstructPolygonSymbol(val, (SimpleFillStyle)0, SymbolFactory.Instance.ConstructStroke(val2, 1.5, (SimpleLineStyle)0));
		CIMPointSymbol val3 = SymbolFactory.Instance.ConstructPointSymbol(val, 9.0, (SimpleMarkerStyle)0);
		CIMSymbolLayer[] array = ((CIMMultiLayerSymbol)val3).SymbolLayers ?? Array.Empty<CIMSymbolLayer>();
		foreach (CIMSymbolLayer val4 in array)
		{
			CIMVectorMarker val5 = (CIMVectorMarker)(object)((val4 is CIMVectorMarker) ? val4 : null);
			if (val5 != null && val5.MarkerGraphics != null)
			{
				CIMMarkerGraphic[] markerGraphics = val5.MarkerGraphics;
				foreach (CIMMarkerGraphic val6 in markerGraphics)
				{
					val6.Symbol = (CIMSymbol)(object)symbol;
				}
			}
		}
		return SymbolExtensionMethods.MakeSymbolReference((CIMSymbol)(object)val3);
	}

	private static bool IsSimplePointTemplate(SimpleTemplate template)
	{
		if (!IsFeatureLayerTemplate(template))
		{
			return false;
		}
		string key = template.Name ?? string.Empty;
		lock (_simplePointTemplateCacheLock)
		{
			if (_simplePointTemplateCache.TryGetValue(key, out var value))
			{
				return value;
			}
		}
		FeatureLayer featureLayerByName = MapMemberLookupService.GetFeatureLayerByName(template.SubtypeLayer, template.GroupLayer);
		if (featureLayerByName == null)
		{
			return false;
		}
		bool flag = GeometryTypeHelper.IsPoint(GetFeatureLayerShapeType(featureLayerByName));
		lock (_simplePointTemplateCacheLock)
		{
			_simplePointTemplateCache[key] = flag;
		}
		return flag;
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
			double num = rotationDegrees * Math.PI / 180.0;
			double num2 = Math.Cos(num);
			double num3 = Math.Sin(num);
			double num4 = xOffset * num2 - yOffset * num3;
			double num5 = xOffset * num3 + yOffset * num2;
			xOffset = num4;
			yOffset = num5;
		}
		return MapPointBuilderEx.CreateMapPoint(anchorPoint.X + xOffset, anchorPoint.Y + yOffset, ((Geometry)anchorPoint).SpatialReference);
	}

	private static void ApplyMirrorMode(ref double xOffset, ref double yOffset)
	{
		switch (AddinConfiguration.PlacementMirrorMode)
		{
		case PlacementMirrorMode.Horizontal:
			xOffset = 0.0 - xOffset;
			break;
		case PlacementMirrorMode.Vertical:
			yOffset = 0.0 - yOffset;
			break;
		case PlacementMirrorMode.Both:
			xOffset = 0.0 - xOffset;
			yOffset = 0.0 - yOffset;
			break;
		}
	}

	private static AssociationDescription CreateAssociationDescription(AssociationObject association, FeatureInfo fromInfo, FeatureInfo toInfo)
	{
		RowHandle val = CreateRowHandle(fromInfo);
		RowHandle val2 = CreateRowHandle(toInfo);
		AssociationDescription result = null;
		switch (association.Type.ToUpper())
		{
		case "CONTAINMENT":
			result = new AssociationDescription((AssociationType)2, val, val2, toInfo.IsSpatialFeature);
			break;
		case "ATTACHMENT":
			result = new AssociationDescription((AssociationType)3, val, val2);
			break;
		case "JUNCTIONJUNCTIONCONNECTIVITY":
			result = ((association.FromTerminal == 0) ? new AssociationDescription((AssociationType)1, val, val2) : new AssociationDescription((AssociationType)1, val, (long)association.FromTerminal, val2));
			break;
		case "JUNCTIONEDGEOBJECTCONNECTIVITYFROMSIDE":
			result = new AssociationDescription((AssociationType)4, val, val2);
			break;
		case "JUNCTIONEDGEOBJECTCONNECTIVITYTOSIDE":
			result = new AssociationDescription((AssociationType)6, val, val2);
			break;
		case "JUNCTIONEDGEOBJECTCONNECTIVITYMIDSPAN":
			result = new AssociationDescription((AssociationType)5, val, val2);
			break;
		}
		return result;
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
		if (template == null)
		{
			throw new InvalidOperationException("Template configuration references a missing simple template.");
		}
		string placementPartKey = PlacementAttributeOverrideService.BuildPlacementPartKey(template, parentTemplateName, featureId);
		RowToken token;
		if (AddinConfiguration.GroupFeatureLayerNames.Contains(template.GroupLayer.ToUpperInvariant()))
		{
			FeatureLayer layer = await MapMemberLookupService.GetFeatureLayerByNameAsync(template.SubtypeLayer, template.GroupLayer);
			if (layer == null)
			{
				throw new InvalidOperationException($"Layer '{template.GroupLayer}/{template.SubtypeLayer}' was not found for template '{template.Name}'.");
			}
			token = await CreateFeature(layer, geometry, template, operation, includeDefaultAttributes, rotationDegrees, placementPartKey);
		}
		else
		{
			StandaloneTable table = await MapMemberLookupService.GetTableByNameAsync(template.SubtypeLayer, template.GroupLayer);
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
		Subtype subtype = null;
		if (!string.IsNullOrEmpty(subtypeField))
		{
			await QueuedTask.Run((Action)delegate
			{
				List<Subtype> source = ((TableDefinition)layer.GetFeatureClass().GetDefinition()).GetSubtypes().ToList();
				if (!defaultFieldValues.TryGetValue(subtypeField, out var value))
				{
					throw new InvalidOperationException($"Template '{template.Name}' is missing subtype field '{subtypeField}'.");
				}
				string subtypeDesc = Convert.ToString(GetObjectValue(value), CultureInfo.InvariantCulture);
				subtype = source.FirstOrDefault((Subtype n) => n.GetName() == subtypeDesc);
			}, TaskCreationOptions.None);
		}
		if (template.Geometry != null)
		{
			MapPoint anchorPoint = (MapPoint)geometry;
			List<MapPoint> points = template.Geometry.Select((List<double> n) => CreateMapPoint(anchorPoint, n, rotationDegrees)).ToList();
			geometry = (Geometry)PolygonBuilderEx.CreatePolygon((IEnumerable<MapPoint>)points, ((Geometry)anchorPoint).SpatialReference);
		}
		Dictionary<string, object> effectiveFieldValues = await PlacementAttributeOverrideService.ApplyOverridesAsync(template, defaultFieldValues, subtype, fields, placementPartKey);
		Dictionary<string, object> attributes = new Dictionary<string, object> { ["SHAPE"] = geometry };
		foreach (string fieldName in GetAttributeFieldsToApply(effectiveFieldValues, subtypeField, includeDefaultAttributes, fields, rotationDegrees))
		{
			Dictionary<string, object> dictionary = attributes;
			string key = fieldName;
			Dictionary<string, object> dictionary2 = dictionary;
			string key2 = key;
			dictionary2[key2] = await GetDatabaseFieldValueFromConfigValue(effectiveFieldValues, subtype, fields, fieldName, rotationDegrees);
		}
		return operation.Create((MapMember)layer, attributes);
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
		Subtype subtype = null;
		if (!string.IsNullOrEmpty(subtypeField))
		{
			await QueuedTask.Run((Action)delegate
			{
				List<Subtype> source = table.GetTable().GetDefinition().GetSubtypes()
					.ToList();
				if (!defaultFieldValues.TryGetValue(subtypeField, out var value))
				{
					throw new InvalidOperationException($"Template '{template.Name}' is missing subtype field '{subtypeField}'.");
				}
				string subtypeDesc = Convert.ToString(GetObjectValue(value), CultureInfo.InvariantCulture);
				subtype = source.FirstOrDefault((Subtype n) => n.GetName() == subtypeDesc);
			}, TaskCreationOptions.None);
		}
		Dictionary<string, object> effectiveFieldValues = await PlacementAttributeOverrideService.ApplyOverridesAsync(template, defaultFieldValues, subtype, fields, placementPartKey);
		Dictionary<string, object> attributes = new Dictionary<string, object>();
		foreach (string fieldName in GetAttributeFieldsToApply(effectiveFieldValues, subtypeField, includeDefaultAttributes))
		{
			Dictionary<string, object> dictionary = attributes;
			string key = fieldName;
			Dictionary<string, object> dictionary2 = dictionary;
			string key2 = key;
			dictionary2[key2] = await GetDatabaseFieldValueFromConfigValue(effectiveFieldValues, subtype, fields, fieldName);
		}
		return operation.Create((MapMember)table, attributes);
	}

	private static IEnumerable<string> GetAttributeFieldsToApply(Dictionary<string, object> defaultFieldValues, string subtypeField, bool includeDefaultAttributes, List<Field> fields = null, double rotationDegrees = 0.0)
	{
		IEnumerable<string> enumerable = (includeDefaultAttributes ? defaultFieldValues.Keys : ((!string.IsNullOrWhiteSpace(subtypeField)) ? defaultFieldValues.Keys.Where((string fieldName) => string.Equals(fieldName, subtypeField, StringComparison.OrdinalIgnoreCase)) : Enumerable.Empty<string>()));
		if (!ShouldApplySymbolRotation(rotationDegrees))
		{
			return enumerable;
		}
		IEnumerable<string> second = from field in fields ?? new List<Field>()
			where IsSymbolRotationField((field != null) ? field.Name : null)
			select field.Name;
		return from fieldName in enumerable.Concat(second)
			group fieldName by NormalizeFieldIdentifier(fieldName) into @group
			select @group.First();
	}

	private static async Task<object> GetDatabaseFieldValueFromConfigValue(Dictionary<string, object> defaultFieldValues, Subtype subtype, List<Field> fields, string fieldName, double rotationDegrees = 0.0)
	{
		object fieldValue = null;
		bool hasConfiguredFieldValue = defaultFieldValues.TryGetValue(fieldName, out var rawConfigFieldValue);
		object configFieldValue = GetObjectValue(rawConfigFieldValue);
		Field field = fields.FirstOrDefault((Field n) => string.Equals(n.Name, fieldName, StringComparison.OrdinalIgnoreCase));
		if (field == null)
		{
			throw new InvalidOperationException("Field '" + fieldName + "' was not found.");
		}
		configFieldValue = GetSymbolRotationFieldValue(configFieldValue, hasConfiguredFieldValue, field, fieldName, rotationDegrees);
		await QueuedTask.Run((Action)delegate
		{
			if (subtype != null)
			{
				Domain domain = field.GetDomain(subtype);
				if (domain != null)
				{
					fieldValue = GetCodedDomainValue(domain, configFieldValue);
				}
				else
				{
					domain = field.GetDomain((Subtype)null);
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
				Domain domain2 = field.GetDomain((Subtype)null);
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
		if (!IsSymbolRotationField((field != null) ? field.Name : null) && !IsSymbolRotationField(configFieldName))
		{
			return configFieldValue;
		}
		bool flag = !hasConfiguredFieldValue || IsBlankValue(configFieldValue);
		double? num = (flag ? new double?(GetDefaultSymbolRotationWhenMissing()) : TryGetDouble(configFieldValue));
		if (!num.HasValue)
		{
			return configFieldValue;
		}
		double num2 = ApplyMirrorModeToSymbolRotation(num.Value);
		double num3 = NormalizeSymbolRotation(num2 + rotationDegrees);
		string value = (flag ? "default missing-field value" : "template value");
		LogService.Write($"Adjusted symbol rotation field '{configFieldName}' from {num.Value:0.######} ({value}) to {num3:0.######} (placement rotation {rotationDegrees:0.######}, mirror {AddinConfiguration.PlacementMirrorMode}).");
		return FormatSymbolRotationFieldValue(num3, configFieldValue, field);
	}

	private static object ConvertValueToFieldType(Field field, object value, Subtype subtype)
	{
		value = GetObjectValue(value);
		if (value == null)
		{
			return null;
		}
		string text = value as string;
		if (((int)field.FieldType == 1 || (int)field.FieldType == 0 || (int)field.FieldType == 13) && !string.IsNullOrWhiteSpace(text) && subtype != null && string.Equals(text.Trim(), subtype.GetName(), StringComparison.OrdinalIgnoreCase))
		{
			object value2 = subtype.GetCode();
			if ((int)field.FieldType == 13)
			{
				return Convert.ToInt64(value2, CultureInfo.InvariantCulture);
			}
			return Convert.ToInt32(value2, CultureInfo.InvariantCulture);
		}
		if ((int)field.FieldType == 13)
		{
			return (value is long num) ? num : long.Parse(text, CultureInfo.InvariantCulture);
		}
		if ((int)field.FieldType == 1 || (int)field.FieldType == 0)
		{
			return (value is int num2) ? num2 : int.Parse(text, CultureInfo.InvariantCulture);
		}
		if ((int)field.FieldType == 2)
		{
			return (value is float num3) ? num3 : float.Parse(text, CultureInfo.InvariantCulture);
		}
		if ((int)field.FieldType == 3)
		{
			return (value is double num4) ? num4 : double.Parse(text, CultureInfo.InvariantCulture);
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
		List<string> list = AddinConfiguration.Settings?.SymbolRotationFieldNames;
		IEnumerable<string> enumerable2;
		if (list == null || list.Count <= 0)
		{
			IEnumerable<string> enumerable = new string[4] { "ROTATION", "SYMBOLROTATION", "SYMBOL_ROTATION", "ANGLE" };
			enumerable2 = enumerable;
		}
		else
		{
			IEnumerable<string> enumerable = list;
			enumerable2 = enumerable;
		}
		IEnumerable<string> source = enumerable2;
		return source.Any((string rotationFieldName) => string.Equals(rotationFieldName, fieldName, StringComparison.OrdinalIgnoreCase) || string.Equals(NormalizeFieldIdentifier(rotationFieldName), normalizedFieldName, StringComparison.OrdinalIgnoreCase));
	}

	private static bool ShouldApplySymbolRotation(double rotationDegrees)
	{
		return Math.Abs(rotationDegrees) > 0.0001 || AddinConfiguration.PlacementMirrorMode != PlacementMirrorMode.None;
	}

	private static bool IsBlankValue(object value)
	{
		return value == null || (value is string value2 && string.IsNullOrWhiteSpace(value2));
	}

	private static double GetDefaultSymbolRotationWhenMissing()
	{
		return AddinConfiguration.Settings?.DefaultSymbolRotationWhenMissing ?? 90.0;
	}

	private static string NormalizeFieldIdentifier(string fieldName)
	{
		return new string((fieldName ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
	}

	private static double? TryGetDouble(object value)
	{
		value = GetObjectValue(value);
		if (value == null)
		{
			return 0.0;
		}
		object obj = value;
		object obj2 = obj;
		if (!(obj2 is double value2))
		{
			if (!(obj2 is float num))
			{
				if (!(obj2 is decimal num2))
				{
					if (!(obj2 is int num3))
					{
						if (!(obj2 is long num4))
						{
							if (!(obj2 is short num5))
							{
								if (!(obj2 is byte b))
								{
									if (obj2 is string text)
									{
										if (string.IsNullOrWhiteSpace(text))
										{
											return 0.0;
										}
										if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
										{
											return result;
										}
										if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var result2))
										{
											return result2;
										}
									}
									return null;
								}
								return (int)b;
							}
							return num5;
						}
						return num4;
					}
					return num3;
				}
				return (double)num2;
			}
			return num;
		}
		return value2;
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
		PlacementMirrorMode placementMirrorMode = AddinConfiguration.PlacementMirrorMode;
		if (1 == 0)
		{
		}
		double result = placementMirrorMode switch
		{
			PlacementMirrorMode.Horizontal => 180.0 - degrees, 
			PlacementMirrorMode.Vertical => 0.0 - degrees, 
			PlacementMirrorMode.Both => degrees + 180.0, 
			_ => degrees, 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	private static object FormatSymbolRotationFieldValue(double degrees, object templateValue, Field field)
	{
		if (templateValue is string)
		{
			return degrees.ToString("0.######", CultureInfo.InvariantCulture);
		}
		if ((int)field.FieldType == 13 || (int)field.FieldType == 1 || (int)field.FieldType == 0)
		{
			return Convert.ToInt32(Math.Round(degrees, MidpointRounding.AwayFromZero));
		}
		if ((int)field.FieldType == 2 || (int)field.FieldType == 3)
		{
			return degrees;
		}
		return degrees.ToString("0.######", CultureInfo.InvariantCulture);
	}

	private static object GetCodedDomainValue(Domain domain, object configFieldValue)
	{
		if (configFieldValue == null)
		{
			return null;
		}
		return (domain is CodedValueDomain) ? ((CodedValueDomain)domain).GetCodedValue(configFieldValue.ToString()) : configFieldValue;
	}

	public static async Task<string> ValidateConfiguration()
	{
		// Validation moves from cheap structural checks to map/schema checks so the
		// user sees the root configuration issue before downstream noise.
		TemplateConfig templates = GetLoadedTemplateConfigOrThrow();
		string message = null;
		List<string> errors = new List<string>();
		List<SimpleTemplate> configuredSimpleTemplates = templates.SimpleTemplates ?? new List<SimpleTemplate>();
		List<GroupTemplate> configuredGroupTemplates = templates.GroupTemplates ?? new List<GroupTemplate>();
		ValidateTemplateStructure(configuredSimpleTemplates, configuredGroupTemplates, errors);
		if (errors.Count > 0)
		{
			return string.Join("\n", errors);
		}
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
			HashSet<string> simpleTemplateNames = new HashSet<string>(templates.SimpleTemplates.Select((SimpleTemplate n) => n.Name), StringComparer.OrdinalIgnoreCase);
			foreach (GroupTemplate groupTemplate in templates.GroupTemplates)
			{
				List<string> invalidTemplateNames = (from n in groupTemplate.SimpleTemplates
					select n.Name into n
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
			foreach (GroupTemplate groupTemplate3 in templates.GroupTemplates)
			{
				List<SimpleTemplateReference> simpleTemplates = groupTemplate3.SimpleTemplates;
				if (simpleTemplates == null || !simpleTemplates.Any((SimpleTemplateReference simpleTemplateReference) => simpleTemplateReference.FeatureId == 1))
				{
					errors.Add("Group template " + groupTemplate3.Name + " must include a simple template reference with FeatureId 1 to define the sketch feature.");
				}
			}
		}
		if (errors.Count == 0)
		{
			foreach (GroupTemplate groupTemplate4 in templates.GroupTemplates)
			{
				foreach (SimpleTemplateReference templateRef in groupTemplate4.SimpleTemplates)
				{
					SimpleTemplate template4 = templates.SimpleTemplates.FirstOrDefault((SimpleTemplate n) => string.Equals(n.Name, templateRef.Name, StringComparison.OrdinalIgnoreCase));
					string error6 = await ValidateGeometry(groupTemplate4, templateRef, template4);
					if (error6 != null)
					{
						errors.Add(error6);
					}
				}
			}
		}
		if (errors.Count == 0)
		{
			foreach (GroupTemplate groupTemplate5 in templates.GroupTemplates)
			{
				List<int> featureIds = (groupTemplate5.SimpleTemplates ?? new List<SimpleTemplateReference>()).Select((SimpleTemplateReference n) => n.FeatureId).ToList();
				foreach (AssociationObject assoc in groupTemplate5.Associations ?? new List<AssociationObject>())
				{
					if (!featureIds.Contains(assoc.FromFeatureId) || !featureIds.Contains(assoc.ToFeatureId))
					{
						string error7 = "Group template " + groupTemplate5.Name + " has associations with invalid FeatureIDs.";
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

	internal static void ValidateTemplateStructure(IReadOnlyList<SimpleTemplate> simpleTemplates, IReadOnlyList<GroupTemplate> groupTemplates, List<string> errors)
	{
		foreach (SimpleTemplate template in simpleTemplates)
		{
			if (template == null)
			{
				errors.Add("Template configuration contains a null simple template.");
			}
			else if (string.IsNullOrWhiteSpace(template.Name))
			{
				errors.Add("Template configuration contains a simple template without a Name.");
			}
		}
		foreach (IGrouping<string, SimpleTemplate> duplicate in simpleTemplates.Where((SimpleTemplate template) => !string.IsNullOrWhiteSpace(template?.Name)).GroupBy((SimpleTemplate template) => template.Name, StringComparer.OrdinalIgnoreCase).Where((IGrouping<string, SimpleTemplate> group) => group.Count() > 1))
		{
			errors.Add("Template configuration contains duplicate simple template name '" + duplicate.Key + "'.");
		}
		foreach (GroupTemplate groupTemplate in groupTemplates)
		{
			if (groupTemplate == null)
			{
				errors.Add("Template configuration contains a null group template.");
				continue;
			}
			if (string.IsNullOrWhiteSpace(groupTemplate.Name))
			{
				errors.Add("Template configuration contains a group template without a Name.");
			}
			if (groupTemplate.SimpleTemplates == null || groupTemplate.SimpleTemplates.Count == 0)
			{
				errors.Add("Group template '" + (groupTemplate.Name ?? "(unnamed)") + "' does not contain any simple templates.");
				continue;
			}
			if (groupTemplate.SimpleTemplates.Any((SimpleTemplateReference reference) => reference == null || string.IsNullOrWhiteSpace(reference.Name)))
			{
				errors.Add("Group template '" + (groupTemplate.Name ?? "(unnamed)") + "' contains a simple template reference without a Name.");
			}
		}
		foreach (IGrouping<string, GroupTemplate> duplicate2 in groupTemplates.Where((GroupTemplate template) => !string.IsNullOrWhiteSpace(template?.Name)).GroupBy((GroupTemplate template) => template.Name, StringComparer.OrdinalIgnoreCase).Where((IGrouping<string, GroupTemplate> group) => group.Count() > 1))
		{
			errors.Add("Template configuration contains duplicate group template name '" + duplicate2.Key + "'.");
		}
	}

	private static string ValidateLayerOrTableName(SimpleTemplate template)
	{
		string result = null;
		if (template == null)
		{
			return "Template configuration contains a null simple template.";
		}
		if (string.IsNullOrWhiteSpace(template.GroupLayer))
		{
			return "Template '" + template.Name + "' is missing GroupLayer.";
		}
		if (AddinConfiguration.GroupFeatureLayerNames.Contains(template.GroupLayer.ToUpper()))
		{
			FeatureLayer featureLayerByName = MapMemberLookupService.GetFeatureLayerByName(template.SubtypeLayer, template.GroupLayer);
			if (featureLayerByName == null)
			{
				result = $"Group layer/subtype layer {template.GroupLayer}/{template.SubtypeLayer} does not exist in the map ({template.Name}).";
			}
		}
		else
		{
			StandaloneTable tableByName = MapMemberLookupService.GetTableByName(template.SubtypeLayer, template.GroupLayer);
			if (tableByName == null)
			{
				result = $"Group table/subtype table {template.GroupLayer}/{template.SubtypeLayer} does not exist in the map ({template.Name}).";
			}
		}
		return result;
	}

	private static async Task<string> ValidateLayerOrTableFields(SimpleTemplate template)
	{
		string error = null;
		List<string> fieldNamesInDatabase = null;
		string subtypeField = null;
		bool isFeatureLayer = AddinConfiguration.GroupFeatureLayerNames.Contains(template.GroupLayer.ToUpper());
		if (isFeatureLayer)
		{
			FeatureLayer layer = await MapMemberLookupService.GetFeatureLayerByNameAsync(template.SubtypeLayer, template.GroupLayer);
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
			StandaloneTable table = await MapMemberLookupService.GetTableByNameAsync(template.SubtypeLayer, template.GroupLayer);
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
		List<Subtype> subtypes = null;
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
				subtypeDescs = subtypes.Select((Subtype n) => n.GetName()).ToList();
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
				string layerType2 = (isFeatureLayer ? "LAYER" : "TABLE");
				error = $"{layerType2} {template.GroupLayer}/{template.SubtypeLayer}: invalid {subtypeField} (subtype) value: {subtypeDesc}.";
			}
			else
			{
				List<string> fieldErrors = new List<string>();
				string subtypeValue = Convert.ToString(GetObjectValue(defaultFieldValues[subtypeField]), CultureInfo.InvariantCulture);
				Subtype subtype = null;
				await QueuedTask.Run((Action)delegate
				{
					subtype = subtypes.FirstOrDefault((Subtype n) => n.GetName() == subtypeValue);
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
					Domain domain = null;
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
						domain = field.GetDomain((Subtype)null);
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
			Dictionary<string, object> defaultFieldValues2 = template.DefaultFieldValues ?? new Dictionary<string, object>();
			List<string> defaultFields2 = defaultFieldValues2.Keys.ToList();
			foreach (string fieldName2 in defaultFields2)
			{
				Field field2 = fields.FirstOrDefault((Field n) => string.Equals(n.Name, fieldName2, StringComparison.OrdinalIgnoreCase));
				if (field2 == null)
				{
					fieldErrors2.Add(fieldName2.ToUpper() + ": field not found");
					continue;
				}
				Domain domain2 = null;
				await QueuedTask.Run((Action)delegate
				{
					domain2 = field2.GetDomain((Subtype)null);
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

	private static async Task CheckValueAgainstDomain(Domain domain, SimpleTemplate template, string fieldName, List<string> fieldErrors)
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
		string text = GetObjectValue(template.DefaultFieldValues[field.Name])?.ToString();
		bool flag = true;
		if ((int)field.FieldType == 13 || (int)field.FieldType == 1 || (int)field.FieldType == 0)
		{
			flag = int.TryParse(text, out var _);
		}
		else if ((int)field.FieldType == 2 || (int)field.FieldType == 3)
		{
			flag = double.TryParse(text, out var _);
		}
		if (!flag)
		{
			fieldErrors.Add(field.Name.ToUpper() + ": " + text);
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
				geometryType = GetFeatureLayerShapeType(layer);
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
}
