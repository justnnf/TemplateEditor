using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Mapping;

namespace TemplateEditor;

public class FeatureInfo
{
	public RowToken Token { get; set; }

	public int FeatureId { get; set; }

	public Geometry Geometry { get; set; }

	public SimpleTemplate Template { get; set; }

	public bool IsSpatialFeature { get; set; }

	public MapMember MapMember { get; set; }

	public long ObjectID { get; set; }
}
