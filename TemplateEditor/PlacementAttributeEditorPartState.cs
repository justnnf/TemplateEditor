using System.Collections.Generic;

namespace TemplateEditor;

internal sealed class PlacementAttributeEditorPartState
{
	public string PartKey { get; set; }

	public string DisplayName { get; set; }

	public string DetailText { get; set; }

	public int FeatureId { get; set; }

	public SimpleTemplate Template { get; set; }

	public List<PlacementAttributeEditorFieldState> AttributeFields { get; set; } = new List<PlacementAttributeEditorFieldState>();
}
