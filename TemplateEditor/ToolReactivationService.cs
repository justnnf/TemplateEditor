using System;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;

namespace TemplateEditor;

internal static class ToolReactivationService
{
	private const string SelectToolId = "esri_mapping_selectByRectangleTool";
	private static int _activationRequestId;

	public static void ActivateSelectTool()
	{
		ActivateTool(SelectToolId);
	}

	public static void ActivateTool(string toolId)
	{
		if (string.IsNullOrWhiteSpace(toolId))
		{
			return;
		}
		int requestId = ++_activationRequestId;
		_ = ActivateToolAsync(toolId, requestId);
	}

	private static async Task ActivateToolAsync(string toolId, int requestId)
	{
		try
		{
			string currentTool = FrameworkApplication.CurrentTool;
			if (!string.Equals(toolId, SelectToolId, StringComparison.OrdinalIgnoreCase) && string.Equals(currentTool, toolId, StringComparison.OrdinalIgnoreCase))
			{
				await FrameworkApplication.SetCurrentToolAsync(SelectToolId);
			}
			if (requestId != _activationRequestId)
			{
				return;
			}
			await FrameworkApplication.SetCurrentToolAsync(toolId);
		}
		catch
		{
			if (requestId != _activationRequestId)
			{
				return;
			}
			_ = FrameworkApplication.Current.Dispatcher.BeginInvoke((Action)delegate
			{
				((ICommand)FrameworkApplication.GetPlugInWrapper(toolId, true)).Execute(null);
			});
		}
	}
}
