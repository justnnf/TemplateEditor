using System;
using System.IO;

namespace TemplateEditor;

internal static class LogService
{
	private const long MaxLogBytes = 1048576L;

	private static readonly object SyncRoot = new object();

	private static string LogDirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FortisAlberta", "TemplateEditor");

	private static string LogFilePath => Path.Combine(LogDirectoryPath, "template-editor.log");

	public static void LogException(string context, Exception exception)
	{
		if (exception != null)
		{
			Write(context + Environment.NewLine + exception);
		}
	}

	public static void Write(string message)
	{
		if (string.IsNullOrWhiteSpace(message))
		{
			return;
		}
		try
		{
			lock (SyncRoot)
			{
				Directory.CreateDirectory(LogDirectoryPath);
				RotateIfNeeded();
				File.AppendAllText(LogFilePath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message + Environment.NewLine + Environment.NewLine);
			}
		}
		catch
		{
		}
	}

	private static void RotateIfNeeded()
	{
		FileInfo fileInfo = new FileInfo(LogFilePath);
		if (fileInfo.Exists && fileInfo.Length >= 1048576)
		{
			string text = Path.Combine(LogDirectoryPath, "template-editor.previous.log");
			if (File.Exists(text))
			{
				File.Delete(text);
			}
			fileInfo.MoveTo(text);
		}
	}
}
