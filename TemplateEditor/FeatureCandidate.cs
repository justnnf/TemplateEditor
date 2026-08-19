using System.Diagnostics;
using System.Runtime.CompilerServices;
using ArcGIS.Core.Data.UtilityNetwork;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Mapping;

namespace TemplateEditor;

internal sealed class FeatureCandidate
{
	[CompilerGenerated]
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private AssociationType _003CAssociationType_003Ek__BackingField;

	public FeatureLayer Layer { get; set; }

	public long ObjectID { get; set; }

	public Geometry Geometry { get; set; }

	public string Label { get; set; }

	public double Distance { get; set; }

	public AssociationType AssociationType
	{
		[CompilerGenerated]
		get
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return _003CAssociationType_003Ek__BackingField;
		}
		[CompilerGenerated]
		set
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			_003CAssociationType_003Ek__BackingField = value;
		}
	}

	public bool CreatedFeatureIsAssociationSource { get; set; }
}
