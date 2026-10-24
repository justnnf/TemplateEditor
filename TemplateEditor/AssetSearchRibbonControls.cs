using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
	private bool _hasSearch;
	private int _searchVersion;
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
			FontSize = 13,
			BorderThickness = new Thickness(1),
			Padding = new Thickness(11, 0, 8, 0),
			HorizontalContentAlignment = HorizontalAlignment.Left,
			VerticalContentAlignment = VerticalAlignment.Center,
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
		searchBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
		searchBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		searchBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
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
			FontSize = 13,
			BorderThickness = new Thickness(0),
			Padding = new Thickness(8, 0, 2, 0),
			HorizontalContentAlignment = HorizontalAlignment.Left,
			VerticalContentAlignment = VerticalAlignment.Center,
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
		Grid.SetColumn(_value, 1);
		searchBar.Children.Add(_value);
		TextBlock searchIndicator = new TextBlock
		{
			Text = "⌕",
			FontSize = 18,
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Center,
			IsHitTestVisible = false
		};
		searchIndicator.SetResourceReference(TextBlock.ForegroundProperty, "Esri_TextStyleSubduedBrush");
		searchBar.Children.Add(searchIndicator);
		System.Windows.Controls.Button clear = new System.Windows.Controls.Button
		{
			Content = "×",
			Width = 20,
			Height = 27,
			FontSize = 17,
			FontWeight = FontWeights.Bold,
			Visibility = Visibility.Collapsed,
			ToolTip = "Clear search and results",
			Cursor = Cursors.Hand,
			Focusable = false
		};
		clear.SetResourceReference(Control.ForegroundProperty, "Esri_TextStyleSubduedBrush");
		clear.Template = IconButtonTemplate();
		clear.Click += (_, _) =>
		{
			_searchVersion++;
			_searching = false;
			_hasSearch = false;
			_lastSearchKey = null;
			_lastSearchStarted = default;
			_value.Clear();
			AssetSearchResultsControl.Clear();
			clear.Visibility = Visibility.Collapsed;
			_value.Focus();
		};
		_value.TextChanged += (_, _) =>
			clear.Visibility = _value.Text.Length > 0 || _hasSearch ? Visibility.Visible : Visibility.Collapsed;
		Grid.SetColumn(clear, 2);
		searchBar.Children.Add(clear);
		layout.Children.Add(searchBorder);
		Content = layout;
	}

	private void AddField(string label, string name) => _field.Items.Add(new System.Windows.Controls.ComboBoxItem
	{
		Content = label,
		Tag = name,
		HorizontalContentAlignment = HorizontalAlignment.Left,
		VerticalContentAlignment = VerticalAlignment.Center
	});

	private static ControlTemplate IconButtonTemplate()
	{
		ControlTemplate template = new ControlTemplate(typeof(System.Windows.Controls.Button));
		FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
		content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		template.VisualTree = content;
		return template;
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
		string searchKey = fieldName + "\n" + value;
		if (_searching || string.IsNullOrEmpty(value) ||
			(searchKey == _lastSearchKey && DateTime.UtcNow - _lastSearchStarted < TimeSpan.FromMilliseconds(500)))
		{
			return;
		}

		_searching = true;
		_hasSearch = true;
		int searchVersion = ++_searchVersion;
		_lastSearchKey = searchKey;
		_lastSearchStarted = DateTime.UtcNow;
		AssetSearchResultsControl.ShowSearching();
		try
		{
			AssetSearchResult result = await AssetSearchService.SearchAsync(fieldName, value);
			if (searchVersion != _searchVersion)
			{
				return;
			}
			AssetSearchResultsControl.ShowResults(result);
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
			if (searchVersion != _searchVersion)
			{
				return;
			}
			LogService.LogException("Asset search failed.", ex);
			AssetSearchResultsControl.ShowMessage("Search failed", "See the Template Editor log.");
			DialogService.ShowToast("Search failed; see the Template Editor log.", "Find Asset", FeedbackSeverity.Warning);
		}
		finally
		{
			if (searchVersion == _searchVersion)
			{
				_searching = false;
			}
		}
	}
}

