using TemplateEditor;
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
}
