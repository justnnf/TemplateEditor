using System.Linq;

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

	public string DisplayText => (FeatureId > 0 ? $"{FeatureId}. " : string.Empty) + Name;

	public string DetailText
	{
		get
		{
			return string.Join(" | ", new[] { TemplateType, SketchType, Description }
				.Where(value => !string.IsNullOrWhiteSpace(value)));
		}
	}
}
