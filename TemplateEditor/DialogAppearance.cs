using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;
using ArcGIS.Desktop.Framework;

namespace TemplateEditor;

internal static class DialogAppearance
{
    private static bool IsDark => FrameworkApplication.ApplicationTheme is ApplicationTheme.Dark or ApplicationTheme.HighContrast;

    internal static Brush Background => Brush("#1F2328", "#F5F7F9");
    internal static Brush Foreground => Brush("#F4F7F9", "#17212B");
    internal static Brush InputBackground => Brush("#2B333B", "#FFFFFF");
    internal static Brush Border => Brush("#58636D", "#BFCBD4");
    internal static Brush Accent => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#007AC2"));

    internal static UIElement WithChrome(Window window, string title, UIElement content)
    {
        window.WindowStyle = WindowStyle.None;
        window.Background = Accent;
        window.Foreground = Foreground;
        WindowChrome.SetWindowChrome(window, new WindowChrome
        {
            CaptionHeight = 44,
            ResizeBorderThickness = new Thickness(6),
            CornerRadius = new CornerRadius(12),
            GlassFrameThickness = new Thickness(0),
            UseAeroCaptionButtons = false
        });

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var header = new Border { Background = Accent, CornerRadius = new CornerRadius(11, 11, 0, 0) };
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
        return new Border { Background = Accent, BorderBrush = Accent, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Child = root };
    }

    private static Button ChromeButton(string content, string toolTip)
    {
        var button = new Button { Content = content, ToolTip = toolTip, Width = 40, Height = 44, Padding = new Thickness(0), Background = Brushes.Transparent, Foreground = Brushes.White, BorderBrush = Brushes.Transparent, FontSize = 15 };
        WindowChrome.SetIsHitTestVisibleInChrome(button, true);
        return button;
    }

    private static Brush Brush(string dark, string light) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(IsDark ? dark : light));
}
