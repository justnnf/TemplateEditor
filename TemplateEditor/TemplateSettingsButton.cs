using System;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;

namespace TemplateEditor;

internal class TemplateSettingsButton : Button
{
	protected override void OnClick()
	{
		try
		{
			AddinConfiguration.ShowSettingsWindow();
		}
		catch (Exception ex)
		{
			DialogService.Show(ex.Message, "Template Settings");
		}
	}
}
