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
			return _003CAssociationType_003Ek__BackingField;
		}
		[CompilerGenerated]
		set
		{
			_003CAssociationType_003Ek__BackingField = value;
		}
	}

	public bool CreatedFeatureIsAssociationSource { get; set; }
}
