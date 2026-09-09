using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ArcGIS.Core.Data.UtilityNetwork;

namespace TemplateEditor;

internal sealed class AssociationRuleCatalog
{
	private static AssociationRuleCatalog _current;

	private readonly List<AssociationRule> _rules;

	private readonly bool _isAvailable;

	public static AssociationRuleCatalog Current => _current ?? (_current = Load());

	public bool HasRules => _rules.Count > 0;

	public bool IsAvailable => _isAvailable;

	public static string RuleFilePath => ResolveRuleFilePath();

	private AssociationRuleCatalog(List<AssociationRule> rules, bool isAvailable = true)
	{
		_rules = rules ?? new List<AssociationRule>();
		_isAvailable = isAvailable;
	}

	public static void Reload()
	{
		_current = Load();
	}

	public bool Allows(AssociationType associationType, FeatureLayerInfo containerOrTarget, FeatureLayerInfo contentOrCreated)
	{
		if (!_isAvailable)
		{
			return false;
		}
		if (!HasRules || containerOrTarget == null || contentOrCreated == null)
		{
			return true;
		}
		string normalizedType = GetRuleAssociationTypeName(associationType);
		if (normalizedType == null)
		{
			return true;
		}
		return _rules.Any((AssociationRule rule) => string.Equals(rule.AssociationType, normalizedType, StringComparison.OrdinalIgnoreCase) && Matches(rule.FromTable, containerOrTarget.TableName) && Matches(rule.FromAssetGroup, containerOrTarget.AssetGroup) && Matches(rule.FromAssetType, containerOrTarget.AssetType) && Matches(rule.ToTable, contentOrCreated.TableName) && Matches(rule.ToAssetGroup, contentOrCreated.AssetGroup) && Matches(rule.ToAssetType, contentOrCreated.AssetType));
	}

	public HashSet<string> GetAllowedCounterpartTables(AssociationType associationType, FeatureLayerInfo knownSide, bool knownSideIsFrom)
	{
		if (!HasRules || knownSide == null)
		{
			return null;
		}
		string ruleAssociationTypeName = GetRuleAssociationTypeName(associationType);
		if (ruleAssociationTypeName == null)
		{
			return null;
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (AssociationRule rule in _rules)
		{
			if (!string.Equals(rule.AssociationType, ruleAssociationTypeName, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			string ruleValue = (knownSideIsFrom ? rule.FromTable : rule.ToTable);
			string ruleValue2 = (knownSideIsFrom ? rule.FromAssetGroup : rule.ToAssetGroup);
			string ruleValue3 = (knownSideIsFrom ? rule.FromAssetType : rule.ToAssetType);
			if (Matches(ruleValue, knownSide.TableName) && Matches(ruleValue2, knownSide.AssetGroup) && Matches(ruleValue3, knownSide.AssetType))
			{
				string value = (knownSideIsFrom ? rule.ToTable : rule.FromTable);
				if (string.IsNullOrWhiteSpace(value))
				{
					return null;
				}
				hashSet.Add(Normalize(value));
			}
		}
		return hashSet;
	}

	private static string GetRuleAssociationTypeName(AssociationType associationType)
	{
		if ((int)associationType == 3)
		{
			return "Attachment";
		}
		if ((int)associationType == 2)
		{
			return "Containment";
		}
		if ((int)associationType == 1)
		{
			return "JunctionJunctionConnectivity";
		}
		return null;
	}

	private static bool Matches(string ruleValue, string actualValue)
	{
		return string.IsNullOrWhiteSpace(ruleValue) || string.Equals(Normalize(ruleValue), Normalize(actualValue), StringComparison.OrdinalIgnoreCase);
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

	private static AssociationRuleCatalog Load()
	{
		try
		{
			// Missing rule JSON is allowed. Without a rule catalog the add-in falls
			// back to permissive association filtering and settings-window fallbacks.
			string path = ResolveRuleFilePath();
			if (!File.Exists(path))
			{
				return new AssociationRuleCatalog(new List<AssociationRule>());
			}
			return new AssociationRuleCatalog(JsonSerializer.Deserialize<AssociationRuleEnvelope>(File.ReadAllText(path), new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			})?.Rules ?? new List<AssociationRule>());
		}
		catch (Exception ex)
		{
			DialogService.ShowAsync("The association rules file could not be loaded. Automatic association filtering is disabled until the rules file is repaired.\n\nError: " + ex.Message, "Template Editor");
			return new AssociationRuleCatalog(new List<AssociationRule>(), isAvailable: false);
		}
	}

	private static string ResolveRuleFilePath()
	{
		string text = AddinConfiguration.Settings?.AssociationRulesJsonPath;
		if (!string.IsNullOrWhiteSpace(text))
		{
			return AtomicFileService.NormalizeJsonFilePath(text);
		}
		// Rule files are intentionally not packaged with the add-in. When the user
		// regenerates rules from the settings window, this app-data path becomes the
		// default location without changing the installed add-in contents.
		return Path.Combine(AddinConfiguration.UserDataDirectoryPath, "AllowedAssociationRules.json");
	}
}
