using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TemplateEditor;

internal sealed class ChildRowIndentConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return value is true ? new Thickness(28, 0, 0, 0) : new Thickness(0);
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Binding.DoNothing;
	}
}
