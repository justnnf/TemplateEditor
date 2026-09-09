using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace TemplateEditor;

internal static class MapMemberLookupService
{
	public static FeatureLayer GetFeatureLayerByName(string subtypeLayerName, string groupLayerName)
	{
		return RunOnMct(() => GetFeatureLayerByNameCore(subtypeLayerName, groupLayerName));
	}

	public static Task<FeatureLayer> GetFeatureLayerByNameAsync(string subtypeLayerName, string groupLayerName)
	{
		return RunOnMctAsync(() => GetFeatureLayerByNameCore(subtypeLayerName, groupLayerName));
	}

	private static FeatureLayer GetFeatureLayerByNameCore(string subtypeLayerName, string groupLayerName)
	{
		if (subtypeLayerName != null)
		{
			MapView active = MapView.Active;
			if (active == null)
			{
				return null;
			}
			return active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault(delegate(FeatureLayer n)
			{
				int result;
				if (string.Equals(((MapMember)n).Name, subtypeLayerName, StringComparison.OrdinalIgnoreCase))
				{
					ILayerContainer parent = ((Layer)n).Parent;
					SubtypeGroupLayer val = (SubtypeGroupLayer)(object)((parent is SubtypeGroupLayer) ? parent : null);
					if (val != null)
					{
						result = (string.Equals(((MapMember)val).Name, groupLayerName, StringComparison.OrdinalIgnoreCase) ? 1 : 0);
						goto IL_0038;
					}
				}
				result = 0;
				goto IL_0038;
				IL_0038:
				return (byte)result != 0;
			});
		}
		MapView active2 = MapView.Active;
		return (active2 != null) ? active2.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault((FeatureLayer n) => string.Equals(((MapMember)n).Name, groupLayerName, StringComparison.OrdinalIgnoreCase)) : null;
	}

	public static StandaloneTable GetTableByName(string subtypeLayerName, string groupLayerName)
	{
		return RunOnMct(() => GetTableByNameCore(subtypeLayerName, groupLayerName));
	}

	public static Task<StandaloneTable> GetTableByNameAsync(string subtypeLayerName, string groupLayerName)
	{
		return RunOnMctAsync(() => GetTableByNameCore(subtypeLayerName, groupLayerName));
	}

	private static StandaloneTable GetTableByNameCore(string subtypeLayerName, string groupLayerName)
	{
		if (subtypeLayerName != null)
		{
			MapView active = MapView.Active;
			if (active == null)
			{
				return null;
			}
			return active.Map.GetStandaloneTablesAsFlattenedList().OfType<StandaloneTable>().FirstOrDefault(delegate(StandaloneTable n)
			{
				int result;
				if (string.Equals(((MapMember)n).Name, subtypeLayerName, StringComparison.OrdinalIgnoreCase))
				{
					IStandaloneTableContainer parent = n.Parent;
					SubtypeGroupTable val = (SubtypeGroupTable)(object)((parent is SubtypeGroupTable) ? parent : null);
					if (val != null)
					{
						result = (string.Equals(((MapMember)val).Name, groupLayerName, StringComparison.OrdinalIgnoreCase) ? 1 : 0);
						goto IL_0038;
					}
				}
				result = 0;
				goto IL_0038;
				IL_0038:
				return (byte)result != 0;
			});
		}
		MapView active2 = MapView.Active;
		return (active2 != null) ? active2.Map.GetStandaloneTablesAsFlattenedList().OfType<StandaloneTable>().FirstOrDefault((StandaloneTable n) => string.Equals(((MapMember)n).Name, groupLayerName, StringComparison.OrdinalIgnoreCase)) : null;
	}

	public static FeatureLayer GetFeatureLayerByName(string layerName)
	{
		return RunOnMct(() => GetFeatureLayerByNameCore(layerName));
	}

	private static FeatureLayer GetFeatureLayerByNameCore(string layerName)
	{
		MapView active = MapView.Active;
		return (active != null) ? active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault((FeatureLayer n) => string.Equals(((MapMember)n).Name, layerName, StringComparison.OrdinalIgnoreCase)) : null;
	}

	public static SubtypeGroupLayer GetGroupLayerByName(string layerName)
	{
		return RunOnMct(() => GetGroupLayerByNameCore(layerName));
	}

	private static SubtypeGroupLayer GetGroupLayerByNameCore(string layerName)
	{
		MapView active = MapView.Active;
		return (active != null) ? active.Map.GetLayersAsFlattenedList().OfType<SubtypeGroupLayer>().FirstOrDefault((SubtypeGroupLayer n) => string.Equals(((MapMember)n).Name, layerName, StringComparison.OrdinalIgnoreCase)) : null;
	}

	public static IEnumerable<FeatureLayer> GetFeatureLayersForGroups(IEnumerable<string> groupNames)
	{
		return RunOnMct(() => GetFeatureLayersForGroupsCore(groupNames));
	}

	private static List<FeatureLayer> GetFeatureLayersForGroupsCore(IEnumerable<string> groupNames)
	{
		MapView active = MapView.Active;
		if (active == null || groupNames == null)
		{
			return new List<FeatureLayer>();
		}
		HashSet<string> groupNameLookup = (from name in groupNames
			where !string.IsNullOrWhiteSpace(name)
			select name.ToUpperInvariant()).ToHashSet();
		return active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().Where(delegate(FeatureLayer layer)
		{
			string owningGroupNameCore = GetOwningGroupNameCore(layer);
			return groupNameLookup.Contains(((MapMember)layer).Name.ToUpperInvariant()) || (!string.IsNullOrWhiteSpace(owningGroupNameCore) && groupNameLookup.Contains(owningGroupNameCore.ToUpperInvariant()));
		})
			.ToList();
	}

	public static string GetOwningGroupName(FeatureLayer layer)
	{
		return RunOnMct(() => GetOwningGroupNameCore(layer));
	}

	private static string GetOwningGroupNameCore(FeatureLayer layer)
	{
		if (layer == null)
		{
			return null;
		}
		return (((Layer)layer).Parent is SubtypeGroupLayer) ? ((MapMember)(SubtypeGroupLayer)((Layer)layer).Parent).Name : ((MapMember)layer).Name;
	}

	private static T RunOnMct<T>(Func<T> action)
	{
		if (QueuedTask.OnWorker)
		{
			return action();
		}
		return QueuedTask.Run<T>(action, TaskCreationOptions.None).GetAwaiter().GetResult();
	}

	private static Task<T> RunOnMctAsync<T>(Func<T> action)
	{
		if (QueuedTask.OnWorker)
		{
			return Task.FromResult(action());
		}
		return QueuedTask.Run<T>(action, TaskCreationOptions.None);
	}
}
