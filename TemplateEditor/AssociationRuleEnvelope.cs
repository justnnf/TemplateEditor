using System.Collections.Generic;

namespace TemplateEditor;

internal sealed class AssociationRuleEnvelope
{
	public string Source { get; set; }

	public string GeneratedUtc { get; set; }

	public List<AssociationRule> Rules { get; set; }
}
