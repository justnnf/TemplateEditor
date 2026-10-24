using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ArcGIS.Desktop.Framework.Contracts;

namespace TemplateEditor;

internal sealed class AssetSearchInputControl : CustomControl
{
	protected override FrameworkElement OnCreateContent() => new AssetSearchInputView();
}

internal sealed class AssetSearchInputView : UserControl
{
	private readonly System.Windows.Controls.ComboBox _field;
	private readonly TextBox _value;
	private bool _searching;
	private string _lastSearchKey;
	private DateTime _lastSearchStarted;

	public AssetSearchInputView()
	{
		const double width = 230;
		StackPanel layout = new StackPanel { Width = width, Height = 66, Margin = new Thickness(0, 0, 6, 0) };
		_field = new System.Windows.Controls.ComboBox
		{
			Width = width,
			Height = 28,
			BorderThickness = new Thickness(1),
			Padding = new Thickness(8, 2, 6, 2),
			Margin = new Thickness(0, 2, 0, 5),
			ToolTip = "Search by field"
		};
		_field.SetResourceReference(Control.BackgroundProperty, "Esri_ControlBackgroundBrush");
		_field.SetResourceReference(Control.ForegroundProperty, "Esri_TextControlBrush");
		_field.SetResourceReference(Control.BorderBrushProperty, "Esri_BorderBrush");
		_field.Loaded += (_, _) => FlattenCorners(_field);
		_field.DropDownOpened += (_, _) => FlattenCorners(_field);
		AddField("Facility ID", "facilityid");
		AddField("Switch Number", "assetid");
		AddField("Site ID", "SITE_ID");
		AddField("Global ID", "GLOBALID");
		_field.SelectedIndex = 0;
		layout.Children.Add(_field);

		Grid searchBar = new Grid { Width = width, Height = 29 };
		searchBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		searchBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
		Border searchBorder = new Border
		{
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(0),
			Child = searchBar
		};
		searchBorder.SetResourceReference(Border.BackgroundProperty, "Esri_ControlBackgroundBrush");
		searchBorder.SetResourceReference(Border.BorderBrushProperty, "Esri_BorderBrush");
		_value = new TextBox
		{
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0),
			Padding = new Thickness(9, 4, 2, 3),
			ToolTip = "Enter an exact ID and press Enter"
		};
		_value.SetResourceReference(Control.ForegroundProperty, "Esri_TextControlBrush");
		_value.KeyDown += (_, e) =>
		{
			if (e.Key == Key.Return)
			{
				e.Handled = true;
				StartSearch();
			}
		};
		searchBar.Children.Add(_value);
		System.Windows.Controls.Button find = new System.Windows.Controls.Button
		{
			Width = 27,
			Height = 27,
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0),
			Content = SearchGlyph(),
			ToolTip = "Search live layers and tables",
			Cursor = Cursors.Hand
		};
		ControlTemplate iconButton = new ControlTemplate(typeof(System.Windows.Controls.Button));
		FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
		content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		iconButton.VisualTree = content;
		find.Template = iconButton;
		find.Click += (_, _) => StartSearch();
		Grid.SetColumn(find, 1);
		searchBar.Children.Add(find);
		layout.Children.Add(searchBorder);
		Content = layout;
	}

	private void AddField(string label, string name) => _field.Items.Add(new System.Windows.Controls.ComboBoxItem
	{
		Content = label,
		Tag = name
	});

	private static Canvas SearchGlyph()
	{
		Canvas glyph = new Canvas { Width = 15, Height = 15 };
		Ellipse ring = new Ellipse
		{
			Width = 9,
			Height = 9,
			StrokeThickness = 1.6
		};
		ring.SetResourceReference(Shape.StrokeProperty, "Esri_TextControlBrush");
		glyph.Children.Add(ring);
		Line handle = new Line
		{
			X1 = 8,
			Y1 = 8,
			X2 = 14,
			Y2 = 14,
			StrokeThickness = 1.6,
			StrokeStartLineCap = PenLineCap.Round,
			StrokeEndLineCap = PenLineCap.Round
		};
		handle.SetResourceReference(Shape.StrokeProperty, "Esri_TextControlBrush");
		glyph.Children.Add(handle);
		return glyph;
	}

	private static void FlattenCorners(DependencyObject parent)
	{
		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, i);
			if (child is Border border)
			{
				border.CornerRadius = new CornerRadius(0);
			}
			FlattenCorners(child);
		}
	}

	private void StartSearch() => TaskObservationService.Forget(SearchAsync(), "Asset search failed.");

	private async Task SearchAsync()
	{
		string value = _value.Text.Trim();
		System.Windows.Controls.ComboBoxItem selectedField = _field.SelectedItem as System.Windows.Controls.ComboBoxItem;
		string fieldName = selectedField?.Tag as string ?? "facilityid";
		string fieldLabel = selectedField?.Content as string ?? "Facility ID";
		string searchKey = fieldName + "\n" + value;
		if (_searching || string.IsNullOrEmpty(value) ||
			(searchKey == _lastSearchKey && DateTime.UtcNow - _lastSearchStarted < TimeSpan.FromMilliseconds(500)))
		{
			return;
		}

		_searching = true;
		_lastSearchKey = searchKey;
		_lastSearchStarted = DateTime.UtcNow;
		AssetSearchResultsControl.ShowSearching();
		try
		{
			AssetSearchResult result = await AssetSearchService.SearchAsync(fieldName, value);
			AssetSearchResultsControl.ShowResults(result, fieldLabel, value);
			string message = result.MatchCount == 0
				? "No matches found in the current map."
				: $"Found {result.MatchCount} match(es) in the ribbon.";
			if (result.LimitReached)
			{
				message += " Limited to 100 matches; use a more specific value.";
			}
			if (result.FailedTables > 0)
			{
				message += $" {result.FailedTables} layer(s) or table(s) failed; see the Template Editor log.";
			}
			DialogService.ShowToast(message, "Find Asset", result.FailedTables > 0 ? FeedbackSeverity.Warning : FeedbackSeverity.Success);
		}
		catch (Exception ex)
		{
			LogService.LogException("Asset search failed.", ex);
			AssetSearchResultsControl.ShowMessage("Search failed", "See the Template Editor log.");
			DialogService.ShowToast("Search failed; see the Template Editor log.", "Find Asset", FeedbackSeverity.Warning);
		}
		finally
		{
			_searching = false;
		}
	}
}

