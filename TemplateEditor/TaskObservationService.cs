using System;
using System.Threading.Tasks;

namespace TemplateEditor;

internal static class TaskObservationService
{
	public static void Forget(Task task, string context)
	{
		if (task != null)
		{
			_ = ObserveAsync(task, context);
		}
	}

	private static async Task ObserveAsync(Task task, string context)
	{
		try
		{
			await task;
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			LogService.LogException(context, ex2);
		}
	}
}
