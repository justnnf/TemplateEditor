using System.Windows;

namespace TemplateEditor;

internal sealed class DialogButtonChoice
{
	public string Label { get; }

	public MessageBoxResult Result { get; }

	public bool IsPrimary { get; }

	public bool IsCancel { get; }

	public DialogButtonChoice(string label, MessageBoxResult result, bool isPrimary = false, bool isCancel = false)
	{
		Label = label;
		Result = result;
		IsPrimary = isPrimary;
		IsCancel = isCancel;
	}
}