internal sealed class AssetSearchResultsControl : CustomControl
{
	private static AssetSearchResultsControl _current;
	private static IReadOnlyList<AssetSearchResultCard> _cards = new[]
	{
		new AssetSearchResultCard("No search yet", "Enter an exact ID and press Enter.")
	};
	private AssetSearchResultsView _view;

	public AssetSearchResultsControl()
	{
		_current = this;
	}

	protected override FrameworkElement OnCreateContent()
	{
		_view = new AssetSearchResultsView();
		Refresh();
		return _view;
	}

	internal static void ShowSearching() => ShowMessage("Searching", "Checking live layers and tables...");

	internal static void ShowMessage(string title, string detail)
	{
		_cards = new[] { new AssetSearchResultCard(title, detail) };
		_current?.Refresh();
	}

	internal static void ShowResults(AssetSearchResult result, string fieldName, string value)
	{
		_cards = result.MatchCount == 0
			? new[] { new AssetSearchResultCard("No matches", "Try another ID or field.") }
			: result.Matches.Select(match => new AssetSearchResultCard(match, fieldName, value)).ToArray();
		_current?.Refresh();
	}

	private void Refresh()
	{
		_view?.Show(_cards);
		Caption = _cards.Count == 1 && _cards[0].Match == null ? "Results" : $"Results ({_cards.Count})";
	}
}

internal sealed class AssetSearchResultsView : UserControl
{
	private readonly StackPanel _cards = new StackPanel();

	public AssetSearchResultsView()
	{
		ScrollViewer scroll = new ScrollViewer
		{
			Content = _cards,
			Height = 66,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		};
		Border frame = new Border
		{
			Width = 280,
			Height = 68,
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(0),
			Child = scroll
		};
		frame.SetResourceReference(Border.BackgroundProperty, "Esri_ControlBackgroundBrush");
		frame.SetResourceReference(Border.BorderBrushProperty, "Esri_BorderBrush");
		Content = frame;
	}

