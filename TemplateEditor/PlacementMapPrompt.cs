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
    public static async Task<bool> ConfirmAsync(MapView map, string title, string message, string confirmLabel, string cancelLabel)
    {
        var dispatcher = Application.Current.Dispatcher;
        return await (await dispatcher.InvokeAsync(ShowAsync));

        async Task<bool> ShowAsync()
        {
            if (!ReferenceEquals(MapView.Active, map))
            {
                return false;
            }

            string position = AddinConfiguration.Settings?.PlacementPromptPosition;
            if (!Enum.TryParse(position, out OverlayControlRelativePosition anchor))
            {
                anchor = OverlayControlRelativePosition.BottomCenter;
            }
            double x = anchor switch
            {
                OverlayControlRelativePosition.TopLeft or OverlayControlRelativePosition.BottomLeft => 0,
                OverlayControlRelativePosition.TopRight or OverlayControlRelativePosition.BottomRight => 1,
                _ => 0.5
            };
            double y = anchor is OverlayControlRelativePosition.TopLeft or OverlayControlRelativePosition.TopCenter
                or OverlayControlRelativePosition.TopRight ? 0 : 1;

            var answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var panel = new StackPanel { Width = 320, Margin = new Thickness(12) };
            panel.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold });
            panel.Children.Add(new ScrollViewer
            {
                MaxHeight = 132,
                Margin = new Thickness(0, 6, 0, 10),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }
            });
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = MakeButton(cancelLabel, false);
            actions.Children.Add(cancel);
            var confirm = MakeButton(confirmLabel, true);
            confirm.Margin = new Thickness(6, 0, 0, 0);
            actions.Children.Add(confirm);
            panel.Children.Add(actions);

            var border = new Border { BorderThickness = new Thickness(1), Child = panel };
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
            border.Loaded += (_, _) => cancel.Focus();

            var overlay = new MapViewOverlayControl(border, false, false, false, anchor, x, y)
            {
                Margin = new Thickness(12)
            };
            var viewToken = ActiveMapViewChangedEvent.Subscribe(args =>
            {
                if (!ReferenceEquals(args.IncomingView, map))
                {
                    answer.TrySetResult(false);
                }
            });
            try
            {
                var toolToken = ActiveToolChangedEvent.Subscribe(_ => answer.TrySetResult(false));
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

            Button MakeButton(string label, bool result)
            {
                var button = new Button { Content = label, Padding = new Thickness(10, 4, 10, 4) };
                button.SetResourceReference(FrameworkElement.StyleProperty, "Esri_Button");
                button.Click += (_, _) => answer.TrySetResult(result);
                return button;
            }
        }
    }
}
