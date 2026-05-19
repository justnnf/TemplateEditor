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

	public bool EnableConfiguredLinePartSplits { get; set; } = true;

	public bool SuppressDuplicateSplitPrompts { get; set; } = true;

	public bool SplitOnlyInteriorCandidates { get; set; } = true;

	public int MaxSplitCandidatesToReview { get; set; } = 20;

	public string SplitPromptMode { get; set; } = "AlwaysAsk";

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

	public bool EnableLineAssociationPrompts { get; set; }

	public bool EnableLineStructuralAttachmentPrompts { get; set; }

	public bool EnableLineContainmentPointPrompts { get; set; }

	public bool EnableLineContainmentBoundaryPrompts { get; set; } = true;

	public string AssociationPromptMode { get; set; } = "AlwaysAsk";

	public bool StopAfterFirstSuccessfulAssociation { get; set; } = true;

	public bool HighlightAssociationCandidates { get; set; } = true;

	public bool HighlightSplitCandidates { get; set; } = true;

	public bool ShowAutomaticStepDiagnostics { get; set; } = true;

	public double AssociationSearchDistance { get; set; } = 1.0;

	public double StructuralAttachmentSearchDistance { get; set; } = 1.0;

	public double ContainmentPointSearchDistance { get; set; } = 1.0;

	public double ContainmentBoundarySearchDistance { get; set; } = 1.0;

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

	public List<string> StructuralAttachmentTargetLayerNames { get; set; } = new List<string>();

	public List<string> ContainmentPointTargetGroups { get; set; } = new List<string>
	{
		"STRUCTUREJUNCTION"
	};

	public List<string> ContainmentPointTargetLayerNames { get; set; } = new List<string>();

	public List<string> ContainmentBoundaryTargetGroups { get; set; } = new List<string>
	{
		"STRUCTUREBOUNDARY"
	};

	public List<string> ContainmentBoundaryTargetLayerNames { get; set; } = new List<string>();

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
			EnableConfiguredLinePartSplits = EnableConfiguredLinePartSplits,
			SuppressDuplicateSplitPrompts = SuppressDuplicateSplitPrompts,
			SplitOnlyInteriorCandidates = SplitOnlyInteriorCandidates,
			MaxSplitCandidatesToReview = MaxSplitCandidatesToReview,
			SplitPromptMode = SplitPromptMode,
			SplitSearchDistance = SplitSearchDistance,
			SplitPointPlacementGroups = new List<string>(SplitPointPlacementGroups ?? new List<string>()),
			SplitLinePlacementGroups = new List<string>(SplitLinePlacementGroups ?? new List<string>()),
			SplitTargetLineGroups = new List<string>(SplitTargetLineGroups ?? new List<string>()),
			SplitTargetLayerNames = new List<string>(SplitTargetLayerNames ?? new List<string>()),
			EnableMultiSegmentParallelCopy = EnableMultiSegmentParallelCopy,
			RequireConnectedParallelCopySpan = RequireConnectedParallelCopySpan,
			ParallelCopyEndpointMatchTolerance = ParallelCopyEndpointMatchTolerance,
			DefaultParallelCopyOffsetDistance = DefaultParallelCopyOffsetDistance,
			DefaultParallelCopyLeftSide = DefaultParallelCopyLeftSide,
			RememberLastParallelCopyOptions = RememberLastParallelCopyOptions,
			AutoCreateParallelCopyWhenSelectedLineExists = AutoCreateParallelCopyWhenSelectedLineExists,
			EnableAssociationPrompts = EnableAssociationPrompts,
			EnableStructuralAttachmentPrompts = EnableStructuralAttachmentPrompts,
			EnableContainmentPointPrompts = EnableContainmentPointPrompts,
			EnableContainmentBoundaryPrompts = EnableContainmentBoundaryPrompts,
			EnableLineAssociationPrompts = EnableLineAssociationPrompts,
			EnableLineStructuralAttachmentPrompts = EnableLineStructuralAttachmentPrompts,
			EnableLineContainmentPointPrompts = EnableLineContainmentPointPrompts,
			EnableLineContainmentBoundaryPrompts = EnableLineContainmentBoundaryPrompts,
			AssociationPromptMode = AssociationPromptMode,
			StopAfterFirstSuccessfulAssociation = StopAfterFirstSuccessfulAssociation,
			HighlightAssociationCandidates = HighlightAssociationCandidates,
			HighlightSplitCandidates = HighlightSplitCandidates,
			ShowAutomaticStepDiagnostics = ShowAutomaticStepDiagnostics,
			AssociationSearchDistance = AssociationSearchDistance,
			StructuralAttachmentSearchDistance = StructuralAttachmentSearchDistance,
			ContainmentPointSearchDistance = ContainmentPointSearchDistance,
			ContainmentBoundarySearchDistance = ContainmentBoundarySearchDistance,
			AssociationPlacementGroups = new List<string>(AssociationPlacementGroups ?? new List<string>()),
			StructuralAttachmentTargetGroups = new List<string>(StructuralAttachmentTargetGroups ?? new List<string>()),
			StructuralAttachmentTargetLayerNames = new List<string>(StructuralAttachmentTargetLayerNames ?? new List<string>()),
			ContainmentPointTargetGroups = new List<string>(ContainmentPointTargetGroups ?? new List<string>()),
			ContainmentPointTargetLayerNames = new List<string>(ContainmentPointTargetLayerNames ?? new List<string>()),
			ContainmentBoundaryTargetGroups = new List<string>(ContainmentBoundaryTargetGroups ?? new List<string>()),
			ContainmentBoundaryTargetLayerNames = new List<string>(ContainmentBoundaryTargetLayerNames ?? new List<string>())
		};
	}

	public void Normalize()
	{
		SplitSearchDistance = Math.Max(0.0, SplitSearchDistance);
		AssociationSearchDistance = Math.Max(0.0, AssociationSearchDistance);
		StructuralAttachmentSearchDistance = Math.Max(0.0, StructuralAttachmentSearchDistance);
		ContainmentPointSearchDistance = Math.Max(0.0, ContainmentPointSearchDistance);
		ContainmentBoundarySearchDistance = Math.Max(0.0, ContainmentBoundarySearchDistance);
		ParallelCopyEndpointMatchTolerance = Math.Max(0.0, ParallelCopyEndpointMatchTolerance);
		DefaultParallelCopyOffsetDistance = Math.Max(0.001, DefaultParallelCopyOffsetDistance);
		MaxSplitCandidatesToReview = Math.Max(1, MaxSplitCandidatesToReview);
		SplitPromptMode = NormalizeChoice(SplitPromptMode, "AlwaysAsk", "AutoWhenOne", "Never");
		AssociationPromptMode = NormalizeChoice(AssociationPromptMode, "AlwaysAsk", "AutoWhenOne", "ReviewMultipleOnly", "Never");
		SplitPointPlacementGroups = NormalizeGroupNames(SplitPointPlacementGroups);
		SplitLinePlacementGroups = NormalizeGroupNames(SplitLinePlacementGroups);
		SplitTargetLineGroups = NormalizeGroupNames(SplitTargetLineGroups);
		SplitTargetLayerNames = NormalizeGroupNames(SplitTargetLayerNames);
		AssociationPlacementGroups = NormalizeGroupNames(AssociationPlacementGroups);
		StructuralAttachmentTargetGroups = NormalizeGroupNames(StructuralAttachmentTargetGroups);
		StructuralAttachmentTargetLayerNames = NormalizeGroupNames(StructuralAttachmentTargetLayerNames);
		ContainmentPointTargetGroups = NormalizeGroupNames(ContainmentPointTargetGroups);
		ContainmentPointTargetLayerNames = NormalizeGroupNames(ContainmentPointTargetLayerNames);
		ContainmentBoundaryTargetGroups = NormalizeGroupNames(ContainmentBoundaryTargetGroups);
		ContainmentBoundaryTargetLayerNames = NormalizeGroupNames(ContainmentBoundaryTargetLayerNames);
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

	private static string NormalizeChoice(string value, string defaultValue, params string[] validValues)
	{
		return validValues.Contains(value, StringComparer.OrdinalIgnoreCase) ? validValues.First((string validValue) => string.Equals(validValue, value, StringComparison.OrdinalIgnoreCase)) : defaultValue;
	}
}
