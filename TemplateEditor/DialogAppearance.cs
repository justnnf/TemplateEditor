using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shell;
using ArcGIS.Desktop.Framework;

namespace TemplateEditor;

internal static class DialogAppearance
{
    private const double ChromeCaptionHeight = 44;

    private static bool IsDark => FrameworkApplication.ApplicationTheme is ApplicationTheme.Dark or ApplicationTheme.HighContrast;

    internal static Brush Background => Brush("#1F2328", "#F5F7F9");
    internal static Brush Foreground => Brush("#F4F7F9", "#17212B");
    internal static Brush InputBackground => Brush("#2B333B", "#FFFFFF");
    internal static Brush Border => Brush("#58636D", "#BFCBD4");
    internal static Brush SectionBorder => Brush("#484848", "#D0D0D0");
    internal static Brush ControlBorder => Brush("#606064", "#969696");
    internal static Brush ButtonBackground => Brush("#3A3A3E", "#E8E8E8");
    internal static Brush ButtonHoverBackground => Brush("#48484E", "#E1EBF5");
    internal static Brush SecondaryForeground => Brush("#CDCDCD", "#606060");
    internal static Brush Accent => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#007AC2"));
    internal static Brush AccentHover => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2080E0"));

    internal static UIElement WithChrome(Window window, string title, UIElement content)
    {
        // ArcGIS Pro hosts add-ins in its own WPF shell, so each custom dialog owns
        // its title bar and rounded border instead of relying on OS window chrome.
        window.WindowStyle = WindowStyle.None;
        window.Background = Accent;
        window.Foreground = Foreground;
        window.UseLayoutRounding = true;
        window.SnapsToDevicePixels = true;
        WindowChrome.SetWindowChrome(window, new WindowChrome
        {
            CaptionHeight = ChromeCaptionHeight,
            ResizeBorderThickness = new Thickness(6),
            CornerRadius = new CornerRadius(0),
            GlassFrameThickness = new Thickness(0),
            UseAeroCaptionButtons = false
        });

        var root = new Grid { SnapsToDevicePixels = true };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var header = new Border { Background = Accent };
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition());
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.Children.Add(new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 16, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) });
        var minimize = ChromeButton("−", "Minimize");
        minimize.Click += (_, _) => window.WindowState = WindowState.Minimized;
        Grid.SetColumn(minimize, 1);
        headerGrid.Children.Add(minimize);
        var close = ChromeButton("×", "Close");
        close.FontSize = 18;
        close.Click += (_, _) => window.Close();
        Grid.SetColumn(close, 2);
        headerGrid.Children.Add(close);
        header.Child = headerGrid;
        root.Children.Add(header);
        var body = new Border { Background = Background, Child = content };
        Grid.SetRow(body, 1);
        root.Children.Add(body);
        // The outer border owns the accent outline for every custom add-in dialog.
        // Squared chrome avoids the mixed inner/outer corner radii that WPF can clip.
        return new Border { Background = Accent, BorderBrush = Accent, BorderThickness = new Thickness(1), Child = root, SnapsToDevicePixels = true };
    }

    private static Button ChromeButton(string content, string toolTip)
    {
        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
        ApplySquareButtonTemplate(style);
        var button = new Button { Content = content, ToolTip = toolTip, Width = 40, Height = 44, Padding = new Thickness(0), FontSize = 15, Style = style };
        WindowChrome.SetIsHitTestVisibleInChrome(button, true);
        return button;
    }

    internal static void ApplySquareButtonTemplate(Style style)
    {
        if (style == null)
        {
            return;
        }
        ControlTemplate controlTemplate = new ControlTemplate(typeof(Button));
        FrameworkElementFactory borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetBinding(System.Windows.Controls.Border.BackgroundProperty, new Binding("Background")
        {
            RelativeSource = RelativeSource.TemplatedParent
        });
        borderFactory.SetBinding(System.Windows.Controls.Border.BorderBrushProperty, new Binding("BorderBrush")
        {
            RelativeSource = RelativeSource.TemplatedParent
        });
        borderFactory.SetBinding(System.Windows.Controls.Border.BorderThicknessProperty, new Binding("BorderThickness")
        {
            RelativeSource = RelativeSource.TemplatedParent
        });
        FrameworkElementFactory contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        contentFactory.SetBinding(FrameworkElement.MarginProperty, new Binding("Padding")
        {
            RelativeSource = RelativeSource.TemplatedParent
        });
        borderFactory.AppendChild(contentFactory);
        controlTemplate.VisualTree = borderFactory;
        style.Setters.Add(new Setter(Control.TemplateProperty, controlTemplate));
    }

    private static Brush Brush(string dark, string light) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(IsDark ? dark : light));
}
