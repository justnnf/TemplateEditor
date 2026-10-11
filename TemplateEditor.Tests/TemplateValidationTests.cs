using TemplateEditor;
using System.Text.Json;
using Xunit;

namespace TemplateEditor.Tests;

public sealed class TemplateValidationTests
{
    [Fact]
    public void ValidateTemplateStructure_RejectsCaseInsensitiveDuplicateSimpleNames()
    {
        var errors = new List<string>();

        CommonFunctions.ValidateTemplateStructure(
            new List<SimpleTemplate>
            {
                new() { Name = "Transformer" },
                new() { Name = "transformer" }
            },
            new List<GroupTemplate>(),
            errors);

        Assert.Contains(errors, error => error.Contains("duplicate simple template name 'Transformer'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateTemplateStructure_ReportsMalformedGroupReferencesWithoutThrowing()
    {
        var errors = new List<string>();

        CommonFunctions.ValidateTemplateStructure(
            new List<SimpleTemplate>(),
            new List<GroupTemplate>
            {
                new()
                {
                    Name = "Equipment group",
                    SimpleTemplates = new List<SimpleTemplateReference> { null!, new() { Name = " " } }
                }
            },
            errors);

        Assert.Contains(errors, error => error.Contains("simple template reference without a Name", StringComparison.Ordinal));
    }

    [Fact]
    public void SettingsNormalization_ClampsAndCanonicalizesValues()
    {
        var settings = new TemplateEditorSettings
        {
            SplitSearchDistance = -1,
            MaxRecentTemplates = 99,
            HintSourceColorHex = "00ff50",
            SplitPromptMode = "invalid",
            SplitPointPlacementGroups = new List<string> { " electricdevice ", "ELECTRICDEVICE", "" }
        };

        settings.Normalize();

        Assert.Equal(0, settings.SplitSearchDistance);
        Assert.Equal(50, settings.MaxRecentTemplates);
        Assert.Equal("#00FF50", settings.HintSourceColorHex);
        Assert.Equal("AlwaysAsk", settings.SplitPromptMode);
        Assert.Equal(new[] { "ELECTRICDEVICE" }, settings.SplitPointPlacementGroups);
    }

    [Fact]
    public void SettingsNormalization_ValidatesTemplatePreviewColors()
    {
        var settings = new TemplateEditorSettings
        {
            PreviewFillColorHex = "de7bcf",
            PreviewOutlineColorHex = "not-a-color"
        };

        settings.Normalize();

        Assert.Equal("#DE7BCF", settings.PreviewFillColorHex);
        Assert.Equal("#0085CA", settings.PreviewOutlineColorHex);
    }

    [Fact]
    public void SplitCandidateInclusions_DefaultToElectricPrimaryAssetGroups()
    {
        var settings = JsonSerializer.Deserialize<TemplateEditorSettings>("{\"SplitExcludedAssetGroups\":[\"ElectricLine: Overhead Primary\"]}")!;

        Assert.True(settings.IsSplitCandidateIncluded("ElectricLine", "Overhead Primary"));
        Assert.True(settings.IsSplitCandidateIncluded("ELECTRICLINE", "underground primary"));
        Assert.False(settings.IsSplitCandidateIncluded("StructureLine", "Overhead Primary"));
        Assert.False(settings.IsSplitCandidateIncluded("ElectricLine", "Overhead Secondary"));

        settings.SplitIncludedAssetGroups = TemplateEditorSettings.ParseGroupNames(" ElectricLine: Overhead Primary, electricline: overhead primary, Underground Secondary ");
        Assert.Equal(2, settings.SplitIncludedAssetGroups.Count);
        Assert.True(settings.IsSplitCandidateIncluded("ElectricLine", "Overhead Primary"));
        Assert.True(settings.IsSplitCandidateIncluded("ElectricLine", "Underground Secondary"));
        Assert.False(settings.IsSplitCandidateIncluded("ElectricLine", "Underground Primary"));

        settings.SplitIncludedAssetGroups.Clear();
        Assert.True(settings.IsSplitCandidateIncluded("ElectricLine", "Overhead Secondary"));
    }
}
