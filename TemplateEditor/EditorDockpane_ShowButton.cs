using System;
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework.Contracts;

namespace TemplateEditor;

internal class EditorDockpane_ShowButton : Button
{
	protected override void OnClick()
	{
		TaskObservationService.Forget(OnClickAsync(), "Template editor button failed.");
	}

	private static async Task OnClickAsync()
	{
		try
		{
			LogService.Write("Template editor button clicked.");
			AddinConfiguration.ReloadUserSettingsIfMissing();
			if (!EnsureTemplateConfigPath())
			{
				LogService.Write("Template editor open canceled because no config path was selected.");
				return;
			}
			AddinConfiguration.ReloadTemplates();
			LogService.Write("Template config loaded for editor launch.");
			EditorDockpaneViewModel.Show();

			if (AddinConfiguration.ValidateConfig)
			{
				LogService.Write("Validating template config.");
				string message = await CommonFunctions.ValidateConfiguration();
				if (!string.IsNullOrWhiteSpace(message))
				{
					LogService.Write("Template config validation reported issues.");
					DialogService.Show(
						"The editor is open. Review these configuration issues before using the affected templates.\n\n" + message,
						"Template Configuration Validation");
				}
			}
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			LogService.LogException("Template editor button failed.", ex2);
			DialogService.Show(ex2.Message, "Template Editor");
		}
	}

	private static bool EnsureTemplateConfigPath()
	{
		if (AddinConfiguration.HasValidTemplateConfigPath())
		{
			return true;
		}
		DialogService.Show("The selected template configuration file could not be found. Choose a template configuration JSON file to open the editor.", "Template Editor");
		string text = AddinConfiguration.PromptForTemplateConfigFilePath(AddinConfiguration.TemplateConfigFilePath);
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		TemplateEditorSettings templateEditorSettings = AddinConfiguration.Settings?.Clone() ?? new TemplateEditorSettings();
		templateEditorSettings.TemplateConfigFilePath = text;
		AddinConfiguration.ApplySettings(templateEditorSettings);
		return AddinConfiguration.HasValidTemplateConfigPath();
	}
}
