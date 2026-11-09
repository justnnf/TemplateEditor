using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace TemplateEditor;

internal class TemplateSettingsButton : Button
{
	protected override void OnClick()
	{
		try
		{
			LogService.Write("Template settings button clicked.");
			AddinConfiguration.ReloadUserSettingsIfMissing();
			bool wasReady = AddinConfiguration.IsSetupComplete();
			if (AddinConfiguration.ShowSettingsWindow() && !wasReady && AddinConfiguration.IsSetupComplete())
			{
				EditorDockpaneViewModel.ReloadConfig();
				EditorDockpaneViewModel.SetPlacementStatus(EditorDockpaneViewModel.ReadyPlacementStatus);
			}
			LogService.Write("Template settings window request completed.");
		}
		catch (Exception ex)
		{
			LogService.LogException("Template settings button failed.", ex);
			DialogService.Show(ex.Message, "Template Settings");
		}
	}
}
