using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TemplateEditor;

internal sealed class GroupExpansionGlyphConverter : IValueConverter
{
	private static readonly Geometry CollapsedChevron = Geometry.Parse("M 1,0 L 9,5 L 1,10 Z");

	private static readonly Geometry ExpandedChevron = Geometry.Parse("M 0,1 L 10,1 L 5,9 Z");

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		// Vector geometry has no font baseline, so it remains visually centered at
		// every Windows DPI scale supported by ArcGIS Pro.
		return value is true ? ExpandedChevron : CollapsedChevron;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Binding.DoNothing;
	}
}
