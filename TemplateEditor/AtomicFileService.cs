using System;
using System.IO;
using System.Text;

namespace TemplateEditor;

internal static class AtomicFileService
{
	public static string NormalizeJsonFilePath(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			throw new ArgumentException("A JSON file path is required.", "path");
		}
		string fullPath = Path.GetFullPath(path.Trim());
		if (!string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
		{
			throw new ArgumentException("The association rules file must use the .json extension.", "path");
		}
		return fullPath;
	}

	public static void WriteAllText(string path, string contents)
	{
		string fullPath = Path.GetFullPath(path);
		string directoryName = Path.GetDirectoryName(fullPath);
		if (string.IsNullOrWhiteSpace(directoryName))
		{
			throw new InvalidOperationException("The output file must have a valid directory.");
		}
		Directory.CreateDirectory(directoryName);
		string text = Path.Combine(directoryName, Path.GetRandomFileName());
		try
		{
			File.WriteAllText(text, contents ?? string.Empty, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			File.Move(text, fullPath, overwrite: true);
		}
		finally
		{
			if (File.Exists(text))
			{
				File.Delete(text);
			}
		}
	}
}
