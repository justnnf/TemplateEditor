using System;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;

namespace TemplateEditor;

internal class EditorDockpane_ShowButton : Button
{
	protected override async void OnClick()
	{
		try
		{
			if (!EnsureTemplateConfigPath())
			{
				return;
			}
			bool isConfigValid = false;
			AddinConfiguration.Templates = AddinConfiguration.LoadTemplateConfig();
			string message = null;
			if (AddinConfiguration.ValidateConfig)
			{
				message = await CommonFunctions.ValidateConfiguration();
			}
			if (message != null)
			{
				DialogService.Show("Error(s) in the template configuration:\n\n" + message, "Template Editor");
			}
			else
			{
				isConfigValid = true;
			}
			if (isConfigValid)
			{
				EditorDockpaneViewModel.Show();
			}
		}
		catch (Exception ex)
		{
			DialogService.Show(ex.Message, "Template Editor");
		}
	}

	private static bool EnsureTemplateConfigPath()
	{
		if (AddinConfiguration.HasValidTemplateConfigPath())
		{
			return true;
		}
		DialogService.Show("The selected template configuration file could not be found. Choose a template configuration JSON file to open the editor.", "Template Editor");
		string selectedPath = AddinConfiguration.PromptForTemplateConfigFilePath(AddinConfiguration.TemplateConfigFilePath);
		if (string.IsNullOrWhiteSpace(selectedPath))
		{
			return false;
		}
		TemplateEditorSettings settings = AddinConfiguration.Settings?.Clone() ?? new TemplateEditorSettings();
		settings.TemplateConfigFilePath = selectedPath;
		AddinConfiguration.ApplySettings(settings);
		return AddinConfiguration.HasValidTemplateConfigPath();
	}
}
