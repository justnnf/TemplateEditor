using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
            choices = new ListBox { ItemsSource = labels, SelectedIndex = 0, MaxHeight = 170 };
            choices.SetResourceReference(FrameworkElement.StyleProperty, "Esri_ListBox");
            System.Windows.Automation.AutomationProperties.SetName(choices, title);
            choices.SelectionChanged += (_, _) =>
            {
                if (choices.SelectedIndex >= 0)
                    selectionChanged?.Invoke(choices.SelectedIndex);
            };
            return Form(message, choices, "Use Selected", "Cancel", finish,
                () => choices.SelectedIndex >= 0);
        });
        return accepted ? choices.SelectedIndex : null;
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

    public static async Task<bool> ShowAsync(MapView map, string title, Func<Action<bool>, FrameworkElement> buildContent)
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
            var panel = new StackPanel { Width = 340, Margin = new Thickness(12) };
            panel.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold });
            panel.Children.Add(buildContent(result => answer.TrySetResult(result)));

            var border = new Border { BorderThickness = new Thickness(1), Child = panel, Focusable = true };
            border.SetResourceReference(Border.BackgroundProperty, "Esri_ControlBackgroundBrush");
            border.SetResourceReference(Border.BorderBrushProperty, "Esri_BorderBrush");
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

            var overlay = new MapViewOverlayControl(border, false, false, false, anchor, x, y)
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
                            map.RemoveOverlayControl(overlay);
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
        panel.Children.Add(new ScrollViewer
        {
            MaxHeight = 104,
            Margin = new Thickness(0, 6, 0, 8),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Focusable = false,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }
        });
        if (input != null)
            panel.Children.Add(input);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 6, 0, 0)
        };
        var cancel = MakeButton(cancelLabel);
        cancel.Click += (_, _) => finish(false);
        actions.Children.Add(cancel);
        var confirm = MakeButton(confirmLabel);
        confirm.Margin = new Thickness(6, 0, 0, 0);
        confirm.Click += (_, _) =>
        {
            if (canConfirm?.Invoke() != false)
                finish(true);
        };
        actions.Children.Add(confirm);
        panel.Children.Add(actions);
        return panel;
    }

    private static Button MakeButton(string label)
    {
        var button = new Button { Content = label, Padding = new Thickness(10, 4, 10, 4) };
        button.SetResourceReference(FrameworkElement.StyleProperty, "Esri_Button");
        return button;
    }
}
