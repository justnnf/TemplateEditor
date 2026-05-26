using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.UtilityNetwork;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace TemplateEditor;

internal static class AssociationRuleJsonRegenerator
{
	public static async Task<AssociationRuleGenerationResult> RegenerateFromActiveMapAsync(string outputPathOverride = null)
	{
		string outputPath = string.IsNullOrWhiteSpace(outputPathOverride) ? AssociationRuleCatalog.RuleFilePath : outputPathOverride.Trim();
		AssociationRuleEnvelope envelope = await QueuedTask.Run(delegate
		{
			if (MapView.Active?.Map == null)
			{
				throw new InvalidOperationException("No active map was found.");
			}
			foreach (Layer layer in MapView.Active.Map.GetLayersAsFlattenedList())
			{
				AssociationRuleEnvelope result = TryBuildRuleEnvelope(layer);
				if (result != null)
				{
					return result;
				}
			}
			throw new InvalidOperationException("No utility network was found in the active map.");
		});
		Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
		File.WriteAllText(outputPath, JsonSerializer.Serialize(envelope, new JsonSerializerOptions
		{
			WriteIndented = true
		}) + Environment.NewLine);
		AssociationRuleCatalog.Reload();
		return new AssociationRuleGenerationResult
		{
			OutputPath = outputPath,
			RuleCount = envelope.Rules?.Count ?? 0
		};
	}

	private static AssociationRuleEnvelope TryBuildRuleEnvelope(Layer layer)
	{
		using UtilityNetwork utilityNetwork = TryGetUtilityNetwork(layer);
		if (utilityNetwork == null)
		{
			return null;
		}
		using UtilityNetworkDefinition definition = utilityNetwork.GetDefinition();
		List<AssociationRule> rules = definition.GetRules()
			.Where((Rule rule) => rule.Type == RuleType.Attachment || rule.Type == RuleType.Containment || rule.Type == RuleType.JunctionJunctionConnectivity)
			.Select(ToAssociationRule)
			.Where((AssociationRule rule) => rule != null)
			.GroupBy(GetRuleKey)
			.Select((IGrouping<string, AssociationRule> group) => group.First())
			.OrderBy((AssociationRule rule) => rule.AssociationType)
			.ThenBy((AssociationRule rule) => rule.FromTable)
			.ThenBy((AssociationRule rule) => rule.FromAssetGroup)
			.ThenBy((AssociationRule rule) => rule.FromAssetType)
			.ThenBy((AssociationRule rule) => rule.ToTable)
			.ThenBy((AssociationRule rule) => rule.ToAssetGroup)
			.ThenBy((AssociationRule rule) => rule.ToAssetType)
			.ToList();
		return new AssociationRuleEnvelope
		{
			Source = definition.GetName() + " / UtilityNetworkDefinition.GetRules",
			GeneratedUtc = DateTimeOffset.UtcNow.ToString("O"),
			Rules = rules
		};
	}

	private static UtilityNetwork TryGetUtilityNetwork(Layer layer)
	{
		if (layer is UtilityNetworkLayer utilityNetworkLayer)
		{
			return utilityNetworkLayer.GetUtilityNetwork();
		}
		if (layer is SubtypeGroupLayer subtypeGroupLayer)
		{
			foreach (Layer childLayer in subtypeGroupLayer.Layers)
			{
				UtilityNetwork utilityNetwork = TryGetUtilityNetwork(childLayer);
				if (utilityNetwork != null)
				{
					return utilityNetwork;
				}
			}
		}
		if (layer is FeatureLayer featureLayer)
		{
			using FeatureClass featureClass = featureLayer.GetFeatureClass();
			return TryGetControllerUtilityNetwork(featureClass);
		}
		return null;
	}

	private static UtilityNetwork TryGetControllerUtilityNetwork(FeatureClass featureClass)
	{
		if (featureClass == null || !featureClass.IsControllerDatasetSupported())
		{
			return null;
		}
		foreach (Dataset controllerDataset in featureClass.GetControllerDatasets())
		{
			if (controllerDataset is UtilityNetwork utilityNetwork)
			{
				return utilityNetwork;
			}
			controllerDataset.Dispose();
		}
		return null;
	}

	private static AssociationRule ToAssociationRule(Rule rule)
	{
		IReadOnlyList<RuleElement> elements = rule.RuleElements;
		if (elements == null || elements.Count < 2)
		{
			return null;
		}
		RuleElement from = elements[0];
		RuleElement to = elements[1];
		return new AssociationRule
		{
			AssociationType = GetAssociationTypeName(rule.Type),
			FromTable = from.NetworkSource?.Name ?? string.Empty,
			FromAssetGroup = from.AssetGroup?.Name ?? string.Empty,
			FromAssetType = from.AssetType?.Name ?? string.Empty,
			ToTable = to.NetworkSource?.Name ?? string.Empty,
			ToAssetGroup = to.AssetGroup?.Name ?? string.Empty,
			ToAssetType = to.AssetType?.Name ?? string.Empty
		};
	}

	private static string GetAssociationTypeName(RuleType ruleType)
	{
		if (ruleType == RuleType.Attachment)
		{
			return "Attachment";
		}
		if (ruleType == RuleType.Containment)
		{
			return "Containment";
		}
		if (ruleType == RuleType.JunctionJunctionConnectivity)
		{
			return "JunctionJunctionConnectivity";
		}
		return Convert.ToString(ruleType);
	}

	private static string GetRuleKey(AssociationRule rule)
	{
		return string.Join("|",
			Normalize(rule.AssociationType),
			Normalize(rule.FromTable),
			Normalize(rule.FromAssetGroup),
			Normalize(rule.FromAssetType),
			Normalize(rule.ToTable),
			Normalize(rule.ToAssetGroup),
			Normalize(rule.ToAssetType));
	}

	private static string Normalize(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}
		return value.Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty).ToUpperInvariant();
	}
}

internal sealed class AssociationRuleGenerationResult
{
	public string OutputPath { get; set; }

	public int RuleCount { get; set; }
}
