namespace TemplateEditor;

internal sealed class PlacementAttributeOverrideFavouriteSummary
{
	public string Id { get; set; }

	public string Name { get; set; }

	public string TemplateKey { get; set; }

	public string TemplateDisplayName { get; set; }

	public override string ToString()
	{
		return Name ?? string.Empty;
	}
}
