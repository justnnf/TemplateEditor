using System.Diagnostics;
using System.Runtime.CompilerServices;
using ArcGIS.Core.Data.UtilityNetwork;
using ArcGIS.Desktop.Mapping;

namespace TemplateEditor;

internal sealed class ExistingAssociationPair
{
	[CompilerGenerated]
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private AssociationType _003CAssociationType_003Ek__BackingField;

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

	public MapMember FirstMember { get; set; }

	public long FirstObjectID { get; set; }

	public MapMember SecondMember { get; set; }

	public long SecondObjectID { get; set; }

	public bool Matches(AssociationType associationType, MapMember firstMember, long firstObjectID, MapMember secondMember, long secondObjectID)
	{
		if (AssociationType != associationType || firstMember == null || secondMember == null || firstObjectID <= 0 || secondObjectID <= 0)
		{
			return false;
		}
		return (MatchesEndpoint(FirstMember, FirstObjectID, firstMember, firstObjectID) && MatchesEndpoint(SecondMember, SecondObjectID, secondMember, secondObjectID)) || (MatchesEndpoint(FirstMember, FirstObjectID, secondMember, secondObjectID) && MatchesEndpoint(SecondMember, SecondObjectID, firstMember, firstObjectID));
	}

	private static bool MatchesEndpoint(MapMember expectedMember, long expectedObjectID, MapMember actualMember, long actualObjectID)
	{
		return expectedMember == actualMember && expectedObjectID == actualObjectID;
	}
}
