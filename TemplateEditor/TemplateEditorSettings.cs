using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace TemplateEditor;

internal sealed class TemplateEditorSettings
{
	public string TemplateConfigFilePath { get; set; }

	public bool ValidateConfig { get; set; }

	public bool PreventDefaultVersionPlacement { get; set; }

	public bool EnableLineSplitPrompts { get; set; } = true;

	public bool EnablePointPlacementSplitPrompt { get; set; } = true;

	public bool EnableLineEndpointSplitPrompt { get; set; } = true;

	public bool EnableParallelCopyPrompt { get; set; } = true;

	public bool EnableSplitAtLineStartPoint { get; set; } = true;

	public bool EnableSplitAtLineEndPoint { get; set; } = true;

	public bool EnableConfiguredLinePartSplits { get; set; } = true;

	public bool SuppressDuplicateSplitPrompts { get; set; } = true;

	public bool SplitOnlyInteriorCandidates { get; set; } = true;

	public int MaxSplitCandidatesToReview { get; set; } = 20;

	public string SplitPromptMode { get; set; } = "AlwaysAsk";

	public double SplitSearchDistance { get; set; } = 0.25;

	public List<string> SplitPointPlacementGroups { get; set; } = new List<string> { "ELECTRICDEVICE", "ELECTRICJUNCTION" };

	public List<string> SplitLinePlacementGroups { get; set; } = new List<string> { "ELECTRICLINE" };

	public List<string> SplitTargetLineGroups { get; set; } = new List<string> { "ELECTRICLINE" };

	public List<string> SplitTargetLayerNames { get; set; } = new List<string>();

	public bool EnableMultiSegmentParallelCopy { get; set; } = true;

	public bool RequireConnectedParallelCopySpan { get; set; } = true;

	public double ParallelCopyEndpointMatchTolerance { get; set; } = 0.001;

	public double DefaultParallelCopyOffsetDistance { get; set; } = 1.0;

	public bool DefaultParallelCopyLeftSide { get; set; } = true;

	public bool RememberLastParallelCopyOptions { get; set; } = true;

	public bool AutoCreateParallelCopyWhenSelectedLineExists { get; set; }

	public bool EnableAssociationPrompts { get; set; } = true;

	public bool EnableStructuralAttachmentPrompts { get; set; } = true;

	public bool EnableContainmentPointPrompts { get; set; } = true;

	public bool EnableContainmentBoundaryPrompts { get; set; } = true;

	public bool EnableJunctionJunctionConnectivityPrompts { get; set; } = true;

	public bool EnableLineAssociationPrompts { get; set; }

	public bool EnableLineStructuralAttachmentPrompts { get; set; }

	public bool EnableLineContainmentPointPrompts { get; set; }

	public bool EnableLineContainmentBoundaryPrompts { get; set; } = true;

	public string AssociationPromptMode { get; set; } = "AlwaysAsk";

	public string ConfiguredAssociationPlacementMode { get; set; } = "Fast";

	public bool StopAfterFirstSuccessfulAssociation { get; set; } = true;

	public bool HighlightAssociationCandidates { get; set; } = true;

	public bool HighlightSplitCandidates { get; set; } = true;

	public bool ShowAutomaticStepDiagnostics { get; set; } = true;

	public bool UseCompactDockpaneLayout { get; set; }

	public bool EnableContinuousPlacementMode { get; set; }

	public List<string> SymbolRotationFieldNames { get; set; } = new List<string> { "ROTATION", "SYMBOLROTATION", "SYMBOL_ROTATION", "ANGLE" };

	public double DefaultSymbolRotationWhenMissing { get; set; } = 90.0;

	public string HintSourceColorHex { get; set; } = "#00FF50";

	public string HintAssociationTargetColorHex { get; set; } = "#FF0000";

	public string HintSplitCandidateColorHex { get; set; } = "#FF0000";

	public string AssociationRulesJsonPath { get; set; }

	public List<string> FavouriteTemplateKeys { get; set; } = new List<string>();

