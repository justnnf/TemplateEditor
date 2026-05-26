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

internal static class CommonFunctions
{
	public static FeatureLayer GetFeatureLayerByName(string subtypeLayerName, string groupLayerName)
	{
		if (subtypeLayerName != null)
		{
			MapView active = MapView.Active;
			List<FeatureLayer> featureLayers = ((active != null) ? (from n in active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>()
				where ((MapMember)n).Name.ToUpper() == subtypeLayerName.ToUpper()
				select n).ToList() : null);
			return featureLayers.Where((FeatureLayer n) => ((Layer)n).Parent is SubtypeGroupLayer && ((MapMember)(SubtypeGroupLayer)((Layer)n).Parent).Name.ToUpper() == groupLayerName.ToUpper()).FirstOrDefault();
		}
		MapView active2 = MapView.Active;
		return (active2 != null) ? (from n in active2.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>()
			where ((MapMember)n).Name.ToUpper() == groupLayerName.ToUpper()
			select n).FirstOrDefault() : null;
	}

	public static StandaloneTable GetTableByName(string subtypeLayerName, string groupLayerName)
	{
		if (subtypeLayerName != null)
		{
			MapView active = MapView.Active;
			List<StandaloneTable> tables = ((active != null) ? (from n in active.Map.GetStandaloneTablesAsFlattenedList().OfType<StandaloneTable>()
				where ((MapMember)n).Name.ToUpper() == subtypeLayerName.ToUpper()
				select n).ToList() : null);
			return tables.Where((StandaloneTable n) => n.Parent is SubtypeGroupTable && ((MapMember)(SubtypeGroupTable)n.Parent).Name.ToUpper() == groupLayerName.ToUpper()).FirstOrDefault();
		}
		MapView active2 = MapView.Active;
		return (active2 != null) ? (from n in active2.Map.GetStandaloneTablesAsFlattenedList().OfType<StandaloneTable>()
			where ((MapMember)n).Name.ToUpper() == groupLayerName.ToUpper()
			select n).FirstOrDefault() : null;
	}

	public static FeatureLayer GetFeatureLayerByName(string layerName)
	{
		MapView active = MapView.Active;
		return (active != null) ? (from n in active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>()
			where ((MapMember)n).Name.ToUpper() == layerName.ToUpper()
			select n).FirstOrDefault() : null;
	}

	public static SubtypeGroupLayer GetGroupLayerByName(string layerName)
	{
		MapView active = MapView.Active;
		return (active != null) ? (from n in active.Map.GetLayersAsFlattenedList().OfType<SubtypeGroupLayer>()
			where ((MapMember)n).Name.ToUpper() == layerName.ToUpper()
			select n).FirstOrDefault() : null;
	}

	public static IEnumerable<FeatureLayer> GetFeatureLayersForGroups(IEnumerable<string> groupNames)
	{
		MapView active = MapView.Active;
		if (active == null || groupNames == null)
		{
			return Enumerable.Empty<FeatureLayer>();
		}
		HashSet<string> groupNameLookup = groupNames.Where((string name) => !string.IsNullOrWhiteSpace(name)).Select((string name) => name.ToUpperInvariant()).ToHashSet();
		return active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().Where(delegate(FeatureLayer layer)
		{
			string text = GetOwningGroupName(layer);
			return groupNameLookup.Contains(layer.Name.ToUpperInvariant()) || (!string.IsNullOrWhiteSpace(text) && groupNameLookup.Contains(text.ToUpperInvariant()));
		}).ToList();
	}

	public static string GetOwningGroupName(FeatureLayer layer)
	{
		if (layer == null)
		{
			return null;
		}
		return (((Layer)layer).Parent is SubtypeGroupLayer) ? ((MapMember)(SubtypeGroupLayer)((Layer)layer).Parent).Name : layer.Name;
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
		string templateName = template?.Name ?? AddinConfiguration.SelectedTemplate?.Name;
		if (string.IsNullOrWhiteSpace(templateName))
		{
			return (GeometryType)0;
		}
		if (template?.IsGroupChild == true)
		{
			SimpleTemplateReference childTemplateRef = GetGroupChildReference(template);
			SimpleTemplate childTemplate = AddinConfiguration.Templates.SimpleTemplates.FirstOrDefault((SimpleTemplate n) =>
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
		if (childTemplateRef.Polygon != null)
		{
			return (GeometryType)513;
		}
			return await GetSimpleTemplateGeometryTypeAsync(childTemplate);
		}
		bool isSimpleTemplate = AddinConfiguration.Templates.SimpleTemplates.Select((SimpleTemplate n) => n.Name).Contains(templateName);
		SimpleTemplate simpleTemplate = null;
		if (isSimpleTemplate)
		{
			simpleTemplate = AddinConfiguration.Templates.SimpleTemplates.Where((SimpleTemplate n) => n.Name == templateName).FirstOrDefault();
			return await GetSimpleTemplateGeometryTypeAsync(simpleTemplate);
		}
		GroupTemplate groupTemplate = AddinConfiguration.Templates.GroupTemplates.Where((GroupTemplate n) => n.Name == templateName).FirstOrDefault();
		SimpleTemplateReference simpleTemplateRef = groupTemplate?.SimpleTemplates?.Where((SimpleTemplateReference n) => n.FeatureId == 1).FirstOrDefault();
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
			return (GeometryType)513;
		}
		SimpleTemplate referencedTemplate = AddinConfiguration.Templates.SimpleTemplates.FirstOrDefault((SimpleTemplate n) => n.Name == simpleTemplateRef.Name);
		return referencedTemplate == null ? configuredSketchType : await GetSimpleTemplateGeometryTypeAsync(referencedTemplate);
	}

