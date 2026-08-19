using System.Collections.Generic;

namespace TemplateEditor;

internal sealed class PlacementAttributeEditorModel
{
	public string TemplateKey { get; set; }

	public string TemplateDisplayName { get; set; }

	public bool IsGroupTemplate { get; set; }

	public List<PlacementAttributeEditorPartState> Parts { get; set; } = new List<PlacementAttributeEditorPartState>();

	public List<PlacementAttributeOverrideFavouriteSummary> AvailableFavourites { get; set; } = new List<PlacementAttributeOverrideFavouriteSummary>();
}
