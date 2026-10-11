using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ArcGIS.Core;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.UtilityNetwork;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace TemplateEditor;

internal static class LineTerminalPromptService
{
	private sealed class DeviceChoice
	{
		public string Label { get; set; }
		public List<(int Id, string Name)> Terminals { get; } = new List<(int, string)>();
	}

	public static bool HasEligibleTemplate()
	{
		return GetLineTemplate() != null;
	}

	public static async Task<int?> ChooseTerminalAsync(MapPoint endpoint, string endName)
	{
		SimpleTemplate template = GetLineTemplate();
		if (template == null || endpoint == null)
		{
			return null;
		}
		List<DeviceChoice> devices;
		try
		{
			devices = await QueuedTask.Run(() => FindDevices(endpoint, template));
		}
		catch (Exception exception)
		{
			LogService.LogException("Could not inspect devices and terminals at the line endpoint.", exception);
			DialogService.ShowToast("Terminal choices could not be loaded. The line can still be placed without an endpoint terminal.", "Template Editor", FeedbackSeverity.Warning);
			return null;
		}
		if (devices.Count == 0)
		{
			return null;
		}
		DeviceChoice device = devices[0];
		if (devices.Count > 1)
		{
			int? index = Choose($"{endName} device", $"Choose the device at the {endName.ToLowerInvariant()} end of the line.", devices.Select(d => d.Label).ToList());
			if (!index.HasValue)
			{
				return null;
			}
			device = devices[index.Value];
		}
		int? terminalIndex = Choose($"{endName} terminal", $"Choose the terminal on {device.Label} for the {endName.ToLowerInvariant()} end.", device.Terminals.Select(t => t.Name).ToList());
		return terminalIndex.HasValue ? device.Terminals[terminalIndex.Value].Id : null;
	}

	private static int? Choose(string title, string prompt, IReadOnlyList<string> labels)
	{
		CandidateChoiceDialog dialog = new CandidateChoiceDialog(title, prompt, labels);
		if (Application.Current?.MainWindow is Window owner)
		{
			dialog.Owner = owner;
		}
		dialog.ShowDialog();
		return dialog.Result == CandidateChoiceResult.UseCandidate ? dialog.SelectedIndex : null;
	}

	private static SimpleTemplate GetLineTemplate()
	{
		DisplayTemplate selected = AddinConfiguration.SelectedTemplate;
		TemplateConfig config = AddinConfiguration.Templates;
		if (selected == null || config == null)
		{
			return null;
		}
		SimpleTemplate template;
		if (selected.IsGroupChild)
		{
			template = config.SimpleTemplates?.FirstOrDefault(t => string.Equals(t.Name, selected.Name, StringComparison.OrdinalIgnoreCase));
		}
		else
		{
			template = config.SimpleTemplates?.FirstOrDefault(t => string.Equals(t.Name, selected.Name, StringComparison.OrdinalIgnoreCase));
			if (template == null)
			{
				SimpleTemplateReference first = config.GroupTemplates?.FirstOrDefault(g => string.Equals(g.Name, selected.Name, StringComparison.OrdinalIgnoreCase))?.SimpleTemplates?.FirstOrDefault(r => r.FeatureId == 1);
				if (first == null || first.Line != null || first.Location != null || first.Polygon != null)
				{
					return null;
				}
				template = config.SimpleTemplates?.FirstOrDefault(t => string.Equals(t.Name, first.Name, StringComparison.OrdinalIgnoreCase));
			}
		}
		return template != null && AddinConfiguration.GroupFeatureLayerNames.Contains(template.GroupLayer?.ToUpperInvariant() ?? string.Empty) ? template : null;
	}

