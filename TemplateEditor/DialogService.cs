using System;
using System.Windows;
using System.Windows.Threading;
using ArcGIS.Desktop.Framework;
using ProMessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace TemplateEditor;

internal static class DialogService
{
	public static MessageBoxResult Show(string message, string title)
	{
		return Show(message, title, MessageBoxButton.OK);
	}

	public static void ShowAsync(string message, string title, FeedbackSeverity? severity = null)
	{
		Application current = Application.Current;
		if (current == null)
		{
			return;
		}
		((DispatcherObject)current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
		{
			if (!severity.HasValue)
			{
				Show(message, title);
			}
			else
			{
				ShowToast(message, title, severity.Value);
			}
		}, Array.Empty<object>());
	}

	public static MessageBoxResult Show(string message, string title, FeedbackSeverity? severity)
	{
		ShowToast(message, title, severity.GetValueOrDefault());
		return MessageBoxResult.OK;
	}

	public static MessageBoxResult Show(string message, string title, MessageBoxButton buttons)
	{
		return ProMessageBox.Show(message, title, buttons);
	}

	public static MessageBoxResult Show(string message, string title, params DialogButtonChoice[] choices)
	{
		return ShowPrompt(message, title, choices);
	}

	public static void ShowToast(string message, string title, FeedbackSeverity severity)
	{
		Application current = Application.Current;
		if (current != null)
		{
			((DispatcherObject)current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
			{
				string notificationTitle = severity switch
				{
					FeedbackSeverity.Warning => title + " — Warning",
					FeedbackSeverity.Error => title + " — Error",
					_ => title
				};
				FrameworkApplication.AddNotification(new Notification
				{
					Title = notificationTitle,
					Message = message ?? string.Empty
				});
			}, Array.Empty<object>());
		}
	}

	private static MessageBoxResult ShowPrompt(string message, string title, DialogButtonChoice[] choices)
	{
		Window window = Application.Current?.MainWindow;
		FeedbackPromptWindow feedbackPromptWindow = new FeedbackPromptWindow(message, title, choices);
		if (window != null)
		{
			feedbackPromptWindow.Owner = window;
		}
		return feedbackPromptWindow.ShowPrompt();
	}
}
