using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace TemplateEditor;

internal sealed class SearchHighlightTextBlock : TextBlock
{
	public static readonly DependencyProperty HighlightTextProperty = DependencyProperty.Register(
		nameof(HighlightText),
		typeof(string),
		typeof(SearchHighlightTextBlock),
		new PropertyMetadata(string.Empty, OnDisplayTextChanged));

	public static readonly DependencyProperty SearchTextProperty = DependencyProperty.Register(
		nameof(SearchText),
		typeof(string),
		typeof(SearchHighlightTextBlock),
		new PropertyMetadata(string.Empty, OnDisplayTextChanged));

	public string HighlightText
	{
		get => (string)GetValue(HighlightTextProperty);
		set => SetValue(HighlightTextProperty, value);
	}

	public string SearchText
	{
		get => (string)GetValue(SearchTextProperty);
		set => SetValue(SearchTextProperty, value);
	}

	private static void OnDisplayTextChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		((SearchHighlightTextBlock)dependencyObject).UpdateInlines();
	}

	private void UpdateInlines()
	{
		Inlines.Clear();
		string text = HighlightText ?? string.Empty;
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		List<TextRange> ranges = GetMatchedRanges(text, SearchText);
		if (ranges.Count == 0)
		{
			Inlines.Add(new Run(text));
			return;
		}
		int position = 0;
		foreach (TextRange range in ranges)
		{
			if (range.Start > position)
			{
				Inlines.Add(new Run(text.Substring(position, range.Start - position)));
			}
			Inlines.Add(new Run(text.Substring(range.Start, range.Length))
			{
				Background = new SolidColorBrush(Color.FromArgb(77, 0, 120, 240)),
				Foreground = Foreground
			});
			position = range.Start + range.Length;
		}
		if (position < text.Length)
		{
			Inlines.Add(new Run(text.Substring(position)));
		}
	}

	private static List<TextRange> GetMatchedRanges(string text, string searchText)
	{
		if (string.IsNullOrWhiteSpace(searchText) || searchText.Trim().Length < 2)
		{
			return new List<TextRange>();
		}
		List<TextRange> rawRanges = new List<TextRange>();
		foreach (string term in searchText.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase))
		{
			int startIndex = 0;
			while (startIndex < text.Length)
			{
				int index = text.IndexOf(term, startIndex, StringComparison.OrdinalIgnoreCase);
				if (index < 0)
				{
					break;
				}
				rawRanges.Add(new TextRange(index, term.Length));
				startIndex = index + term.Length;
			}
		}
		return MergeRanges(rawRanges.OrderBy((TextRange range) => range.Start).ThenBy((TextRange range) => range.Length).ToList());
	}

	private static List<TextRange> MergeRanges(List<TextRange> ranges)
	{
		List<TextRange> merged = new List<TextRange>();
		foreach (TextRange range in ranges)
		{
			if (merged.Count == 0 || range.Start > merged[merged.Count - 1].End)
			{
				merged.Add(range);
				continue;
			}
			TextRange previous = merged[merged.Count - 1];
			merged[merged.Count - 1] = new TextRange(previous.Start, Math.Max(previous.End, range.End) - previous.Start);
		}
		return merged;
	}

	private readonly struct TextRange
	{
		public TextRange(int start, int length)
		{
			Start = start;
			Length = length;
		}

		public int Start { get; }

		public int Length { get; }

		public int End => Start + Length;
	}
}
