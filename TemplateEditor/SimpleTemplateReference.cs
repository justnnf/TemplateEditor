using System.Collections.Generic;

namespace TemplateEditor;

public class SimpleTemplateReference
{
	public string Name { get; set; }

	public int FeatureId { get; set; }

	public List<double> Location { get; set; }

	public List<List<double>> Line { get; set; }

	public List<List<double>> Polygon { get; set; }

	public string SketchType { get; set; }
}
