using ArcGIS.Desktop.Framework.Contracts;

namespace TemplateEditor;

internal sealed class AssetSearchShowButton : Button
{
	protected override void OnClick()
	{
		AssetSearchDockpaneViewModel.Show();
	}
}
