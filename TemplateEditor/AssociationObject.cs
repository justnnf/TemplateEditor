namespace TemplateEditor;

public class AssociationObject
{
	public string Type { get; set; }

	public int FromFeatureId { get; set; }

	public int ToFeatureId { get; set; }

	public int FromTerminal { get; set; }

	public int ToTerminal { get; set; }
}
