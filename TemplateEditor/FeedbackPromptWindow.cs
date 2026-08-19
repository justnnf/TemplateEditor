using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using ArcGIS.Desktop.Framework;

namespace TemplateEditor;

internal sealed class FeedbackPromptWindow : Window
{
	private MessageBoxResult _result = MessageBoxResult.None;

	private static bool IsDarkTheme
	{
		get
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Invalid comparison between Unknown and I4
			return (int)FrameworkApplication.ApplicationTheme == 1;
		}
	}

	private static Brush WindowBackgroundBrush => DialogAppearance.Background;

	private static Brush ButtonBackgroundBrush => DialogAppearance.InputBackground;

	private static Brush ButtonHoverBackgroundBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(72, 72, 78)) : new SolidColorBrush(Color.FromRgb(230, 240, 252));

	private static Brush DisabledButtonBackgroundBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(48, 48, 52)) : new SolidColorBrush(Color.FromRgb(235, 235, 235));

	private static Brush PrimaryTextBrush => DialogAppearance.Foreground;

	private static Brush SecondaryTextBrush => IsDarkTheme ? new SolidColorBrush(Color.FromRgb(156, 156, 156)) : new SolidColorBrush(Color.FromRgb(112, 112, 112));

	private static Brush PromptBorderBrush => DialogAppearance.Border;

	private static Brush AccentBrush => DialogAppearance.Accent;

	private static Brush AccentHoverBrush => new SolidColorBrush(Color.FromRgb(32, 128, 224));

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
			PositionNearOwner();
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
		frameworkElementFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(3.0));
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

	private void PositionNearOwner()
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		Rect ownerBounds = GetOwnerBounds();
		base.Left = ClampToVirtualScreen(ownerBounds.Right - base.ActualWidth - 32.0, base.ActualWidth, horizontal: true);
		base.Top = ClampToVirtualScreen(ownerBounds.Bottom - base.ActualHeight - 96.0, base.ActualHeight, horizontal: false);
	}

	private Rect GetOwnerBounds()
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		return (Rect)((base.Owner != null && base.Owner.ActualWidth > 0.0 && base.Owner.ActualHeight > 0.0) ? new Rect(base.Owner.Left, base.Owner.Top, base.Owner.ActualWidth, base.Owner.ActualHeight) : SystemParameters.WorkArea);
	}

	private static double ClampToVirtualScreen(double value, double size, bool horizontal)
	{
		double num = (horizontal ? SystemParameters.VirtualScreenLeft : SystemParameters.VirtualScreenTop) + 8.0;
		double val = num + (horizontal ? SystemParameters.VirtualScreenWidth : SystemParameters.VirtualScreenHeight) - size - 16.0;
		return Math.Max(num, Math.Min(value, val));
	}
}
