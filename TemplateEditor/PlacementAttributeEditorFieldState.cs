using System.Collections.Generic;

namespace TemplateEditor;

internal sealed class PlacementAttributeEditorFieldState
{
	public string FieldName { get; set; }

	public string Label { get; set; }

	public string ConfiguredValue { get; set; }

	public string CurrentValue { get; set; }

	public bool HasDomainValues { get; set; }

	public bool IsEditable { get; set; } = true;

	public List<string> AvailableValues { get; set; } = new List<string>();

	public string ConfiguredValueSummary => string.IsNullOrWhiteSpace(ConfiguredValue) ? "Configured default: (blank)" : ("Configured default: " + ConfiguredValue);
}
