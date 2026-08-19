namespace TemplateEditor;

internal sealed class AssociationPromptResult
{
	public static AssociationPromptResult NotAttempted { get; } = new AssociationPromptResult();

	public FeatureCandidate Candidate { get; private set; }

	public bool WasAttempted { get; private set; }

	public bool WasCreated { get; private set; }

	public static AssociationPromptResult Created(FeatureCandidate candidate)
	{
		return new AssociationPromptResult
		{
			Candidate = candidate,
			WasAttempted = true,
			WasCreated = true
		};
	}

	public static AssociationPromptResult Failed(FeatureCandidate candidate)
	{
		return new AssociationPromptResult
		{
			Candidate = candidate,
			WasAttempted = true
		};
	}
}
