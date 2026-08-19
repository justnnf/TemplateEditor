using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ArcGIS.Desktop.Framework;

namespace TemplateEditor;

internal static class ToolReactivationService
{
	private const string SelectToolId = "esri_mapping_selectByRectangleTool";

	private static int _activationRequestId;

	public static void ActivateSelectTool()
	{
		ActivateTool("esri_mapping_selectByRectangleTool");
	}

	public static void ActivateTool(string toolId)
	{
		if (!string.IsNullOrWhiteSpace(toolId))
		{
			int requestId = Interlocked.Increment(ref _activationRequestId);
			TaskObservationService.Forget(ActivateToolAsync(toolId, requestId), "Tool reactivation failed for '" + toolId + "'.");
		}
	}

	private static async Task ActivateToolAsync(string toolId, int requestId)
	{
		try
		{
			string currentTool = FrameworkApplication.CurrentTool;
			if (!string.Equals(toolId, "esri_mapping_selectByRectangleTool", StringComparison.OrdinalIgnoreCase) && string.Equals(currentTool, toolId, StringComparison.OrdinalIgnoreCase))
			{
				await FrameworkApplication.SetCurrentToolAsync("esri_mapping_selectByRectangleTool");
			}
			if (requestId == _activationRequestId)
			{
				await FrameworkApplication.SetCurrentToolAsync(toolId);
			}
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			if (requestId != _activationRequestId)
			{
				return;
			}
			LogService.LogException("SetCurrentToolAsync failed for '" + toolId + "'. Falling back to plug-in command activation.", ex2);
			((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
			{
				if (FrameworkApplication.GetPlugInWrapper(toolId, true) is ICommand command)
				{
					command.Execute(null);
				}
			}, Array.Empty<object>());
		}
	}
}
