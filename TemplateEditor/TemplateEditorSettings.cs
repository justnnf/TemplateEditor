using System;
using System.Collections.Generic;
using System.Linq;

namespace TemplateEditor;

internal sealed class TemplateEditorSettings
{
	public string TemplateConfigFilePath { get; set; }

	public bool ValidateConfig { get; set; }

	public bool EnableLineSplitPrompts { get; set; } = true;

	public bool EnablePointPlacementSplitPrompt { get; set; } = true;

	public bool EnableLineEndpointSplitPrompt { get; set; } = true;

	public bool EnableParallelCopyPrompt { get; set; } = true;

	public bool EnableSplitAtLineStartPoint { get; set; } = true;

	public bool EnableSplitAtLineEndPoint { get; set; } = true;

	public double SplitSearchDistance { get; set; } = 0.25;

	public List<string> SplitPointPlacementGroups { get; set; } = new List<string>
	{
		"ELECTRICDEVICE",
		"ELECTRICJUNCTION"
	};

	public List<string> SplitLinePlacementGroups { get; set; } = new List<string>
	{
		"ELECTRICLINE"
	};

	public List<string> SplitTargetLineGroups { get; set; } = new List<string>
	{
		"ELECTRICLINE"
	};

	public bool EnableAssociationPrompts { get; set; } = true;

	public bool EnableStructuralAttachmentPrompts { get; set; } = true;

	public bool EnableContainmentPointPrompts { get; set; } = true;

	public bool EnableContainmentBoundaryPrompts { get; set; } = true;

	public bool HighlightAssociationCandidates { get; set; } = true;

	public double AssociationSearchDistance { get; set; } = 1.0;

	public List<string> AssociationPlacementGroups { get; set; } = new List<string>
	{
		"ELECTRICDEVICE",
		"ELECTRICJUNCTION",
		"ELECTRICLINE"
	};

	public List<string> StructuralAttachmentTargetGroups { get; set; } = new List<string>
	{
		"STRUCTUREJUNCTION"
	};

	public List<string> ContainmentPointTargetGroups { get; set; } = new List<string>
	{
		"STRUCTUREJUNCTION"
	};

	public List<string> ContainmentBoundaryTargetGroups { get; set; } = new List<string>
	{
		"STRUCTUREBOUNDARY"
	};

	public TemplateEditorSettings Clone()
	{
		return new TemplateEditorSettings
		{
			TemplateConfigFilePath = TemplateConfigFilePath,
			ValidateConfig = ValidateConfig,
			EnableLineSplitPrompts = EnableLineSplitPrompts,
			EnablePointPlacementSplitPrompt = EnablePointPlacementSplitPrompt,
			EnableLineEndpointSplitPrompt = EnableLineEndpointSplitPrompt,
			EnableParallelCopyPrompt = EnableParallelCopyPrompt,
			EnableSplitAtLineStartPoint = EnableSplitAtLineStartPoint,
			EnableSplitAtLineEndPoint = EnableSplitAtLineEndPoint,
			SplitSearchDistance = SplitSearchDistance,
			SplitPointPlacementGroups = new List<string>(SplitPointPlacementGroups ?? new List<string>()),
			SplitLinePlacementGroups = new List<string>(SplitLinePlacementGroups ?? new List<string>()),
			SplitTargetLineGroups = new List<string>(SplitTargetLineGroups ?? new List<string>()),
			EnableAssociationPrompts = EnableAssociationPrompts,
			EnableStructuralAttachmentPrompts = EnableStructuralAttachmentPrompts,
			EnableContainmentPointPrompts = EnableContainmentPointPrompts,
			EnableContainmentBoundaryPrompts = EnableContainmentBoundaryPrompts,
			HighlightAssociationCandidates = HighlightAssociationCandidates,
			AssociationSearchDistance = AssociationSearchDistance,
			AssociationPlacementGroups = new List<string>(AssociationPlacementGroups ?? new List<string>()),
			StructuralAttachmentTargetGroups = new List<string>(StructuralAttachmentTargetGroups ?? new List<string>()),
			ContainmentPointTargetGroups = new List<string>(ContainmentPointTargetGroups ?? new List<string>()),
			ContainmentBoundaryTargetGroups = new List<string>(ContainmentBoundaryTargetGroups ?? new List<string>())
		};
	}

	public void Normalize()
	{
		SplitSearchDistance = Math.Max(0.0, SplitSearchDistance);
		AssociationSearchDistance = Math.Max(0.0, AssociationSearchDistance);
		SplitPointPlacementGroups = NormalizeGroupNames(SplitPointPlacementGroups);
		SplitLinePlacementGroups = NormalizeGroupNames(SplitLinePlacementGroups);
		SplitTargetLineGroups = NormalizeGroupNames(SplitTargetLineGroups);
		AssociationPlacementGroups = NormalizeGroupNames(AssociationPlacementGroups);
		StructuralAttachmentTargetGroups = NormalizeGroupNames(StructuralAttachmentTargetGroups);
		ContainmentPointTargetGroups = NormalizeGroupNames(ContainmentPointTargetGroups);
		ContainmentBoundaryTargetGroups = NormalizeGroupNames(ContainmentBoundaryTargetGroups);
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
		return (groupNames ?? Enumerable.Empty<string>()).Select((string name) => (name ?? string.Empty).Trim())
			.Where((string name) => !string.IsNullOrWhiteSpace(name))
			.Select((string name) => name.ToUpperInvariant())
			.Distinct()
			.ToList();
	}
}
