using System.Collections.Generic;

namespace TemplateEditor;

public class DisplayTemplate
{
	public string Name { get; set; }

	public string TemplateType { get; set; }

	public string Description { get; set; }

	public List<DisplayTemplateChild> ChildTemplates { get; set; } = new List<DisplayTemplateChild>();

	public bool HasChildTemplates => ChildTemplates != null && ChildTemplates.Count > 0;

	public bool IsExpanded { get; set; }

	public bool IsGroupChild { get; set; }

	public bool IsFlatListItem { get; set; }

	public bool IsIndentedChild => IsGroupChild && !IsFlatListItem;

	public string ParentTemplateName { get; set; }

	public int FeatureId { get; set; }

	public string SketchType { get; set; }

	public string DisplayName => (IsIndentedChild && FeatureId > 0) ? $"{FeatureId}. {Name}" : Name;

	public string UniqueKey => (IsGroupChild && !string.IsNullOrEmpty(ParentTemplateName)) ? $"{ParentTemplateName}|{FeatureId}|{Name}" : (Name ?? string.Empty);
}
