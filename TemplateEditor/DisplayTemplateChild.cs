using System.Collections.Generic;

namespace TemplateEditor;

public class DisplayTemplateChild
{
	public string Name { get; set; }

	public string TemplateType { get; set; }

	public string Description { get; set; }

	public int FeatureId { get; set; }

	public string SketchType { get; set; }

	public string ParentTemplateName { get; set; }

	public string SelectionKey => $"{ParentTemplateName}|{FeatureId}|{Name}";

	public bool IsSelected { get; set; }

	public string DisplayText
	{
		get
		{
			string text = ((FeatureId > 0) ? $"{FeatureId}. " : string.Empty);
			return text + Name;
		}
	}

	public string DetailText
	{
		get
		{
			List<string> list = new List<string>();
			if (!string.IsNullOrWhiteSpace(TemplateType))
			{
				list.Add(TemplateType);
			}
			if (!string.IsNullOrWhiteSpace(SketchType))
			{
				list.Add(SketchType);
			}
			if (!string.IsNullOrWhiteSpace(Description))
			{
				list.Add(Description);
			}
			return string.Join(" | ", list);
		}
	}
}