	internal void Show(IEnumerable<AssetSearchResultCard> cards)
	{
		_cards.Children.Clear();
		foreach (AssetSearchResultCard card in cards)
		{
			Grid row = new Grid { Height = 21 };
			row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
			TextBlock title = new TextBlock
			{
				Text = card.Title,
				FontSize = 11,
				FontWeight = FontWeights.SemiBold,
				TextTrimming = TextTrimming.CharacterEllipsis,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(7, 0, 3, 0)
			};
			title.SetResourceReference(TextBlock.ForegroundProperty, "Esri_TextControlBrush");
			row.Children.Add(title);
			TextBlock detail = new TextBlock
			{
				Text = card.Detail,
				FontSize = 10,
				TextTrimming = TextTrimming.CharacterEllipsis,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(3, 0, 5, 0)
			};
			detail.SetResourceReference(TextBlock.ForegroundProperty, "Esri_TextStyleSubduedBrush");
			Grid.SetColumn(detail, 1);
			row.Children.Add(detail);
			Border tile = new Border
			{
				BorderThickness = new Thickness(0, 0, 0, 1),
				CornerRadius = new CornerRadius(0),
				ContextMenu = card.Menu,
				ToolTip = string.Join(" · ", new[] { card.Title, card.Detail, card.Identifier }.Where(s => !string.IsNullOrEmpty(s))),
				Child = row
			};
			tile.SetResourceReference(Border.BackgroundProperty, "Esri_ControlBackgroundBrush");
			tile.SetResourceReference(Border.BorderBrushProperty, "Esri_BorderBrush");
			if (card.Match != null)
			{
				tile.Cursor = Cursors.Hand;
				tile.MouseEnter += (_, _) => tile.SetResourceReference(Border.BackgroundProperty, "Esri_DialogFrameBackgroundBrush");
				tile.MouseLeave += (_, _) => tile.SetResourceReference(Border.BackgroundProperty, "Esri_ControlBackgroundBrush");
				tile.MouseLeftButtonUp += (_, e) =>
				{
					e.Handled = true;
					card.Run(AssetSearchService.ResultAction.AddToSelection);
				};
			}
			_cards.Children.Add(tile);
		}
	}
}

public sealed class AssetSearchResultCard
{
	internal AssetSearchMatch Match { get; }
	public string Title { get; }
	public string Detail { get; }
	public string Identifier { get; }
	public ContextMenu Menu { get; }

	internal AssetSearchResultCard(string title, string detail)
	{
		Title = title;
		Detail = detail;
		Identifier = string.Empty;
	}

	internal AssetSearchResultCard(AssetSearchMatch match, string fieldName, string value)
	{
		Match = match;
		Title = match.Source;
		Detail = $"{(match.IsSpatial ? "Feature" : "Row")} · {match.ObjectID}";
		Identifier = $"{fieldName}: {value}";
		Menu = new ContextMenu();
		AddAction("Add to selection", AssetSearchService.ResultAction.AddToSelection);
		if (match.IsSpatial)
		{
			AddAction("Zoom to", AssetSearchService.ResultAction.ZoomTo);
			AddAction("Pan to", AssetSearchService.ResultAction.PanTo);
		}
	}

	internal void Run(AssetSearchService.ResultAction action)
	{
		if (Match != null)
		{
			TaskObservationService.Forget(RunAsync(action), "Asset search result action failed.");
		}
	}

	private void AddAction(string title, AssetSearchService.ResultAction action)
	{
		MenuItem menuItem = new MenuItem { Header = title };
		menuItem.Click += (_, _) => Run(action);
		Menu.Items.Add(menuItem);
	}

	private async Task RunAsync(AssetSearchService.ResultAction action)
	{
		try
		{
			await AssetSearchService.ApplyResultAsync(Match, action);
			if (action == AssetSearchService.ResultAction.AddToSelection)
			{
				DialogService.ShowToast("Added result to the current selection.", "Find Asset", FeedbackSeverity.Success);
			}
		}
		catch (Exception ex)
		{
			LogService.LogException("Asset search result action failed.", ex);
			DialogService.ShowToast(ex.Message, "Find Asset", FeedbackSeverity.Warning);
		}
	}
}
