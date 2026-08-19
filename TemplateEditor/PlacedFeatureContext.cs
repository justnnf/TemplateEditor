using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Mapping;

namespace TemplateEditor;

internal sealed class PlacedFeatureContext
{
	public SimpleTemplate Template { get; set; }

	public Geometry Geometry { get; set; }

	public RowToken Token { get; set; }

	public FeatureLayer Layer { get; set; }

	public long ObjectID { get; set; }

	public bool AllowPlacementEnhancements { get; set; } = true;

	public MapPoint SplitPointOverride { get; set; }
}
