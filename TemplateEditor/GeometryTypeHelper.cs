using ArcGIS.Core.Geometry;

namespace TemplateEditor;

internal static class GeometryTypeHelper
{
	public const GeometryType TableGeometryType = (GeometryType)0;

	public const GeometryType PointGeometryType = (GeometryType)513;

	public const GeometryType PolylineGeometryType = (GeometryType)25607;

	public const GeometryType PolygonGeometryType = (GeometryType)27656;

	public static bool IsTable(GeometryType geometryType)
	{
		return (int)geometryType == 0;
	}

	public static bool IsPoint(GeometryType geometryType)
	{
		return (int)geometryType == 513;
	}

	public static bool IsPolyline(GeometryType geometryType)
	{
		return (int)geometryType == 25607;
	}

	public static bool IsPolygon(GeometryType geometryType)
	{
		return (int)geometryType == 27656;
	}
}
