using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Data;
using ArcGIS.Desktop.Framework.Events;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;

namespace TemplateEditor;

internal static class PlacementMapPrompt
{
    private static bool _isOpen;

    public static bool UseMap => AddinConfiguration.Settings?.PlacementPromptPosition != "Window";

    public static Task<bool> ConfirmAsync(MapView map, string title, string message, string confirmLabel, string cancelLabel)
        => ShowAsync(map, title, finish => Form(message, null, confirmLabel, cancelLabel, finish));

    public static async Task<int?> ChooseAsync(MapView map, string title, string message,
        System.Collections.Generic.IReadOnlyList<string> labels, Action<int> selectionChanged = null)
    {
        ListBox choices = null;
        bool accepted = await ShowAsync(map, title, finish =>
        {
            choices = new ListBox { ItemsSource = labels, SelectedIndex = 0, MaxHeight = 240 };
            ConfigureChoiceList(choices);
            System.Windows.Automation.AutomationProperties.SetName(choices, title);
            choices.SelectionChanged += (_, _) =>
            {
                if (choices.SelectedIndex >= 0)
                    selectionChanged?.Invoke(choices.SelectedIndex);
            };
            return Form(message, choices, "Use Selected", "Cancel", finish,
                () => choices.SelectedIndex >= 0);
        }, width: selectionChanged == null ? 280 : 400);
        return accepted ? choices.SelectedIndex : null;
    }

    internal static void ConfigureChoiceList(ListBox choices)
    {
        choices.SetResourceReference(Control.BackgroundProperty, "Esri_ControlBackgroundBrush");
        choices.SetResourceReference(Control.ForegroundProperty, "Esri_TextControlBrush");
        choices.SetResourceReference(Control.BorderBrushProperty, "Esri_BorderBrush");
        choices.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        ScrollViewer.SetHorizontalScrollBarVisibility(choices, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(choices, ScrollBarVisibility.Auto);

        var label = new FrameworkElementFactory(typeof(TextBlock));
        label.SetBinding(TextBlock.TextProperty, new Binding());
        label.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        choices.ItemTemplate = new DataTemplate { VisualTree = label };
    }

    public static async Task<(bool Start, bool End)> ChooseSplitAsync(MapView map, bool hasStart, bool hasEnd)
    {
        CheckBox start = null;
        CheckBox end = null;
        bool accepted = await ShowAsync(map, "Split Underlying Line", finish =>
        {
            var choices = new StackPanel();
            if (hasStart)
            {
                start = new CheckBox { Content = "Insert/start point", IsChecked = true };
                choices.Children.Add(start);
            }
            if (hasEnd)
            {
                end = new CheckBox { Content = "End point", IsChecked = true };
                choices.Children.Add(end);
            }
            return Form("Choose which points on the placed line should split the underlying line.",
                choices, "Continue", "Skip", finish);
        });
        return accepted ? (start?.IsChecked == true, end?.IsChecked == true) : default;
    }

    public static async Task<bool> ShowAsync(MapView map, string title, Func<Action<bool>, FrameworkElement> buildContent, double width = 280)
    {
        return await (await Application.Current.Dispatcher.InvokeAsync(ShowOnUiAsync));

        async Task<bool> ShowOnUiAsync()
        {
            if (_isOpen || map == null || !ReferenceEquals(MapView.Active, map))
                return false;

            if (!Enum.TryParse(AddinConfiguration.Settings?.PlacementPromptPosition, out OverlayControlRelativePosition anchor))
                anchor = OverlayControlRelativePosition.BottomCenter;

            double x = anchor switch
            {
                OverlayControlRelativePosition.TopLeft or OverlayControlRelativePosition.BottomLeft => 0,
                OverlayControlRelativePosition.TopRight or OverlayControlRelativePosition.BottomRight => 1,
                _ => 0.5
            };
            double y = anchor is OverlayControlRelativePosition.TopLeft or OverlayControlRelativePosition.TopCenter
                or OverlayControlRelativePosition.TopRight ? 0 : 1;

            var answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            double mapWidth = map.GetViewSize().Width;
            double contentWidth = mapWidth > 64 ? Math.Min(width, mapWidth - 48) : width;
            var previousFocus = Keyboard.FocusedElement;
            var panel = new StackPanel { Width = contentWidth, Margin = new Thickness(8) };
            panel.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold });
            panel.Children.Add(buildContent(result => answer.TrySetResult(result)));

            var surface = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Opacity = SystemParameters.HighContrast ? 1 : 0.76,
                IsHitTestVisible = false
            };
            surface.SetResourceReference(Border.BackgroundProperty, "Esri_ControlBackgroundBrush");
            surface.SetResourceReference(Border.BorderBrushProperty, "Esri_BorderBrush");

