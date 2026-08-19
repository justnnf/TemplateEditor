using System.Collections.Generic;

namespace TemplateEditor;

internal sealed class PlacementAttributeOverrideCatalog
{
	public List<PlacementAttributeOverrideDefinition> Fields { get; set; } = new List<PlacementAttributeOverrideDefinition>();
}
