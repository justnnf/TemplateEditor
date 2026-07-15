using System;
using System.IO;

namespace TemplateEditor;

internal static class LogService
{
	private const long MaxLogBytes = 1024 * 1024;

	private static readonly object SyncRoot = new object();

	private static string LogDirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FortisAlberta", "TemplateEditor");

	private static string LogFilePath => Path.Combine(LogDirectoryPath, "template-editor.log");

	public static void LogException(string context, Exception exception)
	{
		if (exception == null)
		{
			return;
		}
		Write(context + Environment.NewLine + exception);
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
			// Logging must never interrupt placement or add-in startup.
		}
	}

	private static void RotateIfNeeded()
	{
		FileInfo logFile = new FileInfo(LogFilePath);
		if (!logFile.Exists || logFile.Length < MaxLogBytes)
		{
			return;
		}
		string backupPath = Path.Combine(LogDirectoryPath, "template-editor.previous.log");
		if (File.Exists(backupPath))
		{
			File.Delete(backupPath);
		}
		logFile.MoveTo(backupPath);
	}
}