            var border = new Grid { Focusable = true };
            border.Children.Add(surface);
            border.Children.Add(panel);
            border.SetResourceReference(TextBlock.ForegroundProperty, "Esri_TextControlBrush");
            border.PreviewKeyDown += (_, args) =>
            {
                if (args.Key == Key.Escape)
                {
                    answer.TrySetResult(false);
                    args.Handled = true;
                }
            };
            border.Loaded += (_, _) => border.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));

            var overlay = new MapViewOverlayControl(border, true, false, false, anchor, x, y)
            {
                Margin = new Thickness(12)
            };
            _isOpen = true;
            try
            {
                var viewToken = ActiveMapViewChangedEvent.Subscribe(args =>
                {
                    if (!ReferenceEquals(args.IncomingView, map))
                        answer.TrySetResult(false);
                }, true);
                try
                {
                    var toolToken = ActiveToolChangedEvent.Subscribe(_ => answer.TrySetResult(false), true);
                    try
                    {
                        map.AddOverlayControl(overlay);
                        try
                        {
                            return await answer.Task;
                        }
                        finally
                        {
                            bool restoreFocus = border.IsKeyboardFocusWithin && ReferenceEquals(MapView.Active, map);
                            map.RemoveOverlayControl(overlay);
                            if (restoreFocus && previousFocus != null)
                                Keyboard.Focus(previousFocus);
                        }
                    }
                    finally
                    {
                        ActiveToolChangedEvent.Unsubscribe(toolToken);
                    }
                }
                finally
                {
                    ActiveMapViewChangedEvent.Unsubscribe(viewToken);
                }
            }
            finally
            {
                _isOpen = false;
            }
        }
    }

    private static FrameworkElement Form(string message, FrameworkElement input, string confirmLabel, string cancelLabel,
        Action<bool> finish, Func<bool> canConfirm = null)
    {
        var panel = new StackPanel();
        var messageScroll = new ScrollViewer
        {
            MaxHeight = 200,
            Margin = new Thickness(0, 4, 0, 2),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Focusable = false,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }
        };
        System.Windows.Automation.AutomationProperties.SetName(messageScroll, "Prompt details");
        messageScroll.ScrollChanged += (_, _) => messageScroll.Focusable = messageScroll.ScrollableHeight > 0;
        panel.Children.Add(messageScroll);
        if (input != null)
            panel.Children.Add(input);

        panel.Children.Add(CreateActions(cancelLabel, (_, _) => finish(false), confirmLabel, (_, _) =>
        {
            if (canConfirm?.Invoke() != false)
                finish(true);
        }));
        return panel;
    }

    internal static UniformGrid CreateActions(string cancelLabel, RoutedEventHandler cancelClick,
        string confirmLabel, RoutedEventHandler confirmClick)
    {
        var row = new UniformGrid
        {
            Columns = 2,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 6, 0, 0)
        };
        row.Children.Add(MakeButton(cancelLabel, cancelClick, new Thickness(0, 0, 4, 0)));
        row.Children.Add(MakeButton(confirmLabel, confirmClick, new Thickness(4, 0, 0, 0)));
        return row;
    }

    private static Button MakeButton(string label, RoutedEventHandler click, Thickness margin)
    {
        var button = new Button
        {
            Content = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center },
            MinHeight = 28,
            Margin = margin,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Padding = new Thickness(4, 0, 4, 0)
        };
        System.Windows.Automation.AutomationProperties.SetName(button, label);
        button.SetResourceReference(FrameworkElement.StyleProperty, "Esri_Button");
        button.Click += click;
        return button;
    }
}
