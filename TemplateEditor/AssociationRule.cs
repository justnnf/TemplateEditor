namespace TemplateEditor;

internal sealed class AssociationRule
{
	public string AssociationType { get; set; }

	public string FromTable { get; set; }

	public string FromAssetGroup { get; set; }

	public string FromAssetType { get; set; }

	public string ToTable { get; set; }

	public string ToAssetGroup { get; set; }

	public string ToAssetType { get; set; }
}
