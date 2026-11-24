using ArcGIS.Core.Data;
using TemplateEditor;
using Xunit;

namespace TemplateEditor.Tests;

public sealed class AssetSearchServiceTests
{
    [Fact]
    public void BuildsSafeExactQueriesForIdentifiers()
    {
        Assert.Equal("UPPER(facilityid) = 'O''BRIEN'", AssetSearchService.BuildWhereClause("facilityid", FieldType.String, " O'Brien "));
        Assert.Equal("assetid = 123", AssetSearchService.BuildWhereClause("assetid", FieldType.Integer, "123"));
        Assert.Equal("GLOBALID = '{11111111-2222-3333-4444-555555555555}'", AssetSearchService.BuildWhereClause("GLOBALID", FieldType.GlobalID, "11111111-2222-3333-4444-555555555555"));
        Assert.Null(AssetSearchService.BuildWhereClause("GLOBALID", FieldType.GlobalID, "not-a-guid"));
    }

    [Fact]
    public void MatchModeIsConfigurableForTextFields()
    {
        Assert.Equal("UPPER(assetid) = 'ABC'", AssetSearchService.BuildWhereClause("assetid", FieldType.String, "aBc"));
        Assert.Equal("UPPER(tag) LIKE '%ABC%'", AssetSearchService.BuildWhereClause("tag", FieldType.String, "aBc", contains: true));
        Assert.Equal("tag = 25", AssetSearchService.BuildWhereClause("tag", FieldType.Integer, "25", contains: true));
    }

    [Theory]
    [InlineData("27B21776", "UPPER(assetid) LIKE '%27B21776%'")]
    [InlineData(" test_switch ", "UPPER(assetid) LIKE '%TEST$_SWITCH%' ESCAPE '$'")]
    [InlineData("A%$'B", "UPPER(assetid) LIKE '%A$%$$''B%' ESCAPE '$'")]
    public void SwitchNumberMatchesLiteralTextAnywhereInTheId(string input, string expected)
    {
        Assert.Equal(expected, AssetSearchService.BuildWhereClause("assetid", FieldType.String, input, contains: true));
    }
}
