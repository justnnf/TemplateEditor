using ArcGIS.Core.Geometry;

namespace TemplateEditor;

internal static class GeometryTypeHelper
{
	private const int TableGeometryType = 0;
	private const int PointGeometryType = 513;
	private const int PolylineGeometryType = 25607;
	private const int PolygonGeometryType = 27656;

	public static bool IsTable(GeometryType geometryType) => (int)geometryType == TableGeometryType;

	public static bool IsPoint(GeometryType geometryType) => (int)geometryType == PointGeometryType;

	public static bool IsPolyline(GeometryType geometryType) => (int)geometryType == PolylineGeometryType;

	public static bool IsPolygon(GeometryType geometryType) => (int)geometryType == PolygonGeometryType;
}
