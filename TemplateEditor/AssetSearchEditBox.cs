using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace TemplateEditor;

internal sealed class AssetSearchEditBox : EditBox
{
	private bool _searching;

	protected override async void OnEnter()
	{
		string value = Text?.Trim();
		if (_searching || string.IsNullOrEmpty(value))
		{
			return;
		}
		_searching = true;
		try
		{
			AssetSearchResult result = await AssetSearchService.SearchAndSelectAsync(AssetSearchFieldComboBox.FieldName, value);
			string message = result.SelectedCount == 0
				? "No matching rows were found in the current map."
				: $"Added {result.SelectedCount} matching row(s) to the current selection.";
			if (result.LimitReached)
			{
				message += " The search is limited to 100 matches; use a more specific value.";
			}
			if (result.FailedTables > 0)
			{
				message += $" {result.FailedTables} layer(s) or table(s) could not be searched; see the Template Editor log.";
			}
			DialogService.ShowToast(message, "Find asset", result.FailedTables > 0 ? FeedbackSeverity.Warning : result.SelectedCount > 0 ? FeedbackSeverity.Success : FeedbackSeverity.Info);
		}
		catch (Exception ex)
		{
			LogService.LogException("Asset search failed.", ex);
			DialogService.ShowToast("Asset search failed. See the Template Editor log.", "Find asset", FeedbackSeverity.Error);
		}
		finally
		{
			_searching = false;
		}
	}
}
