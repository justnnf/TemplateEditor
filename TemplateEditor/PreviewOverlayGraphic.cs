using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;

namespace TemplateEditor;

internal sealed class PreviewOverlayGraphic
{
	public Geometry Geometry { get; }

	public CIMSymbolReference Symbol { get; }

	public PreviewOverlayGraphic(Geometry geometry, CIMSymbolReference symbol)
	{
		Geometry = geometry;
		Symbol = symbol;
	}
}
