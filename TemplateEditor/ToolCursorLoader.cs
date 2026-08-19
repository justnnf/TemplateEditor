using System.IO;
using System.Reflection;
using System.Windows.Input;

namespace TemplateEditor;

internal static class ToolCursorLoader
{
	public static Cursor Load(string fileName)
	{
		string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Images", fileName);
		if (!File.Exists(path))
		{
			return Cursors.Cross;
		}
		byte[] buffer = File.ReadAllBytes(path);
		return new Cursor(new MemoryStream(buffer));
	}
}
