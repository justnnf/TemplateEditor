using System.Collections.Generic;

namespace TemplateEditor;

internal sealed record AssetSearchMatch(string Source, string Kind, long ObjectID);

internal sealed record AssetSearchResult(IReadOnlyList<AssetSearchMatch> Matches, int FailedTables, bool LimitReached)
{
	internal int SelectedCount => Matches.Count;
}
