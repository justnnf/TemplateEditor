using TemplateEditor;
using System.Text.Json;
using Xunit;

namespace TemplateEditor.Tests;

public sealed class TemplateValidationTests
{
    [Fact]
    public void ValidateTemplateStructure_AllowsRepeatedPartNames()
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

        Assert.Empty(errors);
    }

    [Fact]
    public void RepeatedPartNamesResolveToTheirOwnDefinitions()
    {
        var first = new SimpleTemplate { Name = "Switch", DefaultFieldValues = new() { ["ITEM_NUMBER"] = "A" } };
        var second = new SimpleTemplate { Name = "switch", DefaultFieldValues = new() { ["ITEM_NUMBER"] = "B" } };
        var firstPart = new SimpleTemplateReference { Name = "Switch", FeatureId = 1 };
        var secondPart = new SimpleTemplateReference { Name = "switch", FeatureId = 2 };

        TemplateCache.Initialize(new TemplateConfig
        {
            SimpleTemplates = new() { first, second },
            GroupTemplates = new() { new GroupTemplate { Name = "Group", SimpleTemplates = new() { firstPart, secondPart } } }
        });

        Assert.Same(first, TemplateCache.GetSimpleTemplate(firstPart));
        Assert.Same(second, TemplateCache.GetSimpleTemplate(secondPart));
    }

    [Fact]
    public void OneDefinitionCanBeReusedByMultipleParts()
    {
        var definition = new SimpleTemplate { Name = "Connector" };
        var firstPart = new SimpleTemplateReference { Name = "Connector", FeatureId = 1 };
        var secondPart = new SimpleTemplateReference { Name = "Connector", FeatureId = 2 };

        TemplateCache.Initialize(new TemplateConfig
        {
            SimpleTemplates = new() { definition },
            GroupTemplates = new() { new GroupTemplate { Name = "Group", SimpleTemplates = new() { firstPart, secondPart } } }
        });

        Assert.Same(definition, TemplateCache.GetSimpleTemplate(firstPart));
        Assert.Same(definition, TemplateCache.GetSimpleTemplate(secondPart));
    }

    [Fact]
    public void RepeatedNamesAcrossGroupsResolveToEachGroupsDefinition()
    {
        var first = new SimpleTemplate { Name = "Switch", DefaultFieldValues = new() { ["ITEM_NUMBER"] = "A" } };
        var second = new SimpleTemplate { Name = "Switch", DefaultFieldValues = new() { ["ITEM_NUMBER"] = "B" } };
        var firstGroupPart = new SimpleTemplateReference { Name = "Switch", FeatureId = 1 };
        var secondGroupPart = new SimpleTemplateReference { Name = "Switch", FeatureId = 1 };

        TemplateCache.Initialize(new TemplateConfig
        {
            SimpleTemplates = new() { first, second },
            GroupTemplates = new()
            {
                new GroupTemplate { Name = "First", SimpleTemplates = new() { firstGroupPart } },
                new GroupTemplate { Name = "Second", SimpleTemplates = new() { secondGroupPart } }
            }
        });

        Assert.Same(first, TemplateCache.GetSimpleTemplate(firstGroupPart));
        Assert.Same(second, TemplateCache.GetSimpleTemplate(secondGroupPart));
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
