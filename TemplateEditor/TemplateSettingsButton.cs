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
			LogService.Write("Template settings button clicked.");
			AddinConfiguration.ShowSettingsWindow();
			LogService.Write("Template settings window request completed.");
		}
		catch (Exception ex)
		{
			LogService.LogException("Template settings button failed.", ex);
			DialogService.Show(ex.Message, "Template Settings");
		}
	}
}
