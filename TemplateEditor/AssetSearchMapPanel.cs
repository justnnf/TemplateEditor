using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Framework;

namespace TemplateEditor;

internal static class AssetSearchMapPanel
{
	private static MapView _map;
	private static MapViewOverlayControl _overlay;
	private static AssetSearchInputView _search;

	internal static void TrackLocation(FrameworkElement element, Action<bool> update)
	{
		void Refresh()
		{
			if (!element.Dispatcher.CheckAccess())
			{
				element.Dispatcher.BeginInvoke((Action)Refresh);
				return;
			}

			bool floating = AddinConfiguration.Settings?.FloatFindAssetInMap == true;
			if (!floating)
				Close();
			update(floating);
		}

		element.Loaded += (_, _) =>
		{
			AddinConfiguration.SettingsChanged += Refresh;
			Refresh();
		};
		element.Unloaded += (_, _) => AddinConfiguration.SettingsChanged -= Refresh;
		Refresh();
	}

	internal static void UpdateLocation()
	{
		var dispatcher = Application.Current.Dispatcher;
		if (!dispatcher.CheckAccess())
		{
			dispatcher.BeginInvoke(new Action(UpdateLocation));
			return;
		}

		const string state = "TemplateEditor_FindAssetFloatingState";
		if (AddinConfiguration.Settings?.FloatFindAssetInMap == true)
		{
			FrameworkApplication.State.Activate(state);
			_search?.RefreshFields();
		}
		else
		{
			FrameworkApplication.State.Deactivate(state);
			Close();
		}
	}

