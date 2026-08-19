using System;
using System.Windows.Input;

namespace TemplateEditor;

internal sealed class RelayCommand : ICommand
{
	private readonly Action<object> _execute;

	public event EventHandler CanExecuteChanged
	{
		add
		{
		}
		remove
		{
		}
	}

	public RelayCommand(Action<object> execute)
	{
		_execute = execute ?? throw new ArgumentNullException("execute");
	}

	public bool CanExecute(object parameter)
	{
		return true;
	}

	public void Execute(object parameter)
	{
		_execute(parameter);
	}
}
