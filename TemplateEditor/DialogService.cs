using System.Windows;

namespace TemplateEditor;

internal static class DialogService
{
	public static MessageBoxResult Show(string message, string title)
	{
		return Show(message, title, MessageBoxButton.OK);
	}

	public static MessageBoxResult Show(string message, string title, MessageBoxButton buttons)
	{
		Window mainWindow = Application.Current?.MainWindow;
		return mainWindow == null ? MessageBox.Show(message, title, buttons) : MessageBox.Show(mainWindow, message, title, buttons);
	}
}
