using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace TemplateEditor;

internal class TemplateReloadConfigButton : Button
{
	protected override void OnClick()
	{
		try
		{
			EditorDockpaneViewModel.ReloadConfig();
		}
		catch (Exception ex)
		{
			DialogService.Show(ex.Message, "Template Editor");
		}
	}
}
