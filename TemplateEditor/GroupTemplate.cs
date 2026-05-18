using System.Collections.Generic;

namespace TemplateEditor;

public class GroupTemplate
{
	public string Name { get; set; }

	public string Description { get; set; }

	public string TemplateType { get; set; }

	public List<SimpleTemplateReference> SimpleTemplates { get; set; }

	public List<AssociationObject> Associations { get; set; }
}
