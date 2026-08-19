using System.Collections.Generic;

namespace TemplateEditor;

internal sealed class PlacementAttributeOverrideFavourite
{
	public string Id { get; set; }

	public string Name { get; set; }

	public string TemplateKey { get; set; }

	public string TemplateDisplayName { get; set; }

	public string CreatedUtc { get; set; }

	public string UpdatedUtc { get; set; }

	public Dictionary<string, Dictionary<string, string>> PartValues { get; set; } = new Dictionary<string, Dictionary<string, string>>();
}
