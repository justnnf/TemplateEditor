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
	private readonly struct TextRange(int start, int length)
	{
		public int Start { get; } = start;

		public int Length { get; } = length;

		public int End => Start + Length;
	}

	public static readonly DependencyProperty HighlightTextProperty;

	public static readonly DependencyProperty SearchTextProperty;

	public string HighlightText
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(HighlightTextProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(HighlightTextProperty, (object)value);
		}
	}

	public string SearchText
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(SearchTextProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(SearchTextProperty, (object)value);
		}
	}

	private static void OnDisplayTextChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		((SearchHighlightTextBlock)(object)dependencyObject).UpdateInlines();
	}

	private void UpdateInlines()
	{
		base.Inlines.Clear();
		string text = HighlightText ?? string.Empty;
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		List<TextRange> matchedRanges = GetMatchedRanges(text, SearchText);
		if (matchedRanges.Count == 0)
		{
			base.Inlines.Add(new Run(text));
			return;
		}
		int num = 0;
		foreach (TextRange item in matchedRanges)
		{
			if (item.Start > num)
			{
				base.Inlines.Add(new Run(text.Substring(num, item.Start - num)));
			}
			base.Inlines.Add(new Run(text.Substring(item.Start, item.Length))
			{
				Background = new SolidColorBrush(Color.FromArgb(77, 0, 120, 240)),
				Foreground = base.Foreground
			});
			num = item.Start + item.Length;
		}
		if (num < text.Length)
		{
			base.Inlines.Add(new Run(text.Substring(num)));
		}
	}

	private static List<TextRange> GetMatchedRanges(string text, string searchText)
	{
		if (string.IsNullOrWhiteSpace(searchText) || searchText.Trim().Length < 2)
		{
			return new List<TextRange>();
		}
		List<TextRange> list = new List<TextRange>();
		foreach (string item in searchText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Distinct<string>(StringComparer.OrdinalIgnoreCase))
		{
			int num = 0;
			while (num < text.Length)
			{
				int num2 = text.IndexOf(item, num, StringComparison.OrdinalIgnoreCase);
				if (num2 < 0)
				{
					break;
				}
				list.Add(new TextRange(num2, item.Length));
				num = num2 + item.Length;
			}
		}
		return MergeRanges((from range in list
			orderby range.Start, range.Length
			select range).ToList());
	}

	private static List<TextRange> MergeRanges(List<TextRange> ranges)
	{
		List<TextRange> list = new List<TextRange>();
		foreach (TextRange range in ranges)
		{
			if (list.Count == 0 || range.Start > list[list.Count - 1].End)
			{
				list.Add(range);
				continue;
			}
			TextRange textRange = list[list.Count - 1];
			list[list.Count - 1] = new TextRange(textRange.Start, Math.Max(textRange.End, range.End) - textRange.Start);
		}
		return list;
	}

	static SearchHighlightTextBlock()
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Expected O, but got Unknown
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Expected O, but got Unknown
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Expected O, but got Unknown
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Expected O, but got Unknown
		HighlightTextProperty = DependencyProperty.Register("HighlightText", typeof(string), typeof(SearchHighlightTextBlock), new PropertyMetadata((object)string.Empty, new PropertyChangedCallback(OnDisplayTextChanged)));
		SearchTextProperty = DependencyProperty.Register("SearchText", typeof(string), typeof(SearchHighlightTextBlock), new PropertyMetadata((object)string.Empty, new PropertyChangedCallback(OnDisplayTextChanged)));
	}
}