	public int MaxRecentTemplates { get; set; } = 15;

	public List<string> RecentTemplateKeys { get; set; } = new List<string>();

	public List<PlacementAttributeOverrideValue> SessionAttributeOverrides { get; set; } = new List<PlacementAttributeOverrideValue>();

	public double AssociationSearchDistance { get; set; } = 1.0;

	public double StructuralAttachmentSearchDistance { get; set; } = 1.0;

	public double JunctionJunctionConnectivitySearchDistance { get; set; } = 1.0;

	public double ContainmentPointSearchDistance { get; set; } = 1.0;

	public double ContainmentBoundarySearchDistance { get; set; } = 1.0;

	public List<string> AssociationPlacementGroups { get; set; } = new List<string> { "ELECTRICDEVICE", "ELECTRICJUNCTION", "ELECTRICLINE" };

	public List<string> StructuralAttachmentTargetGroups { get; set; } = new List<string> { "STRUCTUREJUNCTION" };

	public List<string> StructuralAttachmentTargetLayerNames { get; set; } = new List<string> { "POLE" };

	public List<string> JunctionJunctionConnectivityTargetGroups { get; set; } = new List<string> { "ELECTRICDEVICE", "ELECTRICJUNCTION" };

	public List<string> JunctionJunctionConnectivityTargetLayerNames { get; set; } = new List<string>();

	public List<string> ContainmentPointTargetGroups { get; set; } = new List<string> { "STRUCTUREJUNCTION" };

	public List<string> ContainmentPointTargetLayerNames { get; set; } = new List<string>();

	public List<string> ContainmentBoundaryTargetGroups { get; set; } = new List<string> { "STRUCTUREBOUNDARY", "STRUCTURELINE" };

	public List<string> ContainmentBoundaryTargetLayerNames { get; set; } = new List<string> { "CUBICLE", "FACILITY BOUNDARY", "FOUNDATION BOUNDARY", "TRENCH", "CONDUIT" };

	public TemplateEditorSettings Clone()
	{
		TemplateEditorSettings templateEditorSettings = JsonSerializer.Deserialize<TemplateEditorSettings>(JsonSerializer.Serialize(this)) ?? new TemplateEditorSettings();
		templateEditorSettings.Normalize();
		return templateEditorSettings;
	}

