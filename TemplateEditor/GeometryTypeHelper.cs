using ArcGIS.Core.Geometry;

namespace TemplateEditor;

internal static class GeometryTypeHelper
{
	public const GeometryType TableGeometryType = GeometryType.Unknown;
	public const GeometryType PointGeometryType = GeometryType.Point;
	public const GeometryType PolylineGeometryType = GeometryType.Polyline;
	public const GeometryType PolygonGeometryType = GeometryType.Polygon;

	public static bool IsTable(GeometryType geometryType) => geometryType == TableGeometryType;

	public static bool IsPoint(GeometryType geometryType) => geometryType == PointGeometryType;

	public static bool IsPolyline(GeometryType geometryType) => geometryType == PolylineGeometryType;

	public static bool IsPolygon(GeometryType geometryType) => geometryType == PolygonGeometryType;
}
