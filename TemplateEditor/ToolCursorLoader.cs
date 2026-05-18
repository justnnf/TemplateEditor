using System.IO;
using System.Reflection;
using System.Windows.Input;

namespace TemplateEditor;

internal static class ToolCursorLoader
{
	public static Cursor Load(string fileName)
	{
		string cursorPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Images", fileName);
		if (!File.Exists(cursorPath))
		{
			return Cursors.Cross;
		}
		byte[] cursorBytes = File.ReadAllBytes(cursorPath);
		return new Cursor(new MemoryStream(cursorBytes));
	}
}
