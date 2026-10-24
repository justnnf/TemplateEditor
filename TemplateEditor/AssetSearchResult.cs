using System.Collections.Generic;
using ArcGIS.Desktop.Mapping;

namespace TemplateEditor;

internal sealed record AssetSearchMatch(Map Map, MapMember Member, long ObjectID, string Value)
{
	internal string Source => Member.Name;
	internal bool IsSpatial => Member is FeatureLayer;
}

internal sealed record AssetSearchResult(IReadOnlyList<AssetSearchMatch> Matches, int FailedTables, bool LimitReached)
{
	internal int MatchCount => Matches.Count;
}