	internal static void ToggleFromCommand()
	{
		Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
		{
			if (AddinConfiguration.Settings?.FloatFindAssetInMap != true)
			{
				DialogService.ShowToast("Set Find Asset location to Floating in map under Settings → Find Asset.",
					"Find Asset", FeedbackSeverity.Info);
				return;
			}

			try
			{
				Toggle();
			}
			catch (Exception exception)
			{
				LogService.LogException("Could not toggle floating Find Asset.", exception);
				DialogService.ShowToast("Could not open the Find Asset panel. See the Template Editor log.",
					"Find Asset", FeedbackSeverity.Error);
			}
		}));
	}

	private static void Toggle()
	{
		if (_overlay != null)
		{
			Close();
			return;
		}

		var map = MapView.Active;
		if (map == null)
		{
			DialogService.ShowToast("Activate a map view to open Find Asset.", "Find Asset", FeedbackSeverity.Info);
			return;
		}

		var results = new AssetSearchResultsView(floating: true);
		var search = new AssetSearchInputView(results.Show) { HorizontalAlignment = HorizontalAlignment.Left };
		var close = CreateIconButton("Close Find Asset", "M 4,4 L 12,12 M 12,4 L 4,12", Color.FromRgb(171, 49, 52));
		close.Click += (_, _) => Close();

		double mapWidth = map.GetViewSize().Width;
		var searchAndResults = new WrapPanel
		{
			MaxWidth = mapWidth >= 575 || mapWidth <= 0 ? 520 : 280,
			Children = { search, results }
		};
		const string collapseLabel = "Collapse Find Asset";
		const string expandLabel = "Expand Find Asset";
		const string minus = "M 3,8 L 13,8";
		const string plus = "M 3,8 L 13,8 M 8,3 L 8,13";
		var collapse = CreateIconButton(collapseLabel, minus, Color.FromRgb(94, 94, 94));
		var collapseIcon = (System.Windows.Shapes.Path)collapse.Content;
		var collapsedLabel = new TextBlock
		{
			Text = "Find Asset",
			FontWeight = FontWeights.SemiBold,
			MinWidth = 68,
			Margin = new Thickness(10, 11, 47, 6),
			Visibility = Visibility.Collapsed
		};

		var controls = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Top
		};
		collapse.Margin = new Thickness(0, 0, 3, 0);
		controls.Children.Add(collapse);
		controls.Children.Add(close);

		var surface = PlacementMapPrompt.CreateTranslucentSurface();
		surface.Margin = new Thickness(0, 7, 5, 0);
		searchAndResults.Margin = new Thickness(8, 22, 13, 8);
		if (!Enum.TryParse(AddinConfiguration.Settings?.FindAssetMapPosition, out OverlayControlRelativePosition anchor))
			anchor = OverlayControlRelativePosition.TopRight;

		double expandedWidth = searchAndResults.MaxWidth + searchAndResults.Margin.Left + searchAndResults.Margin.Right;
		var chrome = new Grid
		{
			Width = expandedWidth,
			HorizontalAlignment = anchor switch
			{
				OverlayControlRelativePosition.TopLeft or OverlayControlRelativePosition.BottomLeft => HorizontalAlignment.Left,
				OverlayControlRelativePosition.TopCenter or OverlayControlRelativePosition.BottomCenter => HorizontalAlignment.Center,
				_ => HorizontalAlignment.Right
			},
			Children = { surface, searchAndResults, collapsedLabel, controls }
		};
		var frame = new Grid { Width = expandedWidth, MinWidth = expandedWidth, Children = { chrome } };
		frame.SetResourceReference(TextBlock.ForegroundProperty, "Esri_TextControlBrush");
		frame.Loaded += (_, _) => search.FocusSearch();

		collapse.Click += (_, _) =>
		{
			bool expand = searchAndResults.Visibility == Visibility.Collapsed;
			chrome.Width = expand ? expandedWidth : double.NaN;
			searchAndResults.Visibility = expand ? Visibility.Visible : Visibility.Collapsed;
			collapsedLabel.Visibility = expand ? Visibility.Collapsed : Visibility.Visible;
			collapseIcon.Data = Geometry.Parse(expand ? minus : plus);
			collapse.ToolTip = expand ? collapseLabel : expandLabel;
			AutomationProperties.SetName(collapse, (string)collapse.ToolTip);
			if (expand)
				search.FocusSearch();
		};

		double initialX = anchor switch
		{
			OverlayControlRelativePosition.TopLeft or OverlayControlRelativePosition.BottomLeft => 0,
			OverlayControlRelativePosition.TopCenter or OverlayControlRelativePosition.BottomCenter => 0.5,
			_ => 1
		};
		double initialY = anchor is OverlayControlRelativePosition.BottomLeft or OverlayControlRelativePosition.BottomCenter
			or OverlayControlRelativePosition.BottomRight ? 1 : 0;

		var overlay = new MapViewOverlayControl(frame, true, false, false, anchor, initialX, initialY)
		{
			Margin = new Thickness(6)
		};
		map.AddOverlayControl(overlay);
		_map = map;
		_overlay = overlay;
		_search = search;
	}

	internal static void OnMapChanged(MapView incoming)
	{
		var dispatcher = Application.Current?.Dispatcher;
		if (dispatcher != null && !dispatcher.CheckAccess())
		{
			dispatcher.BeginInvoke((Action)(() => OnMapChanged(incoming)));
			return;
		}

		if (_map != null && !ReferenceEquals(_map, incoming))
			Close();
	}

	internal static void Close()
	{
		var map = _map;
		var overlay = _overlay;
		_search?.ResetSearch();
		_search = null;
		_map = null;
		_overlay = null;
		if (overlay != null)
			map.RemoveOverlayControl(overlay);
	}

	private static Button CreateIconButton(string label, string geometry, Color background)
	{
		var icon = new System.Windows.Shapes.Path
		{
			Data = Geometry.Parse(geometry),
			Width = 9,
			Height = 9,
			Stretch = Stretch.Uniform,
			StrokeThickness = 1.2
		};
		bool highContrast = SystemParameters.HighContrast;
		icon.Stroke = highContrast ? SystemColors.ControlTextBrush : Brushes.White;
		var button = new Button { Content = icon, Width = 18, Height = 18, Padding = new Thickness(0), ToolTip = label };
		var circle = new FrameworkElementFactory(typeof(Border), "Circle");
		circle.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
		circle.SetValue(Border.BackgroundProperty, highContrast ? SystemColors.ControlBrush : new SolidColorBrush(background));
		circle.SetValue(Border.BorderBrushProperty, highContrast ? SystemColors.ControlTextBrush : Brushes.WhiteSmoke);
		circle.SetValue(Border.BorderThicknessProperty, new Thickness(1));
		var content = new FrameworkElementFactory(typeof(ContentPresenter));
		content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		circle.AppendChild(content);
		var template = new ControlTemplate(typeof(Button)) { VisualTree = circle };
		var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
		hover.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(2), "Circle"));
		template.Triggers.Add(hover);
		button.Template = template;
		AutomationProperties.SetName(button, label);
		return button;
	}
}

internal sealed class OpenFloatingFindAssetButton : ArcGIS.Desktop.Framework.Contracts.Button
{
	protected override void OnClick() => AssetSearchMapPanel.ToggleFromCommand();
}
