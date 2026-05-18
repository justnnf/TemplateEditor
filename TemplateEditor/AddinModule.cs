using System;
using ArcGIS.Desktop.Core.Events;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;

namespace TemplateEditor;

internal class AddinModule : Module
{
	private static AddinModule _this;

	public static AddinModule Current => _this ?? (_this = (AddinModule)(object)FrameworkApplication.FindModule("TemplateEditor_Module"));

	private AddinModule()
	{
		try
		{
			AddinConfiguration.Initialize();
		}
		catch (Exception ex)
		{
			DialogService.Show("Error instantiating the add-in module:\n" + ex.Message + "\n\n" + ex.StackTrace, "Template Editor");
		}
	}

	private void OnProjectOpened(ProjectEventArgs args)
	{
		DockPane nbrnPane = FrameworkApplication.DockPaneManager.Find("TemplateEditor_EditorDockpane");
		if (nbrnPane != null && nbrnPane.IsVisible)
		{
			nbrnPane.Hide();
		}
	}

	protected override bool CanUnload()
	{
		return true;
	}
}
