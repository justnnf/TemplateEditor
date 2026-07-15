using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcGIS.Desktop.Framework;

namespace TemplateEditor;

internal sealed class TextEntryPromptWindow : Window
{
	private static readonly bool IsDarkTheme = FrameworkApplication.ApplicationTheme == ApplicationTheme.Dark;

	private static readonly Brush WindowBackgroundBrush = IsDarkTheme ? new SolidColorBrush(Color.FromRgb(45, 45, 48)) : new SolidColorBrush(Color.FromRgb(243, 243, 243));

	private static readonly Brush SurfaceBackgroundBrush = IsDarkTheme ? new SolidColorBrush(Color.FromRgb(31, 31, 31)) : Brushes.White;

	private static readonly Brush BorderBrushColor = IsDarkTheme ? new SolidColorBrush(Color.FromRgb(72, 72, 72)) : new SolidColorBrush(Color.FromRgb(208, 208, 208));

	private static readonly Brush TextBrush = IsDarkTheme ? new SolidColorBrush(Color.FromRgb(238, 238, 238)) : new SolidColorBrush(Color.FromRgb(32, 32, 32));

	private static readonly Brush SecondaryTextBrush = IsDarkTheme ? new SolidColorBrush(Color.FromRgb(205, 205, 205)) : new SolidColorBrush(Color.FromRgb(96, 96, 96));

	private static readonly Brush AccentBrush = new SolidColorBrush(Color.FromRgb(51, 153, 255));

	private readonly TextBox _textBox;

	public string EnteredText => _textBox.Text?.Trim();

	private TextEntryPromptWindow(string title, string prompt, string initialValue)
	{
		Title = title;
		Width = 420.0;
		Height = 180.0;
		MinWidth = 380.0;
		MinHeight = 170.0;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		Background = WindowBackgroundBrush;
		Foreground = TextBrush;
		FontFamily = new FontFamily("Segoe UI");
		FontSize = 11.0;

		DockPanel root = new DockPanel();
		Border footer = new Border
		{
			BorderBrush = BorderBrushColor,
			BorderThickness = new Thickness(0.0, 1.0, 0.0, 0.0),
			Padding = new Thickness(10.0)
		};
		DockPanel.SetDock(footer, Dock.Bottom);

		StackPanel buttonPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		Button cancelButton = CreateButton("Cancel", false);
		cancelButton.IsCancel = true;
		cancelButton.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
		Button okButton = CreateButton("Save", true);
		okButton.IsDefault = true;
		okButton.Click += OkButton_Click;
		buttonPanel.Children.Add(cancelButton);
		buttonPanel.Children.Add(okButton);
		footer.Child = buttonPanel;
		root.Children.Add(footer);

		StackPanel content = new StackPanel
		{
			Margin = new Thickness(10.0)
		};
		content.Children.Add(new TextBlock
		{
			Text = prompt,
			Foreground = SecondaryTextBrush,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		});
		_textBox = new TextBox
		{
			Text = initialValue ?? string.Empty,
			Background = SurfaceBackgroundBrush,
			Foreground = TextBrush,
			BorderBrush = BorderBrushColor,
			BorderThickness = new Thickness(1.0),
			Height = 26.0,
			Padding = new Thickness(6.0, 2.0, 6.0, 2.0)
		};
		content.Children.Add(_textBox);
		root.Children.Add(content);
		Content = root;
		Loaded += delegate
		{
			_textBox.Focus();
			_textBox.SelectAll();
		};
	}

	public static string ShowPrompt(string title, string prompt, string initialValue, Window owner)
	{
		TextEntryPromptWindow window = new TextEntryPromptWindow(title, prompt, initialValue)
		{
			Owner = owner
		};
		return window.ShowDialog() == true ? window.EnteredText : null;
	}

	private void OkButton_Click(object sender, RoutedEventArgs e)
	{
		if (string.IsNullOrWhiteSpace(EnteredText))
		{
			DialogService.Show("Enter a favourite name.", "Template Editor");
			return;
		}
		DialogResult = true;
		Close();
	}

	private static Button CreateButton(string label, bool primary)
	{
		Button button = new Button
		{
			Content = label,
			Width = 82.0,
			Height = 28.0,
			Background = primary ? AccentBrush : SurfaceBackgroundBrush,
			Foreground = primary ? Brushes.White : TextBrush,
			BorderBrush = primary ? AccentBrush : BorderBrushColor,
			BorderThickness = new Thickness(1.0),
			Padding = new Thickness(10.0, 0.0, 10.0, 0.0)
		};
		return button;
	}
}
