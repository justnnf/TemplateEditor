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
using ComboBoxItem = ArcGIS.Desktop.Framework.Contracts.ComboBoxItem;

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
			Background = DialogAppearance.InputBackground,
			Foreground = DialogAppearance.Foreground,
			BorderBrush = DialogAppearance.Border,
			BorderThickness = new Thickness(1),
			Padding = new Thickness(8, 2, 6, 2),
			Margin = new Thickness(0, 2, 0, 5),
			ToolTip = "Search by field"
		};
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
			Background = DialogAppearance.InputBackground,
			BorderBrush = DialogAppearance.Border,
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(4),
			Child = searchBar
		};
		_value = new TextBox
		{
			Background = Brushes.Transparent,
			Foreground = DialogAppearance.Foreground,
			BorderThickness = new Thickness(0),
			Padding = new Thickness(9, 4, 2, 3),
			ToolTip = "Enter an exact ID and press Enter"
		};
		_value.KeyDown += (_, e) =>
		{
			if (e.Key == Key.Return)
			{
				e.Handled = true;
				StartSearch();
			}
		};
		searchBar.Children.Add(_value);
		TextBlock hint = new TextBlock
		{
			Text = "Enter exact ID",
			Foreground = DialogAppearance.SecondaryForeground,
			Margin = new Thickness(11, 5, 0, 0),
			IsHitTestVisible = false
		};
		_value.TextChanged += (_, _) => hint.Visibility = _value.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
		searchBar.Children.Add(hint);
		System.Windows.Controls.Button find = new System.Windows.Controls.Button
		{
			Width = 30,
			Height = 27,
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0),
			Content = SearchGlyph(),
			ToolTip = "Search live layers and tables"
		};
		find.Click += (_, _) => StartSearch();
		Grid.SetColumn(find, 1);
		searchBar.Children.Add(find);
		layout.Children.Add(searchBorder);
		Content = layout;
	}

	private void AddField(string label, string name) => _field.Items.Add(new System.Windows.Controls.ComboBoxItem
	{
		Content = label,
		Tag = name,
		Foreground = DialogAppearance.Foreground,
		Background = DialogAppearance.InputBackground
	});

	private static Canvas SearchGlyph()
	{
		Canvas glyph = new Canvas { Width = 17, Height = 17 };
		glyph.Children.Add(new Ellipse
		{
			Width = 10,
			Height = 10,
			Stroke = DialogAppearance.Accent,
			StrokeThickness = 2
		});
		glyph.Children.Add(new Line
		{
			X1 = 9,
			Y1 = 9,
			X2 = 16,
			Y2 = 16,
			Stroke = DialogAppearance.Accent,
			StrokeThickness = 2,
			StrokeStartLineCap = PenLineCap.Round,
			StrokeEndLineCap = PenLineCap.Round
		});
		return glyph;
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
				: $"Found {result.MatchCount} match(es). Open Results to review them.";
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
		Grid layout = new Grid { Width = 360, Background = DialogAppearance.Background };
		layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
		StackPanel header = new StackPanel { Margin = new Thickness(12, 10, 12, 7) };
		header.Children.Add(new TextBlock
		{
			Text = "Search results",
			FontSize = 14,
			FontWeight = FontWeights.SemiBold,
			Foreground = DialogAppearance.Foreground
		});
		header.Children.Add(new TextBlock
		{
			Text = "Click to select · Right-click for more actions",
			FontSize = 11,
			Foreground = DialogAppearance.SecondaryForeground
		});
		layout.Children.Add(header);
		ScrollViewer scroll = new ScrollViewer
		{
			Content = _cards,
			MaxHeight = 420,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		};
		Grid.SetRow(scroll, 1);
		layout.Children.Add(scroll);
		Content = new Border
		{
			Background = DialogAppearance.Background,
			BorderBrush = DialogAppearance.Border,
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(5),
			Child = layout
		};
	}

	internal void Show(IEnumerable<AssetSearchResultCard> cards)
	{
		_cards.Children.Clear();
		foreach (AssetSearchResultCard card in cards)
		{
			StackPanel text = new StackPanel();
			text.Children.Add(new TextBlock
			{
				Text = card.Title,
				FontSize = 13,
				FontWeight = FontWeights.SemiBold,
				Foreground = DialogAppearance.Foreground,
				TextTrimming = TextTrimming.CharacterEllipsis
			});
			text.Children.Add(new TextBlock { Text = card.Detail, FontSize = 11, Foreground = DialogAppearance.SecondaryForeground });
			if (!string.IsNullOrEmpty(card.Identifier))
			{
				text.Children.Add(new TextBlock { Text = card.Identifier, FontSize = 11, Foreground = DialogAppearance.SecondaryForeground });
			}
			Border tile = new Border
			{
				Background = DialogAppearance.InputBackground,
				BorderBrush = DialogAppearance.Border,
				BorderThickness = new Thickness(1),
				CornerRadius = new CornerRadius(5),
				Padding = new Thickness(10, 8, 10, 8),
				Margin = new Thickness(8, 3, 8, 3),
				ContextMenu = card.Menu,
				Child = text
			};
			if (card.Match != null)
			{
				tile.Cursor = Cursors.Hand;
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
		Detail = $"{match.Kind}  ·  Object ID {match.ObjectID}";
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
