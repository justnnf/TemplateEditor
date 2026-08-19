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
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Invalid comparison between Unknown and I4
		return (int)geometryType == 0;
	}

	public static bool IsPoint(GeometryType geometryType)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Invalid comparison between Unknown and I4
		return (int)geometryType == 513;
	}

	public static bool IsPolyline(GeometryType geometryType)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Invalid comparison between Unknown and I4
		return (int)geometryType == 25607;
	}

	public static bool IsPolygon(GeometryType geometryType)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Invalid comparison between Unknown and I4
		return (int)geometryType == 27656;
	}
}
