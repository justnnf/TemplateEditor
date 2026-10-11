using ArcGIS.Core.Data.UtilityNetwork;
using TemplateEditor;
using Xunit;

namespace TemplateEditor.Tests;

public sealed class AssociationRuleCatalogTests
{
    [Fact]
    public void RuleLookupFiltersByAssociationTypeAndKnownTable()
    {
        var catalog = new AssociationRuleCatalog(new List<AssociationRule>
        {
            new() { AssociationType = "Attachment", FromTable = "ElectricDevice", ToTable = "StructureJunction" },
            new() { AssociationType = "Containment", FromTable = "StructureBoundary", ToTable = "ElectricLine" }
        });
        var device = new FeatureLayerInfo { TableName = "ElectricDevice" };
        var structure = new FeatureLayerInfo { TableName = "StructureJunction" };

        Assert.True(catalog.Allows((AssociationType)3, device, structure));
        Assert.False(catalog.Allows((AssociationType)2, device, structure));
        Assert.Equal(new[] { "STRUCTUREJUNCTION" }, catalog.GetAllowedCounterpartTables((AssociationType)3, device, true));
    }
}
