using System;
using System.Threading.Tasks;

namespace TemplateEditor;

internal static class TaskObservationService
{
    public static void Forget(Task task, string context)
    {
        if (task == null)
        {
            return;
        }
        _ = ObserveAsync(task, context);
    }

    private static async Task ObserveAsync(Task task, string context)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            LogService.LogException(context, ex);
        }
    }
}
