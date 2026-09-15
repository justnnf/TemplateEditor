using System.Collections.Generic;

namespace TemplateEditor;

/// <summary>
/// Describes an editable field discovered on one or more configured template targets.
/// The settings window uses this lightweight model instead of holding ArcGIS SDK objects.
/// </summary>
internal sealed class PlacementAttributeFieldOption
{
	public string FieldName { get; set; }

	public string Alias { get; set; }

	public int TargetCount { get; set; }

	public List<string> DomainNames { get; set; } = new List<string>();

	public string DisplayName => string.IsNullOrWhiteSpace(Alias) || string.Equals(Alias, FieldName, System.StringComparison.OrdinalIgnoreCase)
		? FieldName
		: $"{Alias} ({FieldName})";

	// The settings window uses a custom ComboBox template that displays selected
	// objects via ToString rather than DisplayMemberPath.
	public override string ToString()
	{
		return DisplayName;
	}
}
