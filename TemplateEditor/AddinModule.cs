using System;
using ArcGIS.Desktop.Core.Events;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace TemplateEditor;

internal class AddinModule : Module
{
	private static AddinModule _this;

	public static AddinModule Current => _this ?? (_this = (AddinModule)(object)FrameworkApplication.FindModule("TemplateEditor_Module"));

	private AddinModule()
	{
		try
		{
			LogService.Write("AddinModule constructor starting.");
			AddinConfiguration.Initialize();
			LogService.Write("AddinConfiguration initialized successfully.");
			ProjectOpenedEvent.Subscribe((Action<ProjectEventArgs>)OnProjectOpened, false);
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
		DockPane val = FrameworkApplication.DockPaneManager.Find("TemplateEditor_EditorDockpane");
		if (val != null && val.IsVisible)
		{
			val.Hide();
		}
	}

	protected override bool CanUnload()
	{
		ProjectOpenedEvent.Unsubscribe((Action<ProjectEventArgs>)OnProjectOpened);
		return true;
	}
}
