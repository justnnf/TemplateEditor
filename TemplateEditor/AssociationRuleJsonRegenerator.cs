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
		AssociationRuleEnvelope envelope = await QueuedTask.Run<AssociationRuleEnvelope>((Func<AssociationRuleEnvelope>)delegate
		{
			MapView active = MapView.Active;
			if (((active != null) ? active.Map : null) == null)
			{
				throw new InvalidOperationException("No active map was found.");
			}
			foreach (Layer layersAsFlattened in MapView.Active.Map.GetLayersAsFlattenedList())
			{
				AssociationRuleEnvelope associationRuleEnvelope = TryBuildRuleEnvelope(layersAsFlattened);
				if (associationRuleEnvelope != null)
				{
					return associationRuleEnvelope;
				}
			}
			throw new InvalidOperationException("No utility network was found in the active map.");
		}, TaskCreationOptions.None);
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
		UtilityNetwork val = TryGetUtilityNetwork(layer);
		try
		{
			if (val == null)
			{
				return null;
			}
			UtilityNetworkDefinition definition = val.GetDefinition();
			try
			{
				List<AssociationRule> rules = (from @group in (from rule in definition.GetRules().Where(delegate(Rule rule)
						{
							//IL_0001: Unknown result type (might be due to invalid IL or missing references)
							//IL_0007: Invalid comparison between Unknown and I4
							//IL_000a: Unknown result type (might be due to invalid IL or missing references)
							//IL_0010: Invalid comparison between Unknown and I4
							//IL_0013: Unknown result type (might be due to invalid IL or missing references)
							//IL_0019: Invalid comparison between Unknown and I4
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
			((IDisposable)val)?.Dispose();
		}
	}

	private static UtilityNetwork TryGetUtilityNetwork(Layer layer)
	{
		UtilityNetworkLayer val = (UtilityNetworkLayer)(object)((layer is UtilityNetworkLayer) ? layer : null);
		if (val != null)
		{
			return val.GetUtilityNetwork();
		}
		SubtypeGroupLayer val2 = (SubtypeGroupLayer)(object)((layer is SubtypeGroupLayer) ? layer : null);
		if (val2 != null)
		{
			foreach (Layer layer2 in ((CompositeLayer)val2).Layers)
			{
				UtilityNetwork val3 = TryGetUtilityNetwork(layer2);
				if (val3 != null)
				{
					return val3;
				}
			}
		}
		FeatureLayer val4 = (FeatureLayer)(object)((layer is FeatureLayer) ? layer : null);
		if (val4 != null)
		{
			FeatureClass featureClass = val4.GetFeatureClass();
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
			UtilityNetwork val = (UtilityNetwork)(object)((controllerDataset is UtilityNetwork) ? controllerDataset : null);
			if (val != null)
			{
				return val;
			}
			((CoreObjectsBase)controllerDataset).Dispose();
		}
		return null;
	}

	private static AssociationRule ToAssociationRule(Rule rule)
	{
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		IReadOnlyList<RuleElement> ruleElements = rule.RuleElements;
		if (ruleElements == null || ruleElements.Count < 2)
		{
			return null;
		}
		RuleElement val = ruleElements[0];
		RuleElement val2 = ruleElements[1];
		AssociationRule obj = new AssociationRule
		{
			AssociationType = GetAssociationTypeName(rule.Type)
		};
		NetworkSource networkSource = val.NetworkSource;
		obj.FromTable = ((networkSource != null) ? networkSource.Name : null) ?? string.Empty;
		AssetGroup assetGroup = val.AssetGroup;
		obj.FromAssetGroup = ((assetGroup != null) ? assetGroup.Name : null) ?? string.Empty;
		AssetType assetType = val.AssetType;
		obj.FromAssetType = ((assetType != null) ? assetType.Name : null) ?? string.Empty;
		NetworkSource networkSource2 = val2.NetworkSource;
		obj.ToTable = ((networkSource2 != null) ? networkSource2.Name : null) ?? string.Empty;
		AssetGroup assetGroup2 = val2.AssetGroup;
		obj.ToAssetGroup = ((assetGroup2 != null) ? assetGroup2.Name : null) ?? string.Empty;
		AssetType assetType2 = val2.AssetType;
		obj.ToAssetType = ((assetType2 != null) ? assetType2.Name : null) ?? string.Empty;
		return obj;
	}

	private static string GetAssociationTypeName(RuleType ruleType)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0003: Invalid comparison between Unknown and I4
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Invalid comparison between Unknown and I4
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Invalid comparison between Unknown and I4
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
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