internal sealed class AssetSearchResultsControl : CustomControl
{
	private static AssetSearchResultsControl _current;
	private static IReadOnlyList<AssetSearchResultCard> _cards = Array.Empty<AssetSearchResultCard>();
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

	internal static void Clear()
	{
		_cards = Array.Empty<AssetSearchResultCard>();
		_current?.Refresh();
	}

	internal static void ShowMessage(string title, string detail)
	{
		_cards = new[] { new AssetSearchResultCard(title, detail) };
		_current?.Refresh();
	}

	internal static void ShowResults(AssetSearchResult result)
	{
		_cards = result.MatchCount == 0
			? new[] { new AssetSearchResultCard("No matches", "Try another ID or field.") }
			: result.Matches.Select(match => new AssetSearchResultCard(match)).ToArray();
		_current?.Refresh();
	}

	private void Refresh()
	{
		_view?.Show(_cards);
		Caption = _cards.Count == 0 || _cards[0].Match == null ? "Results" : $"Results ({_cards.Count})";
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
			row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
			TextBlock title = new TextBlock
			{
				Text = card.Title,
				FontSize = 11,
				TextTrimming = TextTrimming.CharacterEllipsis,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(7, 0, 3, 0)
			};
			title.SetResourceReference(TextBlock.ForegroundProperty, "Esri_TextControlBrush");
			row.Children.Add(title);
			if (card.Match != null)
			{
				StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 0, 2, 0) };
				actions.Children.Add(ActionButton(card, "SelectionMethodAdd16", "Add to selection", AssetSearchService.ResultAction.AddToSelection));
				if (card.Match.IsSpatial)
				{
					actions.Children.Add(ActionButton(card, "SelectionZoomToSelected16", "Zoom to", AssetSearchService.ResultAction.ZoomTo));
					actions.Children.Add(ActionButton(card, "SelectionPanToSelected16", "Pan to", AssetSearchService.ResultAction.PanTo));
				}
				Grid.SetColumn(actions, 1);
				row.Children.Add(actions);
			}
			Border tile = new Border
			{
				BorderThickness = new Thickness(0, 0, 0, 1),
				CornerRadius = new CornerRadius(0),
				ToolTip = string.IsNullOrEmpty(card.Detail) ? card.Title : card.Title + " · " + card.Detail,
				Child = row
			};
			tile.SetResourceReference(Border.BackgroundProperty, "Esri_ControlBackgroundBrush");
			tile.SetResourceReference(Border.BorderBrushProperty, "Esri_BorderBrush");
			_cards.Children.Add(tile);
		}
	}

	private static System.Windows.Controls.Button ActionButton(
		AssetSearchResultCard card, string icon, string tooltip, AssetSearchService.ResultAction action)
	{
		Image image = new Image
		{
			Width = 16,
			Height = 16
		};
		image.SetResourceReference(Image.SourceProperty, icon);
		System.Windows.Controls.Button button = new System.Windows.Controls.Button
		{
			Width = 20,
			Height = 20,
			Padding = new Thickness(0),
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0),
			Content = image,
			ToolTip = tooltip,
			Cursor = Cursors.Hand
		};
		ControlTemplate template = new ControlTemplate(typeof(System.Windows.Controls.Button));
		FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
		content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		template.VisualTree = content;
		button.Template = template;
		button.Click += (_, e) =>
		{
			e.Handled = true;
			card.Run(action);
		};
		return button;
	}
}

public sealed class AssetSearchResultCard
{
	internal AssetSearchMatch Match { get; }
	public string Title { get; }
	public string Detail { get; }

	internal AssetSearchResultCard(string title, string detail)
	{
		Title = title;
		Detail = detail;
	}

	internal AssetSearchResultCard(AssetSearchMatch match)
	{
		Match = match;
		Title = match.Source + " - " + match.Value;
		Detail = $"{(match.IsSpatial ? "Feature" : "Table row")} · Object ID {match.ObjectID}";
	}

	internal void Run(AssetSearchService.ResultAction action)
	{
		if (Match != null)
		{
			TaskObservationService.Forget(RunAsync(action), "Asset search result action failed.");
		}
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
