using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace TemplateEditor;

internal static class DialogService
{
	private sealed class PlacementProgressPromptScope : IDisposable
	{
		public void Dispose()
		{
			ResumePlacementProgressAfterPrompt();
		}
	}

	private static PlacementProgressWindow _placementProgressWindow;

	private static DispatcherTimer _placementProgressDelayTimer;

	private static string _placementProgressTitle = string.Empty;

	private static string _placementProgressMessage = string.Empty;

	private static bool _placementProgressPending;

	private static int _placementProgressSuspendCount;

	private static bool _restorePlacementProgressAfterPrompt;

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

	public static void BeginPlacementProgress(string title, string message)
	{
	}

	public static void UpdatePlacementProgress(string message)
	{
	}

	public static void EndPlacementProgress()
	{
	}

	private static void ClosePlacementProgressCore()
	{
		DispatcherTimer placementProgressDelayTimer = _placementProgressDelayTimer;
		if (placementProgressDelayTimer != null)
		{
			placementProgressDelayTimer.Stop();
		}
		_placementProgressDelayTimer = null;
		_placementProgressPending = false;
		if (_placementProgressWindow != null)
		{
			_placementProgressWindow.Close();
			_placementProgressWindow = null;
		}
	}

	private static void SchedulePlacementProgressDisplay()
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Expected O, but got Unknown
		if (!_placementProgressPending || _placementProgressSuspendCount > 0)
		{
			return;
		}
		DispatcherTimer placementProgressDelayTimer = _placementProgressDelayTimer;
		if (placementProgressDelayTimer != null)
		{
			placementProgressDelayTimer.Stop();
		}
		_placementProgressDelayTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(550.0)
		};
		_placementProgressDelayTimer.Tick += delegate
		{
			DispatcherTimer placementProgressDelayTimer2 = _placementProgressDelayTimer;
			if (placementProgressDelayTimer2 != null)
			{
				placementProgressDelayTimer2.Stop();
			}
			ShowPlacementProgressCore();
		};
		_placementProgressDelayTimer.Start();
	}

	private static void ShowPlacementProgressCore()
	{
		if (_placementProgressPending && _placementProgressSuspendCount <= 0 && _placementProgressWindow == null)
		{
			PlacementProgressWindow placementProgressWindow = new PlacementProgressWindow(_placementProgressTitle, _placementProgressMessage);
			if (Application.Current?.MainWindow != null)
			{
				placementProgressWindow.Owner = Application.Current.MainWindow;
			}
			_placementProgressWindow = placementProgressWindow;
			placementProgressWindow.Show();
		}
	}

	public static IDisposable SuspendPlacementProgressForPrompt()
	{
		_placementProgressSuspendCount++;
		if (_placementProgressSuspendCount == 1)
		{
			PlacementProgressWindow placementProgressWindow = _placementProgressWindow;
			if (placementProgressWindow != null && placementProgressWindow.IsVisible)
			{
				_restorePlacementProgressAfterPrompt = true;
				_placementProgressWindow.Hide();
			}
		}
		DispatcherTimer placementProgressDelayTimer = _placementProgressDelayTimer;
		if (placementProgressDelayTimer != null)
		{
			placementProgressDelayTimer.Stop();
		}
		return new PlacementProgressPromptScope();
	}

	private static void ResumePlacementProgressAfterPrompt()
	{
		if (_placementProgressSuspendCount != 0)
		{
			_placementProgressSuspendCount--;
			if (_placementProgressSuspendCount == 0 && _restorePlacementProgressAfterPrompt && _placementProgressWindow != null)
			{
				_restorePlacementProgressAfterPrompt = false;
				_placementProgressWindow.Show();
			}
			else if (_placementProgressSuspendCount == 0 && _placementProgressPending && _placementProgressWindow == null)
			{
				SchedulePlacementProgressDisplay();
			}
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
		using (SuspendPlacementProgressForPrompt())
		{
			return feedbackPromptWindow.ShowPrompt();
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
		using (SuspendPlacementProgressForPrompt())
		{
			return feedbackPromptWindow.ShowPrompt();
		}
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
