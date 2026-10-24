using System;
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework.Contracts;

namespace TemplateEditor;

internal sealed class AssetSearchFieldComboBox : ComboBox
{
	internal static string FieldName { get; private set; } = "facilityid";

	public AssetSearchFieldComboBox()
	{
		Add(new ComboBoxItem("Facility ID"));
		Add(new ComboBoxItem("Switch Number"));
		Add(new ComboBoxItem("Site ID"));
		Add(new ComboBoxItem("Global ID"));
		SelectedItem = ItemCollection[0];
	}

	protected override void OnSelectionChange(ComboBoxItem item)
	{
		FieldName = item?.Text switch
		{
			"Switch Number" => "assetid",
			"Site ID" => "SITE_ID",
			"Global ID" => "GLOBALID",
			_ => "facilityid"
		};
	}
}

internal sealed class AssetSearchEditBox : EditBox
{
	internal static AssetSearchEditBox Current { get; private set; }
	private bool _searching;
	private string _lastSearchKey;
	private DateTime _lastSearchStarted;

	public AssetSearchEditBox()
	{
		Current = this;
	}

	protected override void OnEnter()
	{
		TaskObservationService.Forget(SearchAsync(), "Asset search failed.");
	}

	internal async Task SearchAsync()
	{
		string value = Text?.Trim();
		string fieldName = AssetSearchFieldComboBox.FieldName;
		string searchKey = fieldName + "\n" + value;
		if (_searching || string.IsNullOrEmpty(value) ||
			(searchKey == _lastSearchKey && DateTime.UtcNow - _lastSearchStarted < TimeSpan.FromMilliseconds(500)))
		{
			return;
		}

		_searching = true;
		_lastSearchKey = searchKey;
		_lastSearchStarted = DateTime.UtcNow;
		AssetSearchResultsComboBox.ShowSearching();
		try
		{
			AssetSearchResult result = await AssetSearchService.SearchAndSelectAsync(fieldName, value);
			AssetSearchResultsComboBox.ShowResults(result);
			string message = result.SelectedCount == 0
				? "No matches found in the current map."
				: $"Added {result.SelectedCount} match(es) to the current selection.";
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
			AssetSearchResultsComboBox.ShowMessage("Search failed; see the Template Editor log.");
			DialogService.ShowToast("Search failed; see the Template Editor log.", "Find Asset", FeedbackSeverity.Warning);
		}
		finally
		{
			_searching = false;
		}
	}
}

internal sealed class AssetSearchRunButton : Button
{
	protected override void OnClick()
	{
		if (AssetSearchEditBox.Current is { } search)
		{
			TaskObservationService.Forget(search.SearchAsync(), "Asset search failed.");
		}
	}
}

internal sealed class AssetSearchResultsComboBox : ComboBox
{
	private static AssetSearchResultsComboBox _current;
	private static string[] _items = { "No search yet" };

	public AssetSearchResultsComboBox()
	{
		_current = this;
		Refresh();
	}

	internal static void ShowSearching() => ShowMessage("Searching live layers and tables...");

	internal static void ShowMessage(string message)
	{
		_items = new[] { message };
		_current?.Refresh();
	}

	internal static void ShowResults(AssetSearchResult result)
	{
		_items = new string[result.Matches.Count + 1];
		_items[0] = result.SelectedCount == 0 ? "No matches" : $"{result.SelectedCount} match(es) added to selection";
		for (int i = 0; i < result.Matches.Count; i++)
		{
			AssetSearchMatch match = result.Matches[i];
			_items[i + 1] = $"{match.Source} · {match.Kind} · OID {match.ObjectID}";
		}
		_current?.Refresh();
	}

	private void Refresh()
	{
		Clear();
		foreach (string item in _items)
		{
			Add(new ComboBoxItem(item));
		}
		SelectedItem = ItemCollection[0];
	}
}
