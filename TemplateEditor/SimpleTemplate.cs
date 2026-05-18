using System.Collections.Generic;

namespace TemplateEditor;

public class SimpleTemplate
{
	public string Name { get; set; }

	public string Description { get; set; }

	public string TemplateType { get; set; }

	public string GroupLayer { get; set; }

	public string SubtypeLayer { get; set; }

	public List<List<double>> Geometry { get; set; }

	public Dictionary<string, object> DefaultFieldValues { get; set; }
}
