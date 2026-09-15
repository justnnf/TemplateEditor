using System;
using System.Collections.Generic;
using System.Linq;
using ArcGIS.Core.Data;

namespace TemplateEditor;

internal static class FeatureInfoCache
{
	public static string GetDomainDescription(Domain domain, object value)
	{
		if (domain is not CodedValueDomain codedValueDomain || value == null)
		{
			return null;
		}
		string text = Convert.ToString(value);
		foreach (KeyValuePair<object, string> codedValuePair in codedValueDomain.GetCodedValuePairs())
		{
			if (string.Equals(Convert.ToString(codedValuePair.Key), text, StringComparison.OrdinalIgnoreCase))
			{
				return codedValuePair.Value;
			}
		}
		return null;
	}

	public static Subtype GetSubtype(TableDefinition definition, Feature feature)
	{
		string subtypeField = definition?.GetSubtypeField();
		if (string.IsNullOrWhiteSpace(subtypeField))
		{
			return null;
		}
		object subtypeValue = feature[subtypeField];
		if (subtypeValue == null || subtypeValue == DBNull.Value)
		{
			return null;
		}
		string subtypeValueText = Convert.ToString(subtypeValue);
		return definition.GetSubtypes().FirstOrDefault((Subtype st) => string.Equals(Convert.ToString(st.GetCode()), subtypeValueText, StringComparison.OrdinalIgnoreCase) || string.Equals(st.GetName(), subtypeValueText, StringComparison.OrdinalIgnoreCase));
	}
}
