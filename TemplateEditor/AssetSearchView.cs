using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TemplateEditor;

public sealed class AssetSearchView : UserControl
{
	private readonly ComboBox _field = new ComboBox();
	private readonly TextBox _value = new TextBox();
	private readonly Button _find = new Button();
	private readonly TextBlock _status = new TextBlock();
	private readonly ListBox _results = new ListBox();
	private bool _searching;

	public AssetSearchView()
	{
		Grid layout = new Grid { Margin = new Thickness(12), Background = DialogAppearance.Background };
		layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

		StackPanel controls = new StackPanel();
		controls.Children.Add(new TextBlock
		{
			Text = "Search the current map's layers and tables by an exact ID.",
			TextWrapping = TextWrapping.Wrap,
			Foreground = DialogAppearance.SecondaryForeground,
			Margin = new Thickness(0, 0, 0, 14)
		});
		controls.Children.Add(CreateLabel("Search by"));
		AddField("Facility ID", "facilityid");
		AddField("Switch Number", "assetid");
		AddField("Site ID", "SITE_ID");
		AddField("Global ID", "GLOBALID");
		_field.SelectedIndex = 0;
		_field.Margin = new Thickness(0, 4, 0, 12);
		_field.Background = DialogAppearance.InputBackground;
		_field.Foreground = DialogAppearance.Foreground;
		_field.BorderBrush = DialogAppearance.Border;
		controls.Children.Add(_field);
		controls.Children.Add(CreateLabel("Exact value"));
		_value.Margin = new Thickness(0, 4, 0, 12);
		_value.Padding = new Thickness(7, 5, 7, 5);
		_value.Background = DialogAppearance.InputBackground;
		_value.Foreground = DialogAppearance.Foreground;
		_value.BorderBrush = DialogAppearance.Border;
		_value.KeyDown += OnValueKeyDown;
		controls.Children.Add(_value);
		_find.Content = "Find and add to selection";
		_find.Padding = new Thickness(10, 6, 10, 6);
		_find.HorizontalAlignment = HorizontalAlignment.Left;
		_find.Background = DialogAppearance.Accent;
		_find.Foreground = System.Windows.Media.Brushes.White;
		_find.Click += async (_, _) => await SearchAsync();
		controls.Children.Add(_find);
		_status.Text = "Matching features and table rows will appear below.";
		_status.TextWrapping = TextWrapping.Wrap;
		_status.Foreground = DialogAppearance.SecondaryForeground;
		_status.Margin = new Thickness(0, 16, 0, 12);
		controls.Children.Add(_status);
		Grid.SetRow(controls, 0);
		layout.Children.Add(controls);

		Grid resultsHost = new Grid();
		resultsHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		resultsHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
		TextBlock resultsLabel = CreateLabel("Results");
		resultsLabel.Margin = new Thickness(0, 0, 0, 6);
		resultsHost.Children.Add(resultsLabel);
		_results.Background = DialogAppearance.InputBackground;
		_results.Foreground = DialogAppearance.Foreground;
		_results.BorderBrush = DialogAppearance.Border;
		Grid.SetRow(_results, 1);
		resultsHost.Children.Add(_results);
		Grid.SetRow(resultsHost, 1);
		layout.Children.Add(resultsHost);

		Background = DialogAppearance.Background;
		Content = layout;
		Loaded += (_, _) => _value.Focus();
	}

	private void AddField(string label, string fieldName)
	{
		_field.Items.Add(new ComboBoxItem { Content = label, Tag = fieldName });
	}

	private static TextBlock CreateLabel(string text)
	{
		return new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, Foreground = DialogAppearance.Foreground };
	}

	private async void OnValueKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Return)
		{
			e.Handled = true;
			await SearchAsync();
		}
	}

	private async System.Threading.Tasks.Task SearchAsync()
	{
		string value = _value.Text.Trim();
		if (_searching || value.Length == 0)
		{
			return;
		}
		_searching = true;
		_find.IsEnabled = false;
		_status.Text = "Searching live layers and tables...";
		_results.ItemsSource = null;
		try
		{
			string fieldName = (string)((ComboBoxItem)_field.SelectedItem).Tag;
			AssetSearchResult result = await AssetSearchService.SearchAndSelectAsync(fieldName, value);
			_results.ItemsSource = result.Matches.Select(match => $"{match.Source}  ·  {match.Kind}  ·  OID {match.ObjectID}").ToList();
			_status.Text = result.SelectedCount == 0
				? "No matches found in the current map."
				: $"Added {result.SelectedCount} match(es) to the current selection.";
			if (result.LimitReached)
			{
				_status.Text += " Limited to 100 matches; use a more specific value.";
			}
			if (result.FailedTables > 0)
			{
				_status.Text += $" {result.FailedTables} layer(s) or table(s) failed; see the Template Editor log.";
			}
		}
		catch (Exception ex)
		{
			LogService.LogException("Asset search failed.", ex);
			_status.Text = "Search failed. See the Template Editor log.";
		}
		finally
		{
			_searching = false;
			_find.IsEnabled = true;
		}
	}
}