	private static GeometryType GetConfiguredSketchGeometryType(SimpleTemplateReference simpleTemplateRef)
	{
		return (GeometryType)(simpleTemplateRef?.SketchType?.ToUpperInvariant() switch
		{
			"LINE" => 25607,
			"POLYGON" => 27656,
			_ => 513
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

	private static async Task<GeometryType> GetSimpleTemplateGeometryTypeAsync(SimpleTemplate simpleTemplate)
	{
		if (simpleTemplate == null || !IsFeatureLayerTemplate(simpleTemplate))
		{
			return (GeometryType)0;
		}
		FeatureLayer layer = GetFeatureLayerByName(simpleTemplate.SubtypeLayer, simpleTemplate.GroupLayer);
		if (layer == null)
		{
			throw new InvalidOperationException($"Layer '{simpleTemplate.GroupLayer}/{simpleTemplate.SubtypeLayer}' was not found for template '{simpleTemplate.Name}'.");
		}
		return await QueuedTask.Run(() => layer.GetFeatureClass().GetDefinition().GetShapeType());
	}

	public static async Task CreateFeatures(Geometry sketchGeometry, double rotationDegrees = 0.0)
	{
		DisplayTemplate selectedTemplate = AddinConfiguration.SelectedTemplate;
		string templateName = selectedTemplate?.Name;
		if (string.IsNullOrWhiteSpace(templateName))
		{
			DialogService.Show("Choose a template before placing features.", "Template Editor");
			return;
		}
		try
		{
			string defaultVersionMessage = await GetDefaultVersionPlacementBlockMessageAsync(selectedTemplate);
			if (defaultVersionMessage != null)
			{
				DialogService.Show(defaultVersionMessage, "Template Editor");
				return;
			}
			if (selectedTemplate?.IsGroupChild == true)
			{
				await CreateGroupChildFeature(selectedTemplate, sketchGeometry, rotationDegrees);
				return;
			}
			bool isSimpleTemplate = AddinConfiguration.Templates.SimpleTemplates.Select((SimpleTemplate n) => n.Name).Contains(templateName);
			if (isSimpleTemplate)
			{
				SimpleTemplate simpleTemplate = AddinConfiguration.Templates.SimpleTemplates.FirstOrDefault((SimpleTemplate n) => n.Name == templateName);
				if (!IsStructureJunctionObjectTemplate(simpleTemplate) && !IsFeatureLayerTemplate(simpleTemplate) && !ConfirmCreateNonSpatialTemplate(simpleTemplate))
				{
					return;
				}
			}
			PlacementBuildResult placement = await BuildPlacementOperationAsync(templateName, sketchGeometry, isSimpleTemplate, PlacementOptions.Full, rotationDegrees);
			string errorMessage = await ExecutePlacementOperationAsync(placement.Operation);
			if (errorMessage == null)
			{
				await PopulateFeatureInfoDetailsAsync(placement.FeatureInfos);
				if (placement.ApplyConfiguredAssociations)
				{
					await ExecuteConfiguredAssociationsAsync(placement);
				}
				await FinalizePlacementAsync(placement.CreatedFeatures, applyPostPlacementEnhancements: true);
				return;
			}
			await TryPlaceWithFallbacksAsync(templateName, sketchGeometry, isSimpleTemplate, errorMessage, rotationDegrees);
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex)
		{
			DialogService.Show(ex.Message + "\n\n" + ex.StackTrace, "Template Editor");
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
			SimpleTemplate childTemplate = AddinConfiguration.Templates?.SimpleTemplates?.FirstOrDefault((SimpleTemplate template) =>
				string.Equals(template.Name, childTemplateRef?.Name, StringComparison.OrdinalIgnoreCase));
			if (childTemplate != null)
			{
				yield return childTemplate;
			}
			yield break;
		}
		SimpleTemplate simpleTemplate = AddinConfiguration.Templates?.SimpleTemplates?.FirstOrDefault((SimpleTemplate template) =>
			string.Equals(template.Name, selectedTemplate?.Name, StringComparison.OrdinalIgnoreCase));
		if (simpleTemplate != null)
		{
			yield return simpleTemplate;
			yield break;
		}
		GroupTemplate groupTemplate = AddinConfiguration.Templates?.GroupTemplates?.FirstOrDefault((GroupTemplate template) =>
			string.Equals(template.Name, selectedTemplate?.Name, StringComparison.OrdinalIgnoreCase));
		foreach (SimpleTemplateReference templateRef in groupTemplate?.SimpleTemplates ?? Enumerable.Empty<SimpleTemplateReference>())
		{
			SimpleTemplate template = AddinConfiguration.Templates?.SimpleTemplates?.FirstOrDefault((SimpleTemplate simple) =>
				string.Equals(simple.Name, templateRef.Name, StringComparison.OrdinalIgnoreCase));
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
		catch
		{
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
			catch
			{
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

	private static async Task CreateGroupChildFeature(DisplayTemplate childTemplate, Geometry sketchGeometry, double rotationDegrees)
	{
		SimpleTemplateReference childTemplateRef = GetGroupChildReference(childTemplate);
		SimpleTemplate template = AddinConfiguration.Templates.SimpleTemplates.FirstOrDefault((SimpleTemplate n) =>
			string.Equals(n.Name, childTemplateRef?.Name, StringComparison.OrdinalIgnoreCase));
		if (childTemplateRef == null || template == null)
		{
			throw new InvalidOperationException($"Template part '{childTemplate?.DisplayName}' was not found.");
		}
		EditOperation operation = new EditOperation
		{
			Name = "Create template part",
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
			RowToken token = await CreateFeatureOrRowFromSimpleTemplate(template, featureGeometry, operation, includeDefaultAttributes: true, rotationDegrees);
			TryTrackPlacedFeature(createdFeatures, template, featureGeometry, token, IsPlacementEnhancementCandidate(childTemplateRef));
		}
		else
		{
			await CreateTableRowWithAutoAssociationAsync(template, featureGeometry, operation, PlacementOptions.Full);
		}
		string errorMessage = await ExecutePlacementOperationAsync(operation);
		if (errorMessage != null)
		{
			DialogService.Show("Template part placement failed.\n\n" + CleanErrorMessage(errorMessage), "Template Editor");
			return;
		}
		await FinalizePlacementAsync(createdFeatures, applyPostPlacementEnhancements: true);
	}

	private static async Task TryPlaceWithFallbacksAsync(string templateName, Geometry sketchGeometry, bool isSimpleTemplate, string originalErrorMessage, double rotationDegrees)
	{
		if (DialogService.Show("Template placement failed.\n\n" + CleanErrorMessage(originalErrorMessage) + "\n\nTry placing the template without configured associations?", "Template Editor", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
		{
			PlacementBuildResult placementWithoutAssociations = await BuildPlacementOperationAsync(templateName, sketchGeometry, isSimpleTemplate, PlacementOptions.WithoutAssociations, rotationDegrees);
			string associationFallbackError = await ExecutePlacementOperationAsync(placementWithoutAssociations.Operation);
			if (associationFallbackError == null)
			{
				await PopulateFeatureInfoDetailsAsync(placementWithoutAssociations.FeatureInfos);
				await FinalizePlacementAsync(placementWithoutAssociations.CreatedFeatures, applyPostPlacementEnhancements: false);
				DialogService.Show("Template was placed without configured associations.", "Template Editor");
				return;
			}
			originalErrorMessage = associationFallbackError;
		}
		if (DialogService.Show("Template placement still failed.\n\n" + CleanErrorMessage(originalErrorMessage) + "\n\nTry placing with only subtype/required attributes and without configured associations?", "Template Editor", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
		{
			PlacementBuildResult minimalPlacement = await BuildPlacementOperationAsync(templateName, sketchGeometry, isSimpleTemplate, PlacementOptions.MinimalAttributes, rotationDegrees);
			string minimalError = await ExecutePlacementOperationAsync(minimalPlacement.Operation);
			if (minimalError == null)
			{
				await PopulateFeatureInfoDetailsAsync(minimalPlacement.FeatureInfos);
				await FinalizePlacementAsync(minimalPlacement.CreatedFeatures, applyPostPlacementEnhancements: false);
				DialogService.Show("Template was placed with only subtype/required attributes and without configured associations.", "Template Editor");
				return;
			}
			originalErrorMessage = minimalError;
		}
		DialogService.Show("Template placement failed.\n\n" + CleanErrorMessage(originalErrorMessage), "Template Editor");
	}

	private static async Task<PlacementBuildResult> BuildPlacementOperationAsync(string templateName, Geometry sketchGeometry, bool isSimpleTemplate, PlacementOptions options, double rotationDegrees)
	{
		EditOperation operation = new EditOperation
		{
			Name = options.OperationName,
			SelectNewFeatures = true
		};
		List<PlacedFeatureContext> createdFeatures = new List<PlacedFeatureContext>();
		List<FeatureInfo> featureInfos = new List<FeatureInfo>();
		List<AssociationObject> configuredAssociations = new List<AssociationObject>();
		if (isSimpleTemplate)
		{
			SimpleTemplate template = AddinConfiguration.Templates.SimpleTemplates.Where((SimpleTemplate n) => n.Name == templateName).FirstOrDefault();
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
		GroupTemplate groupTemplate = AddinConfiguration.Templates.GroupTemplates.Where((GroupTemplate n) => n.Name == templateName).FirstOrDefault();
		if (groupTemplate == null)
		{
			throw new InvalidOperationException($"Group template '{templateName}' was not found.");
		}
		foreach (SimpleTemplateReference simpleTemplateRef in groupTemplate.SimpleTemplates)
		{
			SimpleTemplate template = AddinConfiguration.Templates.SimpleTemplates.Where((SimpleTemplate n) => n.Name == simpleTemplateRef.Name).FirstOrDefault();
			if (template == null)
			{
				throw new InvalidOperationException($"Group template '{groupTemplate.Name}' references missing simple template '{simpleTemplateRef.Name}'.");
			}
			Geometry featureGeometry = CreateGeometryForTemplate(simpleTemplateRef, sketchGeometry, rotationDegrees);
			RowToken token = await CreateFeatureOrRowFromSimpleTemplate(template, featureGeometry, operation, options.IncludeDefaultAttributes, rotationDegrees);
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

	private static async Task FinalizePlacementAsync(List<PlacedFeatureContext> createdFeatures, bool applyPostPlacementEnhancements)
	{
		await PopulatePlacedFeatureDetails(createdFeatures);
		if (applyPostPlacementEnhancements)
		{
			await PlacementEnhancementService.ApplyPostPlacementEnhancementsAsync(createdFeatures);
		}
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
			return GetFeatureLayerByName(template.SubtypeLayer, template.GroupLayer);
		}
		return GetTableByName(template.SubtypeLayer, template.GroupLayer);
	}

	private static async Task ExecuteConfiguredAssociationsAsync(PlacementBuildResult placement)
	{
		if (placement?.ConfiguredAssociations == null || placement.ConfiguredAssociations.Count == 0)
		{
			return;
		}
		List<string> failures = new List<string>();
		foreach (AssociationObject association in placement.ConfiguredAssociations)
		{
			FeatureInfo fromInfo = placement.FeatureInfos.FirstOrDefault((FeatureInfo n) => n.FeatureId == association.FromFeatureId);
			FeatureInfo toInfo = placement.FeatureInfos.FirstOrDefault((FeatureInfo n) => n.FeatureId == association.ToFeatureId);
			if (fromInfo == null || toInfo == null)
			{
				failures.Add($"Feature {association.FromFeatureId} -> {association.ToFeatureId}: missing feature id.");
				continue;
			}
			string errorMessage = await ExecuteSingleConfiguredAssociationAsync(association, fromInfo, toInfo);
			if (errorMessage != null)
			{
				failures.Add($"{FormatAssociationLabel(association, fromInfo, toInfo)}: {CleanErrorMessage(errorMessage)}");
			}
		}
		if (failures.Count > 0)
		{
			string displayedFailures = string.Join("\n", failures.Take(8));
			string additionalFailureText = failures.Count > 8 ? $"\n\n{failures.Count - 8} more association failure(s) were not shown." : string.Empty;
			DialogService.Show(
				$"Template was placed, but it is incomplete.\n\n{failures.Count} configured association(s) could not be created. Inspect the newly placed features and verify their associations before continuing.\n\nFailed association(s):\n{displayedFailures}{additionalFailureText}",
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
				Name = "Create template association"
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

	private static Task PopulatePlacedFeatureDetails(List<PlacedFeatureContext> createdFeatures)
	{
		if (createdFeatures == null || createdFeatures.Count == 0)
		{
			return Task.CompletedTask;
		}
		foreach (PlacedFeatureContext createdFeature in createdFeatures)
		{
			createdFeature.Layer = GetFeatureLayerByName(createdFeature.Template.SubtypeLayer, createdFeature.Template.GroupLayer);
			createdFeature.ObjectID = createdFeature.Token.ObjectID.GetValueOrDefault();
		}
		return Task.CompletedTask;
	}

	private static async Task CreateTableRowWithAutoAssociationAsync(SimpleTemplate template, Geometry sketchGeometry, EditOperation operation, PlacementOptions options)
	{
		if (!options.IncludeConfiguredAssociations)
		{
			if (!ConfirmCreateNonSpatialWithoutAssociations(template))
			{
				throw new OperationCanceledException();
			}
			await CreateFeatureOrRowFromSimpleTemplate(template, sketchGeometry, operation, options.IncludeDefaultAttributes);
			return;
		}

		List<(FeatureLayer Layer, long ObjectID, string Label, string OwningGroup)> selectedCandidates = await GetSelectedFeaturesForTableAssociationAsync();
		if (selectedCandidates.Count == 0)
		{
			if (!ConfirmCreateNonSpatialWithoutAssociations(template))
			{
				throw new OperationCanceledException();
			}
			await CreateFeatureOrRowFromSimpleTemplate(template, sketchGeometry, operation, options.IncludeDefaultAttributes);
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
			await CreateFeatureOrRowFromSimpleTemplate(template, sketchGeometry, operation, options.IncludeDefaultAttributes);
			return;
		}

		string associationSummary = candidatesWithRules.Count == 1
			? $"{candidatesWithRules[0].Candidate.Label}:\n{BuildTableAssociationSummary(candidatesWithRules[0].Rules)}"
			: string.Join("\n", candidatesWithRules.Select(pair => $"{pair.Candidate.Label}:\n{BuildTableAssociationSummary(pair.Rules)}"));

		MessageBoxResult result = DialogService.Show(
			$"Create the following associations?\n\n{associationSummary}",
			"Template Editor",
			MessageBoxButton.YesNo);

		if (result != MessageBoxResult.Yes && !ConfirmCreateNonSpatialWithoutAssociations(template))
		{
			throw new OperationCanceledException();
		}

		RowToken rowToken = await CreateFeatureOrRowFromSimpleTemplate(template, sketchGeometry, operation, options.IncludeDefaultAttributes);

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
			MessageBoxButton.YesNo);
		return result == MessageBoxResult.Yes;
	}

	private static bool ConfirmCreateNonSpatialTemplate(SimpleTemplate template)
	{
		MessageBoxResult result = DialogService.Show(
			$"Template '{template?.Name}' creates a non-spatial record.\n\nContinue?",
			"Template Editor",
			MessageBoxButton.YesNo);
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
			"CONTAINMENT" => new AssociationDescription((AssociationType)2, fromHandle, toHandle, isReversed),
			"ATTACHMENT" => new AssociationDescription((AssociationType)3, fromHandle, toHandle),
			"JUNCTIONJUNCTIONCONNECTIVITY" => terminal > 0
				? new AssociationDescription((AssociationType)1, fromHandle, (long)terminal, toHandle)
				: new AssociationDescription((AssociationType)1, fromHandle, toHandle),
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
				string owningGroup = GetOwningGroupName(layer);
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
				MessageBoxResult result = DialogService.Show($"The SJO can be created as attachments for {polesToProcess.Count} selected Pole(s).\nWould you like to do that?", "Template Editor", MessageBoxButton.YesNo);
				if (result == MessageBoxResult.Yes)
				{
					foreach ((FeatureLayer layer, long objectId, string _, string _) in polesToProcess)
					{
						RowHandle poleHandle = new RowHandle((MapMember)(object)layer, objectId);
						RowHandle sjoHandle = new RowHandle(await CreateFeatureOrRowFromSimpleTemplate(template, sketchGeometry, operation, options.IncludeDefaultAttributes));
						AssociationDescription assocDesc = new AssociationDescription((AssociationType)3, poleHandle, sjoHandle);
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
		FeatureLayer poleLayer = GetFeatureLayerByName("Pole", "StructureJunction");
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
				string owningGroup = GetOwningGroupName(layer);
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
		catch
		{
			return false;
		}
		return assetType == 791 || assetType == 793 || assetType == 795 || assetType == 796;
	}

	private static Geometry CreateGeometryForTemplate(SimpleTemplateReference template, Geometry sketchGeometry, double rotationDegrees = 0.0)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Expected O, but got Unknown
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Expected O, but got Unknown
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Expected O, but got Unknown
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

	internal static List<PreviewOverlayGraphic> CreatePreviewGraphics(MapPoint anchorPoint, double rotationDegrees = 0.0)
	{
		DisplayTemplate selectedTemplate = AddinConfiguration.SelectedTemplate;
		string templateName = selectedTemplate?.Name;
		if (anchorPoint == null || string.IsNullOrWhiteSpace(templateName))
		{
			return new List<PreviewOverlayGraphic>();
		}
		CIMSymbolReference pointSymbol = CreatePreviewPointSymbol();
		CIMSymbolReference lineSymbol = SymbolFactory.Instance.ConstructLineSymbol(ColorFactory.Instance.CreateRGBColor(222.0, 123.0, 207.0, 70.0), 2.0, SimpleLineStyle.Dash).MakeSymbolReference();
		CIMSymbolReference polygonSymbol = SymbolFactory.Instance.ConstructPolygonSymbol(ColorFactory.Instance.CreateRGBColor(222.0, 123.0, 207.0, 18.0), SimpleFillStyle.Solid, SymbolFactory.Instance.ConstructStroke(ColorFactory.Instance.CreateRGBColor(0.0, 133.0, 202.0, 70.0), 2.0, SimpleLineStyle.Dash)).MakeSymbolReference();
		List<PreviewOverlayGraphic> graphics = new List<PreviewOverlayGraphic>();
		if (selectedTemplate.IsGroupChild)
		{
			SimpleTemplateReference childTemplateRef = GetGroupChildReference(selectedTemplate);
			SimpleTemplate childTemplate = AddinConfiguration.Templates.SimpleTemplates.FirstOrDefault((SimpleTemplate n) =>
				string.Equals(n.Name, childTemplateRef?.Name, StringComparison.OrdinalIgnoreCase));
			if (childTemplateRef == null || childTemplate == null || !IsFeatureLayerTemplate(childTemplate))
			{
				return graphics;
			}
			AddPreviewGraphicForTemplateReference(graphics, childTemplateRef, childTemplate, anchorPoint, rotationDegrees, pointSymbol, lineSymbol, polygonSymbol, useAllConfiguredGeometry: false);
			return graphics;
		}
		GroupTemplate groupTemplate = AddinConfiguration.Templates?.GroupTemplates?.FirstOrDefault((GroupTemplate n) => n.Name == templateName);
		if (groupTemplate?.SimpleTemplates == null)
		{
			SimpleTemplate simpleTemplate = AddinConfiguration.Templates?.SimpleTemplates?.FirstOrDefault((SimpleTemplate n) => n.Name == templateName);
			if (HasConfiguredSimpleGeometry(simpleTemplate))
			{
				Geometry geometry = CreateGeometryForSimpleTemplate(simpleTemplate, anchorPoint, rotationDegrees);
				AddPreviewGraphic(graphics, geometry, pointSymbol, lineSymbol, polygonSymbol);
			}
			else if (IsSimplePointTemplate(simpleTemplate))
			{
				graphics.Add(new PreviewOverlayGraphic(anchorPoint, pointSymbol));
			}
			return graphics;
		}
		if (!HasConfiguredPlacementGeometry(groupTemplate))
		{
			return graphics;
		}
		foreach (SimpleTemplateReference simpleTemplateRef in groupTemplate.SimpleTemplates)
		{
			SimpleTemplate template = AddinConfiguration.Templates.SimpleTemplates.FirstOrDefault((SimpleTemplate n) => n.Name == simpleTemplateRef.Name);
			if (!IsFeatureLayerTemplate(template))
			{
				continue;
			}
			AddPreviewGraphicForTemplateReference(graphics, simpleTemplateRef, template, anchorPoint, rotationDegrees, pointSymbol, lineSymbol, polygonSymbol, useAllConfiguredGeometry: true);
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
		FeatureLayer layer = GetFeatureLayerByName(template.SubtypeLayer, template.GroupLayer);
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

	private static AssociationDescription CreateAssociationDescription(AssociationObject association, FeatureInfo fromInfo, FeatureInfo toInfo)
	{
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Expected O, but got Unknown
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Expected O, but got Unknown
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Expected O, but got Unknown
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Expected O, but got Unknown
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Expected O, but got Unknown
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Expected O, but got Unknown
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Expected O, but got Unknown
		RowHandle fromHandle = CreateRowHandle(fromInfo);
		RowHandle toHandle = CreateRowHandle(toInfo);
		AssociationDescription description = null;
		switch (association.Type.ToUpper())
		{
		case "CONTAINMENT":
			description = new AssociationDescription((AssociationType)2, fromHandle, toHandle, toInfo.IsSpatialFeature);
			break;
		case "ATTACHMENT":
			description = new AssociationDescription((AssociationType)3, fromHandle, toHandle);
			break;
		case "JUNCTIONJUNCTIONCONNECTIVITY":
			description = ((association.FromTerminal == 0) ? new AssociationDescription((AssociationType)1, fromHandle, toHandle) : new AssociationDescription((AssociationType)1, fromHandle, (long)association.FromTerminal, toHandle));
			break;
		case "JUNCTIONEDGEOBJECTCONNECTIVITYFROMSIDE":
			description = new AssociationDescription((AssociationType)4, fromHandle, toHandle);
			break;
		case "JUNCTIONEDGEOBJECTCONNECTIVITYTOSIDE":
			description = new AssociationDescription((AssociationType)6, fromHandle, toHandle);
			break;
		case "JUNCTIONEDGEOBJECTCONNECTIVITYMIDSPAN":
			description = new AssociationDescription((AssociationType)5, fromHandle, toHandle);
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

	private static async Task<RowToken> CreateFeatureOrRowFromSimpleTemplate(SimpleTemplate template, Geometry geometry, EditOperation operation, bool includeDefaultAttributes, double rotationDegrees = 0.0)
	{
		RowToken token;
		if (template == null)
		{
			throw new InvalidOperationException("Template configuration references a missing simple template.");
		}
		if (AddinConfiguration.GroupFeatureLayerNames.Contains(template.GroupLayer.ToUpperInvariant()))
		{
			FeatureLayer layer = GetFeatureLayerByName(template.SubtypeLayer, template.GroupLayer);
			if (layer == null)
			{
				throw new InvalidOperationException($"Layer '{template.GroupLayer}/{template.SubtypeLayer}' was not found for template '{template.Name}'.");
			}
			token = await CreateFeature(layer, geometry, template, operation, includeDefaultAttributes, rotationDegrees);
		}
		else
		{
			StandaloneTable table = GetTableByName(template.SubtypeLayer, template.GroupLayer);
			if (table == null)
			{
				throw new InvalidOperationException($"Table '{template.GroupLayer}/{template.SubtypeLayer}' was not found for template '{template.Name}'.");
			}
			token = await CreateTableRow(table, template, operation, includeDefaultAttributes);
		}
		return token;
	}

	private static async Task<RowToken> CreateFeature(FeatureLayer layer, Geometry geometry, SimpleTemplate template, EditOperation operation, bool includeDefaultAttributes, double rotationDegrees = 0.0)
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
		Dictionary<string, object> attributes = new Dictionary<string, object> { ["SHAPE"] = geometry };
		foreach (string fieldName in GetAttributeFieldsToApply(defaultFieldValues, subtypeField, includeDefaultAttributes))
		{
			Dictionary<string, object> dictionary = attributes;
			string key = fieldName;
			dictionary[key] = await GetDatabaseFieldValueFromConfigValue(defaultFieldValues, subtype, fields, fieldName);
		}
		return operation.Create((MapMember)(object)layer, attributes);
	}

	private static async Task<RowToken> CreateTableRow(StandaloneTable table, SimpleTemplate template, EditOperation operation, bool includeDefaultAttributes)
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
		Dictionary<string, object> attributes = new Dictionary<string, object>();
		foreach (string fieldName in GetAttributeFieldsToApply(defaultFieldValues, subtypeField, includeDefaultAttributes))
		{
			Dictionary<string, object> dictionary = attributes;
			string key = fieldName;
			dictionary[key] = await GetDatabaseFieldValueFromConfigValue(defaultFieldValues, subtype, fields, fieldName);
		}
		return operation.Create((MapMember)(object)table, attributes);
	}

	private static IEnumerable<string> GetAttributeFieldsToApply(Dictionary<string, object> defaultFieldValues, string subtypeField, bool includeDefaultAttributes)
	{
		if (includeDefaultAttributes)
		{
			return defaultFieldValues.Keys;
		}
		if (string.IsNullOrWhiteSpace(subtypeField))
		{
			return Enumerable.Empty<string>();
		}
		return defaultFieldValues.Keys.Where((string fieldName) => string.Equals(fieldName, subtypeField, StringComparison.OrdinalIgnoreCase));
	}

	private static async Task<object> GetDatabaseFieldValueFromConfigValue(Dictionary<string, object> defaultFieldValues, DataSubtype subtype, List<Field> fields, string fieldName)
	{
		object fieldValue = null;
		object configFieldValue = GetObjectValue(defaultFieldValues[fieldName]);
		Field field = fields.FirstOrDefault((Field n) => n.Name.ToUpper() == fieldName.ToUpper());
		if (field == null)
		{
			throw new InvalidOperationException($"Field '{fieldName}' was not found.");
		}
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
						fieldValue = configFieldValue;
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
					fieldValue = configFieldValue;
				}
			}
		}, TaskCreationOptions.None);
		return fieldValue;
	}

	private static object GetCodedDomainValue(DataDomain domain, object configFieldValue)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		return (domain is CodedValueDomain) ? ((CodedValueDomain)domain).GetCodedValue(configFieldValue.ToString()) : configFieldValue;
	}

	public static async Task<string> ValidateConfiguration()
	{
		string message = null;
		List<string> errors = new List<string>();
		foreach (SimpleTemplate template in AddinConfiguration.Templates.SimpleTemplates)
		{
			string error = ValidateLayerOrTableName(template);
			if (error != null)
			{
				errors.Add(error);
			}
		}
		if (errors.Count == 0)
		{
			foreach (SimpleTemplate template2 in AddinConfiguration.Templates.SimpleTemplates)
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
			foreach (SimpleTemplate template3 in AddinConfiguration.Templates.SimpleTemplates)
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
			List<string> simpleTemplateNames = AddinConfiguration.Templates.SimpleTemplates.Select((SimpleTemplate n) => n.Name.ToUpper()).ToList();
			foreach (GroupTemplate groupTemplate in AddinConfiguration.Templates.GroupTemplates)
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
			foreach (GroupTemplate groupTemplate2 in AddinConfiguration.Templates.GroupTemplates)
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
			foreach (GroupTemplate groupTemplate3 in AddinConfiguration.Templates.GroupTemplates)
			{
				foreach (SimpleTemplateReference templateRef in groupTemplate3.SimpleTemplates)
				{
					string error6 = await ValidateGeometry(template: AddinConfiguration.Templates.SimpleTemplates.FirstOrDefault((SimpleTemplate n) => n.Name == templateRef.Name), groupTemplate: groupTemplate3, templateRef: templateRef);
					if (error6 != null)
					{
						errors.Add(error6);
					}
				}
			}
		}
		if (errors.Count == 0)
		{
			foreach (GroupTemplate groupTemplate4 in AddinConfiguration.Templates.GroupTemplates)
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
			FeatureLayer layer = GetFeatureLayerByName(template.SubtypeLayer, template.GroupLayer);
			if (layer == null)
			{
				error = $"Group layer/subtype layer {template.GroupLayer}/{template.SubtypeLayer} does not exist in the map ({template.Name}).";
			}
		}
		else
		{
			StandaloneTable table = GetTableByName(template.SubtypeLayer, template.GroupLayer);
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
			FeatureLayer layer = GetFeatureLayerByName(template.SubtypeLayer, template.GroupLayer);
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
			StandaloneTable table = GetTableByName(template.SubtypeLayer, template.GroupLayer);
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
			FeatureLayer layer = GetFeatureLayerByName(template.SubtypeLayer, template.GroupLayer);
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
			StandaloneTable table = GetTableByName(template.SubtypeLayer, template.GroupLayer);
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
					Field field = fields.FirstOrDefault((Field n) => n.Name.ToUpper() == fieldName.ToUpper());
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
				Field field2 = fields.FirstOrDefault((Field n) => n.Name.ToUpper() == fieldName2.ToUpper());
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
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Invalid comparison between Unknown and I4
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Invalid comparison between Unknown and I4
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Invalid comparison between Unknown and I4
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Invalid comparison between Unknown and I4
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Invalid comparison between Unknown and I4
		string fieldValue = GetObjectValue(template.DefaultFieldValues[field.Name])?.ToString();
		bool isValid = true;
		if ((int)field.FieldType == 13 || (int)field.FieldType == 1 || (int)field.FieldType == 0)
		{
			isValid = int.TryParse(fieldValue, out var _);
		}
		else if ((int)field.FieldType == 2 || (int)field.FieldType == 3)
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
			FeatureLayer layer = GetFeatureLayerByName(template.SubtypeLayer, template.GroupLayer);
			if (layer == null)
			{
				return $"Group layer/subtype layer {template.GroupLayer}/{template.SubtypeLayer} does not exist in the map ({template.Name}).";
			}
			GeometryType geometryType = (GeometryType)0;
			await QueuedTask.Run((Action)delegate
			{
				//IL_0012: Unknown result type (might be due to invalid IL or missing references)
				//IL_0017: Unknown result type (might be due to invalid IL or missing references)
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
