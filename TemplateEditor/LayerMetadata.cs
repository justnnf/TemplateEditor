using System.Collections.Generic;
using ArcGIS.Core.Data;

namespace TemplateEditor;

internal sealed class LayerMetadata
{
	public TableDefinition Definition { get; set; }

	public List<Field> Fields { get; set; }

	public string OwningGroupName { get; set; }
}
