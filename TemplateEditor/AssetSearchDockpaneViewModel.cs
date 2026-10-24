using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace TemplateEditor;

internal sealed class AssetSearchDockpaneViewModel : DockPane
{
	internal static void Show()
	{
		FrameworkApplication.DockPaneManager.Find("TemplateEditor_AssetSearchDockpane")?.Activate();
	}
}
