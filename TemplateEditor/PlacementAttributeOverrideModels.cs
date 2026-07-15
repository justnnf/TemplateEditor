using System.Collections.Generic;
namespace TemplateEditor;

internal sealed class PlacementAttributeOverrideDefinition
{
	public string FieldName { get; set; }

	public string Label { get; set; }

	public string Description { get; set; }

	public string DomainName { get; set; }
}

internal sealed class PlacementAttributeOverrideCatalog
{
	public List<PlacementAttributeOverrideDefinition> Fields { get; set; } = new List<PlacementAttributeOverrideDefinition>();
}

internal sealed class PlacementAttributeOverrideValue
{
	public string FieldName { get; set; }

	public bool Enabled { get; set; }

	public string Value { get; set; }
}

internal sealed class PlacementAttributeOverrideEditorState
{
	public PlacementAttributeOverrideDefinition Definition { get; set; }

	public bool IsEnabled { get; set; }

	public string Value { get; set; }

	public string ConfiguredValueSummary { get; set; }

	public List<string> AvailableValues { get; set; } = new List<string>();

	public bool UseDropDown => AvailableValues?.Count > 0;
}

internal sealed class PlacementAttributeEditorModel
{
	public string TemplateKey { get; set; }

	public string TemplateDisplayName { get; set; }

	public bool IsGroupTemplate { get; set; }

	public List<PlacementAttributeEditorPartState> Parts { get; set; } = new List<PlacementAttributeEditorPartState>();

	public List<PlacementAttributeOverrideFavouriteSummary> AvailableFavourites { get; set; } = new List<PlacementAttributeOverrideFavouriteSummary>();
}

internal sealed class PlacementAttributeEditorPartState
{
	public string PartKey { get; set; }

	public string DisplayName { get; set; }

	public string DetailText { get; set; }

	public int FeatureId { get; set; }

	public SimpleTemplate Template { get; set; }

	public List<PlacementAttributeEditorFieldState> AttributeFields { get; set; } = new List<PlacementAttributeEditorFieldState>();
}

internal sealed class PlacementAttributeEditorFieldState
{
	public string FieldName { get; set; }

	public string Label { get; set; }

	public string ConfiguredValue { get; set; }

	public string CurrentValue { get; set; }

	public bool HasDomainValues { get; set; }

	public bool IsEditable { get; set; } = true;

	public List<string> AvailableValues { get; set; } = new List<string>();

	public string ConfiguredValueSummary =>
		string.IsNullOrWhiteSpace(ConfiguredValue)
			? "Configured default: (blank)"
			: "Configured default: " + ConfiguredValue;
}

internal sealed class PlacementAttributeOverrideFavouriteCatalog
{
	public List<PlacementAttributeOverrideFavourite> Favourites { get; set; } = new List<PlacementAttributeOverrideFavourite>();
}

internal sealed class PlacementAttributeOverrideFavourite
{
	public string Id { get; set; }

	public string Name { get; set; }

	public string TemplateKey { get; set; }

	public string TemplateDisplayName { get; set; }

	public string CreatedUtc { get; set; }

	public string UpdatedUtc { get; set; }

	public Dictionary<string, Dictionary<string, string>> PartValues { get; set; } =
		new Dictionary<string, Dictionary<string, string>>();
}

internal sealed class PlacementAttributeOverrideFavouriteSummary
{
	public string Id { get; set; }

	public string Name { get; set; }

	public string TemplateKey { get; set; }

	public string TemplateDisplayName { get; set; }

	public override string ToString()
	{
		return Name ?? string.Empty;
	}
}
