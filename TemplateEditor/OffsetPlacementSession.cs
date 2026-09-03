using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using ArcGIS.Core.Geometry;

namespace TemplateEditor;

internal static class OffsetPlacementSession
{
	public static bool IsActive { get; private set; }

	public static double DistanceMeters { get; private set; }

	public static MapPoint InsertPoint { get; private set; }

	public static bool Begin()
	{
		string text = TextEntryPromptWindow.ShowPrompt("Place at Offset", "Offset distance in metres. Click the line insert point, then move the mouse around the distance ring to choose the direction and click to place.", (DistanceMeters > 0.0) ? DistanceMeters.ToString("0.###", CultureInfo.CurrentCulture) : "1", Application.Current?.MainWindow, openAwayFromMapCenter: true);
		if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var result) || result <= 0.0)
		{
			if (text != null)
			{
				DialogService.Show("Enter an offset distance greater than zero metres.", "Template Editor");
			}
			return false;
		}
		DistanceMeters = result;
		InsertPoint = null;
		IsActive = true;
		return true;
	}

	public static void SetInsertPoint(MapPoint insertPoint)
	{
		InsertPoint = insertPoint;
	}

	public static void End()
	{
		IsActive = false;
		InsertPoint = null;
	}

	public static MapPoint GetOffsetPoint(MapPoint directionPoint)
	{
		if (InsertPoint == null || directionPoint == null || DistanceMeters <= 0.0)
		{
			return InsertPoint;
		}
		double num = directionPoint.X - InsertPoint.X;
		double num2 = directionPoint.Y - InsertPoint.Y;
		if (Math.Abs(num) < 1E-09 && Math.Abs(num2) < 1E-09)
		{
			return InsertPoint;
		}
		double num3 = Math.Atan2(num, num2) * 180.0 / Math.PI;
		Polyline val = GeometryEngine.Instance.ConstructGeodeticLineFromDistance((GeodeticCurveType)0, InsertPoint, DistanceMeters, num3, LinearUnit.Meters, (CurveDensifyMethod)0, DistanceMeters);
		return ((val == null) ? null : ((IEnumerable<MapPoint>)((Multipart)val).Points)?.LastOrDefault()) ?? InsertPoint;
	}
}
