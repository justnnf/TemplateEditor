using ArcGIS.Core.Data;
using TemplateEditor;
using Xunit;

namespace TemplateEditor.Tests;

public sealed class AssetSearchServiceTests
{
    [Fact]
    public void BuildsSafeExactQueriesForIdentifiers()
    {
        Assert.Equal("facilityid = 'O''Brien'", AssetSearchService.BuildWhereClause("facilityid", FieldType.String, " O'Brien "));
        Assert.Equal("assetid = 123", AssetSearchService.BuildWhereClause("assetid", FieldType.Integer, "123"));
        Assert.Equal("GLOBALID = '{11111111-2222-3333-4444-555555555555}'", AssetSearchService.BuildWhereClause("GLOBALID", FieldType.GlobalID, "11111111-2222-3333-4444-555555555555"));
        Assert.Null(AssetSearchService.BuildWhereClause("GLOBALID", FieldType.GlobalID, "not-a-guid"));
    }
}
