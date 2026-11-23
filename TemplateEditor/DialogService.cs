using System;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;
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

	public static MessageBoxResult ShowError(string message, string title)
	{
		return ProMessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
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
					Message = message ?? string.Empty,
					ImageSource = CreateNotificationIcon(severity)
				});
			}, Array.Empty<object>());
		}
	}

	private static ImageSource CreateNotificationIcon(FeedbackSeverity severity)
	{
		string path = severity switch
		{
			FeedbackSeverity.Success => "M 3,16 L 12,25 L 29,7",
			FeedbackSeverity.Warning => "M 16,4 L 29,28 L 3,28 Z M 16,12 L 16,19 M 16,23 L 16,24",
			FeedbackSeverity.Error => "M 6,6 L 26,26 M 26,6 L 6,26",
			_ => null
		};
		if (path == null)
		{
			return null;
		}

		Brush brush = severity == FeedbackSeverity.Success ? Brushes.ForestGreen
			: severity == FeedbackSeverity.Warning ? Brushes.DarkGoldenrod : Brushes.IndianRed;
		if (SystemParameters.HighContrast)
		{
			brush = SystemColors.WindowTextBrush;
		}
		var pen = new Pen(brush, 3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
		var image = new DrawingImage(new GeometryDrawing(null, pen, Geometry.Parse(path)));
		image.Freeze();
		return image;
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
