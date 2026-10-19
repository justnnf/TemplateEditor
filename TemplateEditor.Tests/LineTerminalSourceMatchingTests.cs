using TemplateEditor;
using Xunit;

namespace TemplateEditor.Tests;

public sealed class LineTerminalSourceMatchingTests
{
    [Fact]
    public void TerminalFilter_SkipsSingleTerminalAndAppliesAllowlist()
    {
        var terminals = new List<(int Id, string Name)>
        {
            (1, "Single Terminal"), (2, "Source"), (3, "Load")
        };
        var settings = new TemplateEditorSettings();
        settings.Normalize();

        Assert.Equal(new[] { 2, 3 }, LineTerminalPromptService.FilterTerminals(terminals, settings).Select(t => t.Id));

        settings.AllowedLineTerminalNames = TemplateEditorSettings.ParseGroupNames(" load ");
        Assert.Equal(new[] { 3 }, LineTerminalPromptService.FilterTerminals(terminals, settings).Select(t => t.Id));

        settings.AllowedLineTerminalNames = TemplateEditorSettings.ParseGroupNames("other");
        Assert.Empty(LineTerminalPromptService.FilterTerminals(terminals, settings));
    }

    [Theory]
    [InlineData("GIS.ElectricDistributionDevice", "FeatureServer/12", "Electric Distribution Device", true)]
    [InlineData("ElectricLine", "ElectricLine", "Other Layer", true)]
    [InlineData("ElectricDevice", "FeatureServer/12", "Structure Device", false)]
    public void MatchesNetworkSourceAgainstServiceLayerAliases(string source, string className, string layerName, bool expected)
    {
        Assert.Equal(expected, LineTerminalPromptService.SameSource(source, className, layerName));
    }
}
