using ArcGIS.Desktop.Mapping;

namespace TemplateEditor;

internal sealed class LayerSearchContext
{
	public FeatureLayer Layer { get; set; }

	public string OwningGroupName { get; set; }
}
