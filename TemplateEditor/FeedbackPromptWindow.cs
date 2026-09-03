using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace TemplateEditor;

internal sealed class FeedbackPromptWindow : Window
{
	private MessageBoxResult _result = MessageBoxResult.None;

	private static Brush WindowBackgroundBrush => DialogAppearance.Background;

	private static Brush ButtonBackgroundBrush => DialogAppearance.InputBackground;

	private static Brush ButtonHoverBackgroundBrush => DialogAppearance.ButtonHoverBackground;

	private static Brush DisabledButtonBackgroundBrush => DialogAppearance.ButtonBackground;

	private static Brush PrimaryTextBrush => DialogAppearance.Foreground;

	private static Brush SecondaryTextBrush => DialogAppearance.SecondaryForeground;

	private static Brush PromptBorderBrush => DialogAppearance.Border;

	private static Brush AccentBrush => DialogAppearance.Accent;

	private static Brush AccentHoverBrush => DialogAppearance.AccentHover;

	public FeedbackPromptWindow(string message, string title, MessageBoxButton buttons)
		: this(message, title, GetButtons(buttons))
	{
	}

	public FeedbackPromptWindow(string message, string title, params DialogButtonChoice[] choices)
		: this(message, title, (IEnumerable<DialogButtonChoice>)choices)
	{
	}

	private FeedbackPromptWindow(string message, string title, IEnumerable<DialogButtonChoice> choices)
	{
		base.Title = title;
		base.Width = 430.0;
		base.SizeToContent = SizeToContent.Height;
		base.WindowStartupLocation = WindowStartupLocation.Manual;
		base.ShowInTaskbar = false;
		base.ResizeMode = ResizeMode.NoResize;
		base.Background = WindowBackgroundBrush;
		base.Foreground = PrimaryTextBrush;
		base.Topmost = true;
		base.Content = DialogAppearance.WithChrome(this, title, BuildContent(message, choices));
		base.Loaded += delegate
		{
			WindowPlacementHelper.PositionNearOwnerBottomRight(this);
		};
	}

	public MessageBoxResult ShowPrompt()
	{
		ShowDialog();
		return (_result == MessageBoxResult.None) ? MessageBoxResult.Cancel : _result;
	}

	private UIElement BuildContent(string message, IEnumerable<DialogButtonChoice> choices)
	{
		DockPanel dockPanel = new DockPanel
		{
			LastChildFill = true,
			Margin = new Thickness(14.0),
			Background = WindowBackgroundBrush
		};
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
		};
		foreach (DialogButtonChoice choice in choices)
		{
			Button button = new Button
			{
				Content = choice.Label,
				MinWidth = 86.0,
				Margin = new Thickness(8.0, 0.0, 0.0, 0.0),
				Padding = new Thickness(12.0, 4.0, 12.0, 4.0),
				IsDefault = choice.IsPrimary,
				IsCancel = choice.IsCancel,
				Style = CreatePromptButtonStyle(choice.IsPrimary)
			};
			button.Click += delegate
			{
				_result = choice.Result;
				base.DialogResult = true;
				Close();
			};
			stackPanel.Children.Add(button);
		}
		DockPanel.SetDock(stackPanel, Dock.Bottom);
		dockPanel.Children.Add(stackPanel);
		TextBlock element = new TextBlock
		{
			Text = message,
			TextWrapping = TextWrapping.Wrap,
			Foreground = PrimaryTextBrush,
			MaxHeight = 260.0
		};
		dockPanel.Children.Add(element);
		return dockPanel;
	}

	private static Style CreatePromptButtonStyle(bool primary)
	{
		Brush value = (primary ? AccentBrush : ButtonBackgroundBrush);
		Brush value2 = (primary ? AccentHoverBrush : ButtonHoverBackgroundBrush);
		Brush value3 = (primary ? Brushes.White : PrimaryTextBrush);
		Brush value4 = (primary ? AccentBrush : PromptBorderBrush);
		Style style = new Style(typeof(Button));
		style.Setters.Add(new Setter(Control.BackgroundProperty, value));
		style.Setters.Add(new Setter(Control.ForegroundProperty, value3));
		style.Setters.Add(new Setter(Control.BorderBrushProperty, value4));
		style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1.0)));
		style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
		ControlTemplate controlTemplate = new ControlTemplate(typeof(Button));
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory.SetBinding(Border.BackgroundProperty, new Binding("Background")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		frameworkElementFactory.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		frameworkElementFactory.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		frameworkElementFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(0.0));
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(ContentPresenter));
		frameworkElementFactory2.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		frameworkElementFactory2.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		frameworkElementFactory2.SetBinding(FrameworkElement.MarginProperty, new Binding("Padding")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		controlTemplate.VisualTree = frameworkElementFactory;
		style.Setters.Add(new Setter(Control.TemplateProperty, controlTemplate));
		Trigger trigger = new Trigger
		{
			Property = UIElement.IsMouseOverProperty,
			Value = true
		};
		trigger.Setters.Add(new Setter(Control.BackgroundProperty, value2));
		trigger.Setters.Add(new Setter(Control.ForegroundProperty, value3));
		style.Triggers.Add(trigger);
		Trigger trigger2 = new Trigger
		{
			Property = UIElement.IsEnabledProperty,
			Value = false
		};
		trigger2.Setters.Add(new Setter(Control.ForegroundProperty, SecondaryTextBrush));
		trigger2.Setters.Add(new Setter(Control.BackgroundProperty, DisabledButtonBackgroundBrush));
		style.Triggers.Add(trigger2);
		return style;
	}

	private static DialogButtonChoice[] GetButtons(MessageBoxButton buttons)
	{
		if (1 == 0)
		{
		}
		DialogButtonChoice[] result = buttons switch
		{
			MessageBoxButton.YesNo => new DialogButtonChoice[2]
			{
				new DialogButtonChoice("Yes", MessageBoxResult.Yes, isPrimary: true),
				new DialogButtonChoice("No", MessageBoxResult.No, isPrimary: false, isCancel: true)
			}, 
			MessageBoxButton.OKCancel => new DialogButtonChoice[2]
			{
				new DialogButtonChoice("OK", MessageBoxResult.OK, isPrimary: true),
				new DialogButtonChoice("Cancel", MessageBoxResult.Cancel, isPrimary: false, isCancel: true)
			}, 
			MessageBoxButton.YesNoCancel => new DialogButtonChoice[3]
			{
				new DialogButtonChoice("Yes", MessageBoxResult.Yes, isPrimary: true),
				new DialogButtonChoice("No", MessageBoxResult.No),
				new DialogButtonChoice("Cancel", MessageBoxResult.Cancel, isPrimary: false, isCancel: true)
			}, 
			_ => new DialogButtonChoice[1]
			{
				new DialogButtonChoice("OK", MessageBoxResult.OK, isPrimary: true)
			}, 
		};
		if (1 == 0)
		{
		}
		return result;
	}

}