	private static List<DeviceChoice> FindDevices(MapPoint endpoint, SimpleTemplate template)
	{
		FeatureLayer lineLayer = MapMemberLookupService.GetFeatureLayerByName(template.SubtypeLayer, template.GroupLayer);
		if (lineLayer == null)
		{
			return new List<DeviceChoice>();
		}
		using FeatureClass lineClass = lineLayer.GetFeatureClass();
		using FeatureClassDefinition lineDefinition = lineClass?.GetDefinition();
		if (lineDefinition?.GetShapeType() != GeometryType.Polyline ||
			!lineDefinition.GetFields().Any(f => string.Equals(f.Name, "FROMDEVICETERMINAL", StringComparison.OrdinalIgnoreCase)) ||
			!lineDefinition.GetFields().Any(f => string.Equals(f.Name, "TODEVICETERMINAL", StringComparison.OrdinalIgnoreCase)))
		{
			return new List<DeviceChoice>();
		}
		UtilityNetwork network = null;
		if (((Table)lineClass).IsControllerDatasetSupported())
		{
			foreach (Dataset controller in ((Table)lineClass).GetControllerDatasets())
			{
				if (controller is UtilityNetwork utilityNetwork && network == null)
				{
					network = utilityNetwork;
				}
				else
				{
					((CoreObjectsBase)controller).Dispose();
				}
			}
		}
		if (network == null)
		{
			return new List<DeviceChoice>();
		}
		using (network)
		using (UtilityNetworkDefinition definition = network.GetDefinition())
		{
			string lineClassName = ((Dataset)lineClass).GetName();
			List<Rule> rules = definition.GetRules().Where(rule => rule.Type == RuleType.JunctionEdgeConnectivity && rule.RuleElements.Count >= 2 &&
				SameSource(rule.RuleElements[1].NetworkSource?.Name, lineClassName) &&
				Matches(template.DefaultFieldValues, "ASSETGROUP", rule.RuleElements[1].AssetGroup?.Name, rule.RuleElements[1].AssetGroup?.Code) &&
				Matches(template.DefaultFieldValues, "ASSETTYPE", rule.RuleElements[1].AssetType?.Name, rule.RuleElements[1].AssetType?.Code) &&
				rule.RuleElements[0].Terminal != null).ToList();
			if (rules.Count == 0)
			{
				return new List<DeviceChoice>();
			}
			List<DeviceChoice> results = new List<DeviceChoice>();
			HashSet<string> seenDevices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (FeatureLayer layer in MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>())
			{
				using FeatureClass featureClass = layer.GetFeatureClass();
				if (featureClass == null)
				{
					continue;
				}
				using FeatureClassDefinition featureDefinition = featureClass.GetDefinition();
				if (featureDefinition.GetShapeType() != GeometryType.Point)
				{
					continue;
				}
				string sourceName = ((Dataset)featureClass).GetName();
				List<Rule> sourceRules = rules.Where(rule => SameSource(rule.RuleElements[0].NetworkSource?.Name, sourceName)).ToList();
				if (sourceRules.Count == 0)
				{
					continue;
				}
				Geometry searchPoint = GeometryEngine.Instance.Project(endpoint, featureDefinition.GetSpatialReference());
				SpatialQueryFilter filter = new SpatialQueryFilter { FilterGeometry = searchPoint, SpatialRelationship = SpatialRelationship.Intersects };
				using RowCursor cursor = ((BasicFeatureLayer)layer).Search(filter);
				while (cursor.MoveNext())
				{
					using Feature feature = (Feature)cursor.Current;
					int group = Convert.ToInt32(feature["ASSETGROUP"], CultureInfo.InvariantCulture);
					int type = Convert.ToInt32(feature["ASSETTYPE"], CultureInfo.InvariantCulture);
					List<Rule> deviceRules = sourceRules.Where(rule => rule.RuleElements[0].AssetGroup?.Code == group && rule.RuleElements[0].AssetType?.Code == type).ToList();
					if (deviceRules.Count == 0)
					{
						continue;
					}
					if (!seenDevices.Add(sourceName + ":" + feature.GetObjectID().ToString(CultureInfo.InvariantCulture)))
					{
						continue;
					}
					DeviceChoice device = new DeviceChoice { Label = $"{layer.Name}: {deviceRules[0].RuleElements[0].AssetGroup?.Name} / {deviceRules[0].RuleElements[0].AssetType?.Name} (OID {feature.GetObjectID()})" };
					device.Terminals.AddRange(deviceRules.Select(rule => (rule.RuleElements[0].Terminal.ID, rule.RuleElements[0].Terminal.Name)).Distinct().OrderBy(t => t.Name));
					results.Add(device);
				}
			}
			return results;
		}
	}

	private static bool Matches(Dictionary<string, object> values, string field, string name, int? code)
	{
		if (values == null || !values.TryGetValue(field, out object raw) || code == null)
		{
			return false;
		}
		string value = Convert.ToString(CommonFunctions.GetObjectValue(raw), CultureInfo.InvariantCulture);
		return string.Equals(value, name, StringComparison.OrdinalIgnoreCase) || string.Equals(value, code.Value.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
	}

	private static bool SameSource(string networkName, string className)
	{
		return !string.IsNullOrEmpty(networkName) && !string.IsNullOrEmpty(className) &&
			(string.Equals(networkName, className, StringComparison.OrdinalIgnoreCase) || className.EndsWith("." + networkName, StringComparison.OrdinalIgnoreCase));
	}
}
