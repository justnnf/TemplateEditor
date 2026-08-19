using System;
using System.Collections.Generic;
using System.Linq;
using ArcGIS.Core.Data;

namespace TemplateEditor;

internal static class FeatureInfoCache
{
	private static readonly Dictionary<string, string> DomainDescriptionCache = new Dictionary<string, string>(StringComparer.Ordinal);

	private static readonly Dictionary<string, Subtype> SubtypeCache = new Dictionary<string, Subtype>(StringComparer.Ordinal);

	private static readonly object LockObject = new object();

	public static string GetDomainDescription(Domain domain, object value, string cacheKeyPrefix = null)
	{
		CodedValueDomain val = (CodedValueDomain)(object)((domain is CodedValueDomain) ? domain : null);
		if (val == null || value == null)
		{
			return null;
		}
		string text = Convert.ToString(value);
		string key = $"{cacheKeyPrefix ?? ""}Domain:{((domain != null) ? domain.GetName() : null) ?? ""}:{text}";
		lock (LockObject)
		{
			if (DomainDescriptionCache.TryGetValue(key, out var value2))
			{
				return value2;
			}
		}
		foreach (KeyValuePair<object, string> codedValuePair in val.GetCodedValuePairs())
		{
			if (string.Equals(Convert.ToString(codedValuePair.Key), text, StringComparison.OrdinalIgnoreCase))
			{
				lock (LockObject)
				{
					DomainDescriptionCache[key] = codedValuePair.Value;
				}
				return codedValuePair.Value;
			}
		}
		lock (LockObject)
		{
			DomainDescriptionCache[key] = null;
		}
		return null;
	}

	public static Subtype GetSubtype(TableDefinition definition, Feature feature, string cacheKeyPrefix = null)
	{
		string text = ((definition != null) ? definition.GetSubtypeField() : null);
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}
		object obj = ((Row)feature)[text];
		if (obj == null || obj == DBNull.Value)
		{
			return null;
		}
		string subtypeValueText = Convert.ToString(obj);
		string key = $"{cacheKeyPrefix ?? ""}Subtype:{((definition != null) ? ((Definition)definition).GetName() : null) ?? ""}:{subtypeValueText}";
		lock (LockObject)
		{
			if (SubtypeCache.TryGetValue(key, out var value))
			{
				return value;
			}
		}
		Subtype val = definition.GetSubtypes().FirstOrDefault((Subtype st) => string.Equals(Convert.ToString(st.GetCode()), subtypeValueText, StringComparison.OrdinalIgnoreCase) || string.Equals(st.GetName(), subtypeValueText, StringComparison.OrdinalIgnoreCase));
		lock (LockObject)
		{
			SubtypeCache[key] = val;
		}
		return val;
	}

	public static void Clear()
	{
		lock (LockObject)
		{
			DomainDescriptionCache.Clear();
			SubtypeCache.Clear();
		}
	}
}
