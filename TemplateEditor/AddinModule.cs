using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ArcGIS.Desktop.Core.Events;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;

namespace TemplateEditor;

internal class AddinModule : Module
{
	private static AddinModule _this;
	private bool _setupPending;
	private bool _setupRunning;
	private bool _mapNoticeShown;

	public static AddinModule Current => _this ?? (_this = (AddinModule)(object)FrameworkApplication.FindModule("TemplateEditor_Module"));

	private AddinModule()
	{
		try
		{
			LogService.Write("AddinModule " + typeof(AddinModule).Assembly.GetName().Version + " constructor starting.");
			LogService.Write("User settings directory: " + AddinConfiguration.UserDataDirectoryPath);
			AddinConfiguration.Initialize();
			LogService.Write("AddinConfiguration initialized successfully.");
			ProjectOpenedEvent.Subscribe((Action<ProjectEventArgs>)OnProjectOpened, false);
			ActiveMapViewChangedEvent.Subscribe((Action<ActiveMapViewChangedEventArgs>)OnActiveMapViewChanged, false);
			LogService.Write("ProjectOpenedEvent subscribed.");
		}
		catch (Exception ex)
		{
			LogService.LogException("Error instantiating the add-in module.", ex);
			DialogService.Show("Error instantiating the add-in module:\n" + ex.Message + "\n\nDetails were written to the Template Editor log.", "Template Editor");
		}
	}

	private void OnProjectOpened(ProjectEventArgs args)
	{
		TaskObservationService.Forget(PlacementEnhancementService.ClearMetadataCacheAsync(), "Could not clear cached map metadata after opening a project.");
		DockPane val = FrameworkApplication.DockPaneManager.Find("TemplateEditor_EditorDockpaneV2");
		if (val != null && val.IsVisible)
		{
			val.Hide();
		}
		_setupPending = !AddinConfiguration.IsSetupComplete();
		_mapNoticeShown = false;
		ScheduleInitialSetup();
	}

	private void OnActiveMapViewChanged(ActiveMapViewChangedEventArgs args)
	{
		if (args.IncomingView != null)
		{
			ScheduleInitialSetup();
		}
	}

	private void ScheduleInitialSetup()
	{
		if (_setupPending && !_setupRunning && Application.Current != null)
		{
			Application.Current.Dispatcher.BeginInvoke(new Action(() =>
				TaskObservationService.Forget(ContinueInitialSetupAsync(), "Initial template setup failed.")), DispatcherPriority.ApplicationIdle);
		}
	}

	private async Task ContinueInitialSetupAsync()
	{
		if (!_setupPending || _setupRunning)
		{
			return;
		}
		_setupRunning = true;
		try
		{
			AddinConfiguration.ReloadUserSettingsIfMissing();
			while (true)
			{
				if (!AddinConfiguration.HasValidTemplateConfigPath())
				{
					if (DialogService.Show("Template Editor needs a template configuration JSON. Select the file to continue setup.", "Template Editor Setup", new DialogButtonChoice("Select file", MessageBoxResult.Yes, isPrimary: true), new DialogButtonChoice("Later", MessageBoxResult.No, isCancel: true)) != MessageBoxResult.Yes)
					{
						_setupPending = false;
						return;
					}
					string path = AddinConfiguration.PromptForTemplateConfigFilePath(AddinConfiguration.TemplateConfigFilePath);
					if (string.IsNullOrWhiteSpace(path))
					{
						_setupPending = false;
						return;
					}
					TemplateEditorSettings settings = AddinConfiguration.Settings.Clone();
					settings.TemplateConfigFilePath = path;
					AddinConfiguration.ApplySettings(settings);
				}
				try
				{
					AddinConfiguration.ReloadTemplates();
					break;
				}
				catch (Exception ex)
				{
					DialogService.Show("The selected template JSON could not be loaded. Choose another file.\n\n" + ex.Message, "Template Editor Setup");
					TemplateEditorSettings settings = AddinConfiguration.Settings.Clone();
					settings.TemplateConfigFilePath = string.Empty;
					AddinConfiguration.ApplySettings(settings);
				}
			}
			if (!AddinConfiguration.IsSetupComplete())
			{
				if (MapView.Active == null)
				{
					if (!_mapNoticeShown)
					{
						DialogService.Show("Open a map containing the utility network. Template Editor will then guide you through generating the association rules JSON.", "Template Editor Setup");
						_mapNoticeShown = true;
					}
					return;
				}
				string rulePath = AssociationRuleCatalog.RuleFilePath;
				string rulePrompt = (File.Exists(rulePath) ? "Replace the existing association rules JSON" : "Generate an association rules JSON")
					+ " from the utility network in the active map?\n\nFile: " + rulePath;
				if (DialogService.Show(rulePrompt, "Template Editor Setup", new DialogButtonChoice("Generate rules", MessageBoxResult.Yes, isPrimary: true), new DialogButtonChoice("Later", MessageBoxResult.No, isCancel: true)) != MessageBoxResult.Yes)
				{
					_setupPending = false;
					return;
				}
				AssociationRuleGenerationResult result = await AssociationRuleJsonRegenerator.RegenerateFromActiveMapAsync();
				if (result.RuleCount == 0)
				{
					_setupPending = false;
					DialogService.Show("No utility network rules were found in the active map. Open the intended map and generate the rules from Settings > Associations > Rule Catalog.", "Template Editor Setup");
					return;
				}
				TemplateEditorSettings settings = AddinConfiguration.Settings.Clone();
				settings.AssociationRulesJsonPath = result.OutputPath;
				AddinConfiguration.ApplySettings(settings);
			}
			AssociationRuleCatalog.Reload();
			if (AddinConfiguration.IsSetupComplete())
			{
				EditorDockpaneViewModel.ReloadConfig();
				EditorDockpaneViewModel.SetPlacementStatus(EditorDockpaneViewModel.ReadyPlacementStatus);
				_setupPending = false;
			}
		}
		catch (Exception ex)
		{
			_setupPending = false;
			LogService.LogException("Initial template setup failed.", ex);
			DialogService.Show("Setup could not be completed. Use Settings to retry.\n\n" + ex.Message, "Template Editor Setup");
		}
		finally
		{
			_setupRunning = false;
		}
	}

	protected override bool CanUnload()
	{
		ProjectOpenedEvent.Unsubscribe((Action<ProjectEventArgs>)OnProjectOpened);
		ActiveMapViewChangedEvent.Unsubscribe((Action<ActiveMapViewChangedEventArgs>)OnActiveMapViewChanged);
		TaskObservationService.Forget(PlacementEnhancementService.ClearMetadataCacheAsync(), "Could not clear cached map metadata while unloading the add-in.");
		return true;
	}
}
