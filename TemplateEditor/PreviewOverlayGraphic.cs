using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;

namespace TemplateEditor;

internal sealed class PreviewOverlayGraphic
{
	public PreviewOverlayGraphic(Geometry geometry, CIMSymbolReference symbol)
	{
		Geometry = geometry;
		Symbol = symbol;
	}

	public Geometry Geometry { get; }

	public CIMSymbolReference Symbol { get; }
}
