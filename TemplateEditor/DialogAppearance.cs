using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using ArcGIS.Desktop.Framework;

namespace TemplateEditor;

internal static class DialogAppearance
{

	private static bool IsDark => FrameworkApplication.ApplicationTheme is ApplicationTheme.Dark or ApplicationTheme.HighContrast;

	internal static Brush Background => Brush("#1F2328", "#F5F7F9");
	internal static Brush Foreground => Brush("#F4F7F9", "#17212B");
	internal static Brush InputBackground => Brush("#2B333B", "#FFFFFF");
	internal static Brush Border => Brush("#58636D", "#BFCBD4");
	internal static Brush SectionBorder => Brush("#484848", "#D0D0D0");
	internal static Brush ControlBorder => Brush("#606064", "#969696");
	internal static Brush ButtonBackground => Brush("#3A3A3E", "#E8E8E8");
	internal static Brush ButtonHoverBackground => Brush("#48484E", "#E1EBF5");
	internal static Brush SecondaryForeground => Brush("#CDCDCD", "#606060");
	internal static Brush Accent => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#007AC2"));
	internal static Brush AccentHover => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2080E0"));

	internal static void ApplySquareButtonTemplate(Style style)
	{
		if (style == null)
		{
			return;
		}
		ControlTemplate controlTemplate = new ControlTemplate(typeof(Button));
		FrameworkElementFactory borderFactory = new FrameworkElementFactory(typeof(Border));
		borderFactory.SetBinding(System.Windows.Controls.Border.BackgroundProperty, new Binding("Background")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		borderFactory.SetBinding(System.Windows.Controls.Border.BorderBrushProperty, new Binding("BorderBrush")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		borderFactory.SetBinding(System.Windows.Controls.Border.BorderThicknessProperty, new Binding("BorderThickness")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		FrameworkElementFactory contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
		contentFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		contentFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		contentFactory.SetBinding(FrameworkElement.MarginProperty, new Binding("Padding")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		borderFactory.AppendChild(contentFactory);
		controlTemplate.VisualTree = borderFactory;
		style.Setters.Add(new Setter(Control.TemplateProperty, controlTemplate));
	}

	private static Brush Brush(string dark, string light) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(IsDark ? dark : light));
}
