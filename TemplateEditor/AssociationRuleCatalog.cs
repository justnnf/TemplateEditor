using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using ArcGIS.Core.Data.UtilityNetwork;

namespace TemplateEditor;

internal sealed class AssociationRuleCatalog
{
	private static AssociationRuleCatalog _current;

	private readonly List<AssociationRule> _rules;

	private AssociationRuleCatalog(List<AssociationRule> rules)
	{
		_rules = rules ?? new List<AssociationRule>();
	}

	public static AssociationRuleCatalog Current => _current ??= Load();

	public bool HasRules => _rules.Count > 0;

	public static string RuleFilePath => ResolveRuleFilePath(preferExisting: true);

	public static void Reload()
	{
		_current = Load();
	}

	public bool Allows(AssociationType associationType, FeatureLayerInfo containerOrTarget, FeatureLayerInfo contentOrCreated)
	{
		if (!HasRules || containerOrTarget == null || contentOrCreated == null)
		{
			return true;
		}
		string normalizedType = GetRuleAssociationTypeName(associationType);
		if (normalizedType == null)
		{
			return true;
		}
		return _rules.Any((AssociationRule rule) =>
			string.Equals(rule.AssociationType, normalizedType, StringComparison.OrdinalIgnoreCase) &&
			Matches(rule.FromTable, containerOrTarget.TableName) &&
			Matches(rule.FromAssetGroup, containerOrTarget.AssetGroup) &&
			Matches(rule.FromAssetType, containerOrTarget.AssetType) &&
			Matches(rule.ToTable, contentOrCreated.TableName) &&
			Matches(rule.ToAssetGroup, contentOrCreated.AssetGroup) &&
			Matches(rule.ToAssetType, contentOrCreated.AssetType));
	}

	private static string GetRuleAssociationTypeName(AssociationType associationType)
	{
		if (associationType == AssociationType.Attachment)
		{
			return "Attachment";
		}
		if (associationType == AssociationType.Containment)
		{
			return "Containment";
		}
		if (associationType == UtilityNetworkAssociationTypes.JunctionJunctionConnectivity)
		{
			return "JunctionJunctionConnectivity";
		}
		return null;
	}

	private static bool Matches(string ruleValue, string actualValue)
	{
		return string.IsNullOrWhiteSpace(ruleValue) ||
			string.Equals(Normalize(ruleValue), Normalize(actualValue), StringComparison.OrdinalIgnoreCase);
	}

	private static string Normalize(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}
		return value.Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty).ToUpperInvariant();
	}

	private static AssociationRuleCatalog Load()
	{
		string path = ResolveRuleFilePath(preferExisting: true);
		try
		{
			if (!File.Exists(path))
			{
				return new AssociationRuleCatalog(new List<AssociationRule>());
			}
			AssociationRuleEnvelope envelope = JsonSerializer.Deserialize<AssociationRuleEnvelope>(File.ReadAllText(path), new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			});
			return new AssociationRuleCatalog(envelope?.Rules ?? new List<AssociationRule>());
		}
		catch (Exception ex)
		{
			if (File.Exists(path))
			{
				DialogService.ShowAsync(
					$"The association rules file could not be loaded and will be ignored.\n\nPath: {path}\n\nError: {ex.Message}",
					"Template Editor");
			}
			return new AssociationRuleCatalog(new List<AssociationRule>());
		}
	}

	private static string ResolveRuleFilePath(bool preferExisting)
	{
		string configuredPath = AddinConfiguration.Settings?.AssociationRulesJsonPath;
		if (!string.IsNullOrWhiteSpace(configuredPath))
		{
			string configuredDirectory = Path.GetDirectoryName(configuredPath);
			if (!string.IsNullOrWhiteSpace(configuredDirectory))
			{
				Directory.CreateDirectory(configuredDirectory);
			}
			return configuredPath;
		}
		string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		string packagedPath = Path.Combine(assemblyDirectory, "TemplateEditor", "AllowedAssociationRules.json");
		string rootPath = Path.Combine(assemblyDirectory, "AllowedAssociationRules.json");
		if (!preferExisting || File.Exists(packagedPath))
		{
			Directory.CreateDirectory(Path.GetDirectoryName(packagedPath));
			return packagedPath;
		}
		return File.Exists(rootPath) ? rootPath : packagedPath;
	}
}

internal sealed class FeatureLayerInfo
{
	public string TableName { get; set; }

	public string AssetGroup { get; set; }

	public string AssetType { get; set; }
}

internal sealed class AssociationRuleEnvelope
{
	public string Source { get; set; }

	public string GeneratedUtc { get; set; }

	public List<AssociationRule> Rules { get; set; }
}

internal sealed class AssociationRule
{
	public string AssociationType { get; set; }

	public string FromTable { get; set; }

	public string FromAssetGroup { get; set; }

	public string FromAssetType { get; set; }

	public string ToTable { get; set; }

	public string ToAssetGroup { get; set; }

	public string ToAssetType { get; set; }
}
