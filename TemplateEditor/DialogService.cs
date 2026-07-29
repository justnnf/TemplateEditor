using System.Linq;
using System.Windows;

namespace TemplateEditor;

internal static class DialogService
{
	private static PlacementProgressWindow _placementProgressWindow;

	public static MessageBoxResult Show(string message, string title)
	{
		return Show(message, title, MessageBoxButton.OK);
	}

	public static void ShowAsync(string message, string title, FeedbackSeverity? severity = null)
	{
		Application.Current?.Dispatcher.BeginInvoke(new System.Action(() =>
		{
			if (severity == null)
			{
				Show(message, title);
				return;
			}
			ShowToast(message, title, severity.Value);
		}));
	}

	public static MessageBoxResult Show(string message, string title, FeedbackSeverity? severity)
	{
		ShowToast(message, title, severity ?? FeedbackSeverity.Info);
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
		Application.Current?.Dispatcher.BeginInvoke(new System.Action(() =>
		{
			(string summary, string detail) = CreateToastText(message);
			FeedbackToastWindow toast = new FeedbackToastWindow(title, summary, detail, severity);
			toast.Show();
		}));
	}

	public static void BeginPlacementProgress(string title, string message)
	{
		Application.Current?.Dispatcher.BeginInvoke(new System.Action(() =>
		{
			ClosePlacementProgressCore();
			PlacementProgressWindow progressWindow = new PlacementProgressWindow(title, message);
			if (Application.Current?.MainWindow != null)
			{
				progressWindow.Owner = Application.Current.MainWindow;
			}
			_placementProgressWindow = progressWindow;
			progressWindow.Show();
		}));
	}

	public static void UpdatePlacementProgress(string message)
	{
		if (string.IsNullOrWhiteSpace(message))
		{
			return;
		}
		Application.Current?.Dispatcher.BeginInvoke(new System.Action(() =>
		{
			_placementProgressWindow?.SetMessage(message);
		}));
	}

	public static void EndPlacementProgress()
	{
		Application.Current?.Dispatcher.BeginInvoke(new System.Action(ClosePlacementProgressCore));
	}

	private static void ClosePlacementProgressCore()
	{
		if (_placementProgressWindow == null)
		{
			return;
		}
		_placementProgressWindow.Close();
		_placementProgressWindow = null;
	}

	private static MessageBoxResult ShowPrompt(string message, string title, MessageBoxButton buttons)
	{
		Window owner = Application.Current?.MainWindow;
		FeedbackPromptWindow prompt = new FeedbackPromptWindow(message, title, buttons);
		if (owner != null)
		{
			prompt.Owner = owner;
		}
		return prompt.ShowPrompt();
	}

	private static MessageBoxResult ShowPrompt(string message, string title, DialogButtonChoice[] choices)
	{
		Window owner = Application.Current?.MainWindow;
		FeedbackPromptWindow prompt = new FeedbackPromptWindow(message, title, choices);
		if (owner != null)
		{
			prompt.Owner = owner;
		}
		return prompt.ShowPrompt();
	}

	private static (string Summary, string Detail) CreateToastText(string message)
	{
		if (string.IsNullOrWhiteSpace(message))
		{
			return (string.Empty, null);
		}
		string normalized = message.Replace("\r\n", "\n");
		string[] lines = normalized.Split('\n').Select((string line) => line.Trim()).Where((string line) => line.Length > 0).ToArray();
		if (lines.Length == 0)
		{
			return (string.Empty, null);
		}
		string summary = TrimForToast(lines[0], 180);
		string detail = lines.Length <= 1 ? null : TrimForToast(string.Join("\n", lines.Skip(1)), 240);
		return (summary, detail);
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
