using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ArcGIS.Core;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.UtilityNetwork;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace TemplateEditor;

internal static class AssociationRuleJsonRegenerator
{
	public static async Task<AssociationRuleGenerationResult> RegenerateFromActiveMapAsync(string outputPathOverride = null)
	{
		string outputPath = AtomicFileService.NormalizeJsonFilePath(string.IsNullOrWhiteSpace(outputPathOverride) ? AssociationRuleCatalog.RuleFilePath : outputPathOverride);
		// Utility Network datasets and rules are ArcGIS Core objects, so discover
		// and project them to plain JSON models entirely on the MCT.
		AssociationRuleEnvelope envelope = await QueuedTask.Run(() =>
		{
			Map map = MapView.Active?.Map;
			if (map == null)
			{
				throw new InvalidOperationException("No active map was found.");
			}
			foreach (Layer layer in map.GetLayersAsFlattenedList())
			{
				AssociationRuleEnvelope ruleEnvelope = TryBuildRuleEnvelope(layer);
				if (ruleEnvelope != null)
				{
					return ruleEnvelope;
				}
			}
			throw new InvalidOperationException("No utility network was found in the active map.");
		});
		AtomicFileService.WriteAllText(outputPath, JsonSerializer.Serialize(envelope, new JsonSerializerOptions
		{
			WriteIndented = true
		}) + Environment.NewLine);
		AssociationRuleCatalog.Reload();
		return new AssociationRuleGenerationResult
		{
			OutputPath = outputPath,
			RuleCount = (envelope.Rules?.Count ?? 0)
		};
	}

	private static AssociationRuleEnvelope TryBuildRuleEnvelope(Layer layer)
	{
		UtilityNetwork utilityNetwork = TryGetUtilityNetwork(layer);
		try
		{
			if (utilityNetwork == null)
			{
				return null;
			}
			UtilityNetworkDefinition definition = utilityNetwork.GetDefinition();
			try
			{
				List<AssociationRule> rules = (from @group in (from rule in definition.GetRules().Where(delegate (Rule rule)
						{
							return (int)rule.Type == 3 || (int)rule.Type == 2 || (int)rule.Type == 1;
						}).Select(ToAssociationRule)
															   where rule != null
															   select rule).GroupBy(GetRuleKey)
											   select @group.First() into rule
											   orderby rule.AssociationType, rule.FromTable, rule.FromAssetGroup, rule.FromAssetType, rule.ToTable, rule.ToAssetGroup, rule.ToAssetType
											   select rule).ToList();
				return new AssociationRuleEnvelope
				{
					Source = ((Definition)definition).GetName() + " / UtilityNetworkDefinition.GetRules",
					GeneratedUtc = DateTimeOffset.UtcNow.ToString("O"),
					Rules = rules
				};
			}
			finally
			{
				((IDisposable)definition)?.Dispose();
			}
		}
		finally
		{
			utilityNetwork?.Dispose();
		}
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
			FeatureClass featureClass = featureLayer.GetFeatureClass();
			try
			{
				return TryGetControllerUtilityNetwork(featureClass);
			}
			finally
			{
				((IDisposable)featureClass)?.Dispose();
			}
		}
		return null;
	}

	private static UtilityNetwork TryGetControllerUtilityNetwork(FeatureClass featureClass)
	{
		if (featureClass == null || !((Table)featureClass).IsControllerDatasetSupported())
		{
			return null;
		}
		foreach (Dataset controllerDataset in ((Table)featureClass).GetControllerDatasets())
		{
			if (controllerDataset is UtilityNetwork utilityNetwork)
			{
				return utilityNetwork;
			}
			((CoreObjectsBase)controllerDataset).Dispose();
		}
		return null;
	}

	private static AssociationRule ToAssociationRule(Rule rule)
	{
		IReadOnlyList<RuleElement> ruleElements = rule.RuleElements;
		if (ruleElements == null || ruleElements.Count < 2)
		{
			return null;
		}
		RuleElement fromElement = ruleElements[0];
		RuleElement toElement = ruleElements[1];
		AssociationRule associationRule = new AssociationRule
		{
			AssociationType = GetAssociationTypeName(rule.Type),
			FromTable = fromElement.NetworkSource?.Name ?? string.Empty,
			FromAssetGroup = fromElement.AssetGroup?.Name ?? string.Empty,
			FromAssetType = fromElement.AssetType?.Name ?? string.Empty,
			ToTable = toElement.NetworkSource?.Name ?? string.Empty,
			ToAssetGroup = toElement.AssetGroup?.Name ?? string.Empty,
			ToAssetType = toElement.AssetType?.Name ?? string.Empty
		};
		return associationRule;
	}

	private static string GetAssociationTypeName(RuleType ruleType)
	{
		if ((int)ruleType == 3)
		{
			return "Attachment";
		}
		if ((int)ruleType == 2)
		{
			return "Containment";
		}
		if ((int)ruleType == 1)
		{
			return "JunctionJunctionConnectivity";
		}
		return Convert.ToString(ruleType);
	}

	private static string GetRuleKey(AssociationRule rule)
	{
		return string.Join("|", Normalize(rule.AssociationType), Normalize(rule.FromTable), Normalize(rule.FromAssetGroup), Normalize(rule.FromAssetType), Normalize(rule.ToTable), Normalize(rule.ToAssetGroup), Normalize(rule.ToAssetType));
	}

	private static string Normalize(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}
		return value.Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty)
			.ToUpperInvariant();
	}
}
