using System;
using System.Collections.Generic;
using System.Linq;
using ArcGIS.Core.Data;

namespace TemplateEditor;

internal static class FeatureInfoCache
{
	public static string GetDomainDescription(Domain domain, object value)
	{
		CodedValueDomain val = (CodedValueDomain)(object)((domain is CodedValueDomain) ? domain : null);
		if (val == null || value == null)
		{
			return null;
		}
		string text = Convert.ToString(value);
		foreach (KeyValuePair<object, string> codedValuePair in val.GetCodedValuePairs())
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
		return definition.GetSubtypes().FirstOrDefault((Subtype st) => string.Equals(Convert.ToString(st.GetCode()), subtypeValueText, StringComparison.OrdinalIgnoreCase) || string.Equals(st.GetName(), subtypeValueText, StringComparison.OrdinalIgnoreCase));
	}
}
