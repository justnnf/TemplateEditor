using System.Collections.Generic;

namespace TemplateEditor;

internal sealed class PlacementAttributeOverrideEditorState
{
	public PlacementAttributeOverrideDefinition Definition { get; set; }

	public bool IsEnabled { get; set; }

	public string Value { get; set; }

	public string ConfiguredValueSummary { get; set; }

	public List<string> AvailableValues { get; set; } = new List<string>();

	public bool UseDropDown
	{
		get
		{
			List<string> availableValues = AvailableValues;
			return availableValues != null && availableValues.Count > 0;
		}
	}
}