	public void Normalize()
	{
		SplitSearchDistance = Math.Max(0.0, SplitSearchDistance);
		AssociationSearchDistance = Math.Max(0.0, AssociationSearchDistance);
		StructuralAttachmentSearchDistance = Math.Max(0.0, StructuralAttachmentSearchDistance);
		JunctionJunctionConnectivitySearchDistance = Math.Max(0.0, JunctionJunctionConnectivitySearchDistance);
		ContainmentPointSearchDistance = Math.Max(0.0, ContainmentPointSearchDistance);
		ContainmentBoundarySearchDistance = Math.Max(0.0, ContainmentBoundarySearchDistance);
		ParallelCopyEndpointMatchTolerance = Math.Max(0.0, ParallelCopyEndpointMatchTolerance);
		DefaultParallelCopyOffsetDistance = Math.Max(0.001, DefaultParallelCopyOffsetDistance);
		MaxSplitCandidatesToReview = Math.Max(1, MaxSplitCandidatesToReview);
		AssociationRulesJsonPath = (string.IsNullOrWhiteSpace(AssociationRulesJsonPath) ? null : AssociationRulesJsonPath.Trim());
		SplitPromptMode = NormalizeChoice(SplitPromptMode, "AlwaysAsk", "AutoWhenOne", "Never");
		AssociationPromptMode = NormalizeChoice(AssociationPromptMode, "AlwaysAsk", "AutoWhenOne", "ReviewMultipleOnly", "Never");
		ConfiguredAssociationPlacementMode = NormalizeChoice(ConfiguredAssociationPlacementMode, "Fast", "Fast", "Debug");
		HintSourceColorHex = NormalizeHexColor(HintSourceColorHex, "#00FF50");
		HintAssociationTargetColorHex = NormalizeHexColor(HintAssociationTargetColorHex, "#FF0000");
		HintSplitCandidateColorHex = NormalizeHexColor(HintSplitCandidateColorHex, "#FF0000");
		SplitPointPlacementGroups = NormalizeGroupNames(SplitPointPlacementGroups);
		SplitLinePlacementGroups = NormalizeGroupNames(SplitLinePlacementGroups);
		SplitTargetLineGroups = NormalizeGroupNames(SplitTargetLineGroups);
		SplitTargetLayerNames = NormalizeGroupNames(SplitTargetLayerNames);
		AssociationPlacementGroups = NormalizeGroupNames(AssociationPlacementGroups);
		StructuralAttachmentTargetGroups = NormalizeGroupNames(StructuralAttachmentTargetGroups);
		StructuralAttachmentTargetLayerNames = NormalizeGroupNames(StructuralAttachmentTargetLayerNames);
		JunctionJunctionConnectivityTargetGroups = NormalizeGroupNames(JunctionJunctionConnectivityTargetGroups);
		JunctionJunctionConnectivityTargetLayerNames = NormalizeGroupNames(JunctionJunctionConnectivityTargetLayerNames);
		ContainmentPointTargetGroups = NormalizeGroupNames(ContainmentPointTargetGroups);
		ContainmentPointTargetLayerNames = NormalizeGroupNames(ContainmentPointTargetLayerNames);
		ContainmentBoundaryTargetGroups = NormalizeGroupNames(ContainmentBoundaryTargetGroups);
		ContainmentBoundaryTargetLayerNames = NormalizeGroupNames(ContainmentBoundaryTargetLayerNames);
		SymbolRotationFieldNames = NormalizeGroupNames(SymbolRotationFieldNames);
		DefaultSymbolRotationWhenMissing = NormalizeRotationDegrees(DefaultSymbolRotationWhenMissing);
		MaxRecentTemplates = Math.Max(1, Math.Min(50, MaxRecentTemplates));
		if (FavouriteTemplateKeys == null)
		{
			List<string> list = (FavouriteTemplateKeys = new List<string>());
		}
		if (RecentTemplateKeys == null)
		{
			List<string> list = (RecentTemplateKeys = new List<string>());
		}
		SessionAttributeOverrides = PlacementAttributeOverrideService.NormalizeOverrides(SessionAttributeOverrides);
	}

	public static List<string> ParseGroupNames(string text)
	{
		return NormalizeGroupNames((text ?? string.Empty).Split(','));
	}

	public static string FormatGroupNames(IEnumerable<string> groupNames)
	{
		return string.Join(", ", NormalizeGroupNames(groupNames));
	}

	private static List<string> NormalizeGroupNames(IEnumerable<string> groupNames)
	{
		return (from name in groupNames ?? Enumerable.Empty<string>()
			select (name ?? string.Empty).Trim() into name
			where !string.IsNullOrWhiteSpace(name)
			select name.ToUpperInvariant()).Distinct().ToList();
	}

	private static string NormalizeChoice(string value, string defaultValue, params string[] validValues)
	{
		return validValues.Contains<string>(value, StringComparer.OrdinalIgnoreCase) ? validValues.First((string validValue) => string.Equals(validValue, value, StringComparison.OrdinalIgnoreCase)) : defaultValue;
	}

	private static string NormalizeHexColor(string value, string fallback)
	{
		string text = (value ?? string.Empty).Trim();
		if (text.StartsWith("#", StringComparison.Ordinal))
		{
			text = text.Substring(1);
		}
		if (text.Length != 6 || text.Any((char c) => !Uri.IsHexDigit(c)))
		{
			return fallback;
		}
		return "#" + text.ToUpperInvariant();
	}

	private static double NormalizeRotationDegrees(double degrees)
	{
		degrees %= 360.0;
		return (degrees < 0.0) ? (degrees + 360.0) : degrees;
	}
}
