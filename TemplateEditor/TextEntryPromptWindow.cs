using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TemplateEditor;

internal sealed class TextEntryPromptWindow : Window
{
	private static readonly Brush WindowBackgroundBrush;

	private static readonly Brush SurfaceBackgroundBrush;

	private static readonly Brush BorderBrushColor;

	private static readonly Brush TextBrush;

	private static readonly Brush SecondaryTextBrush;

	private static readonly Brush AccentBrush;

	private readonly TextBox _textBox;

	public string EnteredText => _textBox.Text?.Trim();

	private TextEntryPromptWindow(string title, string prompt, string initialValue, bool openAwayFromMapCenter)
	{
		base.Title = title;
		base.Width = 420.0;
		base.MinWidth = 380.0;
		base.SizeToContent = SizeToContent.Height;
		base.WindowStartupLocation = openAwayFromMapCenter ? WindowStartupLocation.Manual : WindowStartupLocation.CenterOwner;
		base.Background = WindowBackgroundBrush;
		base.Foreground = TextBrush;
		base.FontFamily = new FontFamily("Segoe UI");
		base.FontSize = 11.0;
		DockPanel dockPanel = new DockPanel();
		Border border = new Border
		{
			BorderBrush = BorderBrushColor,
			BorderThickness = new Thickness(0.0, 1.0, 0.0, 0.0),
			Padding = new Thickness(10.0)
		};
		DockPanel.SetDock(border, Dock.Bottom);
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		Button button = CreateButton("Cancel", primary: false);
		button.IsCancel = true;
		button.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
		Button button2 = CreateButton("Save", primary: true);
		button2.IsDefault = true;
		button2.Click += OkButton_Click;
		stackPanel.Children.Add(button);
		stackPanel.Children.Add(button2);
		border.Child = stackPanel;
		dockPanel.Children.Add(border);
		StackPanel stackPanel2 = new StackPanel
		{
			Margin = new Thickness(10.0)
		};
		stackPanel2.Children.Add(new TextBlock
		{
			Text = prompt,
			Foreground = SecondaryTextBrush,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		});
		_textBox = new TextBox
		{
			Text = (initialValue ?? string.Empty),
			Background = SurfaceBackgroundBrush,
			Foreground = TextBrush,
			BorderBrush = BorderBrushColor,
			BorderThickness = new Thickness(1.0),
			Height = 26.0,
			Padding = new Thickness(6.0, 2.0, 6.0, 2.0)
		};
		stackPanel2.Children.Add(_textBox);
		dockPanel.Children.Add(stackPanel2);
		base.Content = DialogAppearance.WithChrome(this, title, dockPanel);
		base.Loaded += delegate
		{
			if (openAwayFromMapCenter)
			{
				WindowPlacementHelper.PositionAwayFromMapCenter(this);
			}
			_textBox.Focus();
			_textBox.SelectAll();
		};
	}

	public static string ShowPrompt(string title, string prompt, string initialValue, Window owner, bool openAwayFromMapCenter = false)
	{
		TextEntryPromptWindow textEntryPromptWindow = new TextEntryPromptWindow(title, prompt, initialValue, openAwayFromMapCenter)
		{
			Owner = owner
		};
		return (textEntryPromptWindow.ShowDialog() == true) ? textEntryPromptWindow.EnteredText : null;
	}

	private void OkButton_Click(object sender, RoutedEventArgs e)
	{
		if (string.IsNullOrWhiteSpace(EnteredText))
		{
			DialogService.Show("Enter a favourite name.", "Template Editor");
			return;
		}
		base.DialogResult = true;
		Close();
	}

	private static Button CreateButton(string label, bool primary)
	{
		Style style = new Style(typeof(Button));
		style.Setters.Add(new Setter(Control.BackgroundProperty, primary ? AccentBrush : SurfaceBackgroundBrush));
		style.Setters.Add(new Setter(Control.ForegroundProperty, primary ? Brushes.White : TextBrush));
		style.Setters.Add(new Setter(Control.BorderBrushProperty, primary ? AccentBrush : BorderBrushColor));
		style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1.0)));
		style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
		DialogAppearance.ApplySquareButtonTemplate(style);
		return new Button
		{
			Content = label,
			Width = 82.0,
			Height = 28.0,
			Padding = new Thickness(10.0, 0.0, 10.0, 0.0),
			Style = style
		};
	}

	static TextEntryPromptWindow()
	{
		WindowBackgroundBrush = DialogAppearance.Background;
		SurfaceBackgroundBrush = DialogAppearance.InputBackground;
		BorderBrushColor = DialogAppearance.SectionBorder;
		TextBrush = DialogAppearance.Foreground;
		SecondaryTextBrush = DialogAppearance.SecondaryForeground;
		AccentBrush = DialogAppearance.Accent;
	}
}
