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

	public static async Task<int?> ChooseTerminalAsync(FeatureLayer snappedLayer, long snappedObjectId, string endName)
	{
		SimpleTemplate template = GetLineTemplate();
		if (template == null || snappedLayer == null || snappedObjectId <= 0)
		{
			return null;
		}
		DeviceChoice device;
		try
		{
			device = await QueuedTask.Run(() => FindDevice(snappedLayer, snappedObjectId, template));
		}
		catch (Exception exception)
		{
			LogService.LogException("Could not inspect devices and terminals at the line endpoint.", exception);
			DialogService.ShowToast("Terminal choices could not be loaded. The line can still be placed without an endpoint terminal.", "Template Editor", FeedbackSeverity.Warning);
			return null;
		}
		if (device == null)
		{
			return null;
		}
		List<(int Id, string Name)> terminals = FilterTerminals(device.Terminals, AddinConfiguration.Settings);
		if (terminals.Count == 0)
		{
			return 0; // The Utility Network terminal domain uses 0 for None.
		}
		List<string> labels = terminals.Select(t => t.Name).ToList();
		labels.Add("None (no terminal)");
		int? terminalIndex = Choose($"{endName} terminal", $"Choose the terminal on {device.Label} for the {endName.ToLowerInvariant()} end.", labels);
		return terminalIndex.HasValue ? (terminalIndex.Value == terminals.Count ? 0 : terminals[terminalIndex.Value].Id) : null;
	}

	internal static List<(int Id, string Name)> FilterTerminals(IEnumerable<(int Id, string Name)> terminals, TemplateEditorSettings settings)
	{
		HashSet<string> ignored = new HashSet<string>(settings?.IgnoredLineTerminalNames ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
		HashSet<string> allowed = new HashSet<string>(settings?.AllowedLineTerminalNames ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
		return terminals.Where(t => !ignored.Contains(t.Name) && (allowed.Count == 0 || allowed.Contains(t.Name))).ToList();
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

	private static DeviceChoice FindDevice(FeatureLayer snappedLayer, long snappedObjectId, SimpleTemplate template)
	{
		FeatureLayer lineLayer = MapMemberLookupService.GetFeatureLayerByName(template.SubtypeLayer, template.GroupLayer);
		if (lineLayer == null)
		{
			return null;
		}
		using FeatureClass lineClass = lineLayer.GetFeatureClass();
		using FeatureClassDefinition lineDefinition = lineClass?.GetDefinition();
		if (lineDefinition?.GetShapeType() != GeometryType.Polyline ||
			!lineDefinition.GetFields().Any(f => string.Equals(f.Name, "FROMDEVICETERMINAL", StringComparison.OrdinalIgnoreCase)) ||
			!lineDefinition.GetFields().Any(f => string.Equals(f.Name, "TODEVICETERMINAL", StringComparison.OrdinalIgnoreCase)))
		{
			return null;
		}
		UtilityNetwork network = null;
		try
		{
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
		}
		catch (Exception exception)
		{
			LogService.LogException("Could not get the line layer's utility network controller; checking map layers.", exception);
		}
		// Service-backed feature classes may not expose their controller dataset.
		// The Utility Network layer in the map provides the same rule definition.
		if (network == null)
		{
			foreach (UtilityNetworkLayer networkLayer in MapView.Active?.Map.GetLayersAsFlattenedList().OfType<UtilityNetworkLayer>() ?? Enumerable.Empty<UtilityNetworkLayer>())
			{
				try
				{
					network = networkLayer.GetUtilityNetwork();
					if (network != null)
					{
						break;
					}
				}
				catch (Exception exception)
				{
					LogService.LogException($"Could not read utility network layer '{networkLayer.Name}'.", exception);
				}
			}
		}
		if (network == null)
		{
			return null;
		}
		using (network)
		using (UtilityNetworkDefinition definition = network.GetDefinition())
		{
			string lineClassName = ((Dataset)lineClass).GetName();
			List<Rule> rules = definition.GetRules().Where(rule => rule.Type == RuleType.JunctionEdgeConnectivity && rule.RuleElements.Count >= 2 &&
				SameSource(rule.RuleElements[1].NetworkSource?.Name, lineClassName, lineDefinition.GetAliasName(), lineLayer.Name, (lineLayer.Parent as MapMember)?.Name, template.GroupLayer) &&
				Matches(template.DefaultFieldValues, "ASSETGROUP", rule.RuleElements[1].AssetGroup?.Name, rule.RuleElements[1].AssetGroup?.Code) &&
				Matches(template.DefaultFieldValues, "ASSETTYPE", rule.RuleElements[1].AssetType?.Name, rule.RuleElements[1].AssetType?.Code) &&
				rule.RuleElements[0].Terminal != null).ToList();
			if (rules.Count == 0)
			{
				return null;
			}
			using FeatureClass featureClass = snappedLayer.GetFeatureClass();
			if (featureClass == null)
			{
				return null;
			}
			using FeatureClassDefinition featureDefinition = featureClass.GetDefinition();
			if (featureDefinition.GetShapeType() != GeometryType.Point)
			{
				return null;
			}
			string sourceName = ((Dataset)featureClass).GetName();
			List<Rule> sourceRules = rules.Where(rule => SameSource(rule.RuleElements[0].NetworkSource?.Name, sourceName, featureDefinition.GetAliasName(), snappedLayer.Name, (snappedLayer.Parent as MapMember)?.Name)).ToList();
			if (sourceRules.Count == 0)
			{
				return null;
			}
			QueryFilter filter = new QueryFilter { ObjectIDs = new List<long> { snappedObjectId } };
			using RowCursor cursor = ((BasicFeatureLayer)snappedLayer).Search(filter);
			if (!cursor.MoveNext())
			{
				return null;
			}
			using Feature feature = (Feature)cursor.Current;
			int group = Convert.ToInt32(feature["ASSETGROUP"], CultureInfo.InvariantCulture);
			int type = Convert.ToInt32(feature["ASSETTYPE"], CultureInfo.InvariantCulture);
			List<Rule> deviceRules = sourceRules.Where(rule => rule.RuleElements[0].AssetGroup?.Code == group && rule.RuleElements[0].AssetType?.Code == type).ToList();
			if (deviceRules.Count == 0)
			{
				return null;
			}
			DeviceChoice device = new DeviceChoice { Label = $"{snappedLayer.Name}: {deviceRules[0].RuleElements[0].AssetGroup?.Name} / {deviceRules[0].RuleElements[0].AssetType?.Name} (OID {snappedObjectId})" };
			device.Terminals.AddRange(deviceRules.Select(rule => (rule.RuleElements[0].Terminal.ID, rule.RuleElements[0].Terminal.Name)).Distinct().OrderBy(t => t.Name));
			return device;
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

	internal static bool SameSource(string networkName, params string[] names)
	{
		string normalizedNetworkName = NormalizeSourceName(networkName);
		return normalizedNetworkName.Length > 0 && names.Any(name => NormalizeSourceName(name) == normalizedNetworkName);
	}

	private static string NormalizeSourceName(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return string.Empty;
		}
		string unqualified = name.Substring(name.LastIndexOf('.') + 1);
		return new string(unqualified.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
	}
}
