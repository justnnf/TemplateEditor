using TemplateEditor;
using Xunit;

namespace TemplateEditor.Tests;

public sealed class LineTerminalSourceMatchingTests
{
    [Theory]
    [InlineData("GIS.ElectricDistributionDevice", "FeatureServer/12", "Electric Distribution Device", true)]
    [InlineData("ElectricLine", "ElectricLine", "Other Layer", true)]
    [InlineData("ElectricDevice", "FeatureServer/12", "Structure Device", false)]
    public void MatchesNetworkSourceAgainstServiceLayerAliases(string source, string className, string layerName, bool expected)
    {
        Assert.Equal(expected, LineTerminalPromptService.SameSource(source, className, layerName));
    }
}
