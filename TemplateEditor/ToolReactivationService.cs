using System;
using System.Threading;
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
		int requestId = Interlocked.Increment(ref _activationRequestId);
		TaskObservationService.Forget(ActivateToolAsync(toolId, requestId), $"Tool reactivation failed for '{toolId}'.");
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
		catch (Exception ex)
		{
			if (requestId != _activationRequestId)
			{
				return;
			}
			LogService.LogException($"SetCurrentToolAsync failed for '{toolId}'. Falling back to plug-in command activation.", ex);
			_ = FrameworkApplication.Current.Dispatcher.BeginInvoke((Action)delegate
			{
				if (FrameworkApplication.GetPlugInWrapper(toolId, true) is ICommand command)
				{
					command.Execute(null);
				}
			});
		}
	}
}
