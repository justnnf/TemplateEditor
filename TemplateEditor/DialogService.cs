using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

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
		return ShowPrompt(message, title, buttons);
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
				(string Summary, string Detail) tuple = CreateToastText(message);
				string item = tuple.Summary;
				string item2 = tuple.Detail;
				FeedbackToastWindow feedbackToastWindow = new FeedbackToastWindow(title, item, item2, severity);
				feedbackToastWindow.Show();
			}, Array.Empty<object>());
		}
	}

	private static MessageBoxResult ShowPrompt(string message, string title, MessageBoxButton buttons)
	{
		Window window = Application.Current?.MainWindow;
		FeedbackPromptWindow feedbackPromptWindow = new FeedbackPromptWindow(message, title, buttons);
		if (window != null)
		{
			feedbackPromptWindow.Owner = window;
		}
		return feedbackPromptWindow.ShowPrompt();
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

	private static (string Summary, string Detail) CreateToastText(string message)
	{
		if (string.IsNullOrWhiteSpace(message))
		{
			return (Summary: string.Empty, Detail: null);
		}
		string text = message.Replace("\r\n", "\n");
		string[] array = (from line in text.Split('\n')
			select line.Trim() into line
			where line.Length > 0
			select line).ToArray();
		if (array.Length == 0)
		{
			return (Summary: string.Empty, Detail: null);
		}
		string item = TrimForToast(array[0], 180);
		string item2 = ((array.Length <= 1) ? null : TrimForToast(string.Join("\n", array.Skip(1)), 240));
		return (Summary: item, Detail: item2);
	}

	private static string TrimForToast(string value, int maxLength)
	{
		if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
		{
			return value;
		}
		return value.Substring(0, maxLength - 3) + "...";
	}
}
