using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace TemplateEditor;

internal static class MapMemberLookupService
{
	// ArcGIS map members must be inspected on the Main CIM Thread (MCT).
	// Core methods assume that boundary; these public wrappers establish it for
	// callers that originate on either the WPF thread or the MCT.
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
		Map map = MapView.Active?.Map;
		if (map == null)
		{
			return null;
		}

		IEnumerable<FeatureLayer> layers = map.GetLayersAsFlattenedList().OfType<FeatureLayer>();
		return subtypeLayerName == null
			? layers.FirstOrDefault(layer => NamesMatch(layer.Name, groupLayerName))
			: layers.FirstOrDefault(layer =>
				NamesMatch(layer.Name, subtypeLayerName) &&
				layer.Parent is SubtypeGroupLayer parent &&
				NamesMatch(parent.Name, groupLayerName));
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
		Map map = MapView.Active?.Map;
		if (map == null)
		{
			return null;
		}

		IEnumerable<StandaloneTable> tables = map.GetStandaloneTablesAsFlattenedList().OfType<StandaloneTable>();
		return subtypeLayerName == null
			? tables.FirstOrDefault(table => NamesMatch(table.Name, groupLayerName))
			: tables.FirstOrDefault(table =>
				NamesMatch(table.Name, subtypeLayerName) &&
				table.Parent is SubtypeGroupTable parent &&
				NamesMatch(parent.Name, groupLayerName));
	}

	public static FeatureLayer GetFeatureLayerByName(string layerName)
	{
		return RunOnMct(() => GetFeatureLayerByNameCore(layerName));
	}

	private static FeatureLayer GetFeatureLayerByNameCore(string layerName)
	{
		return MapView.Active?.Map.GetLayersAsFlattenedList()
			.OfType<FeatureLayer>()
			.FirstOrDefault(layer => NamesMatch(layer.Name, layerName));
	}

	public static SubtypeGroupLayer GetGroupLayerByName(string layerName)
	{
		return RunOnMct(() => GetGroupLayerByNameCore(layerName));
	}

	private static SubtypeGroupLayer GetGroupLayerByNameCore(string layerName)
	{
		return MapView.Active?.Map.GetLayersAsFlattenedList()
			.OfType<SubtypeGroupLayer>()
			.FirstOrDefault(layer => NamesMatch(layer.Name, layerName));
	}

	public static IEnumerable<FeatureLayer> GetFeatureLayersForGroups(IEnumerable<string> groupNames)
	{
		return RunOnMct(() => GetFeatureLayersForGroupsCore(groupNames));
	}

	private static List<FeatureLayer> GetFeatureLayersForGroupsCore(IEnumerable<string> groupNames)
	{
		Map map = MapView.Active?.Map;
		if (map == null || groupNames == null)
		{
			return new List<FeatureLayer>();
		}
		HashSet<string> groupNameLookup = groupNames
			.Where(name => !string.IsNullOrWhiteSpace(name))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		return map.GetLayersAsFlattenedList().OfType<FeatureLayer>().Where(layer =>
		{
			string owningGroupName = GetOwningGroupNameCore(layer);
			return groupNameLookup.Contains(layer.Name) || groupNameLookup.Contains(owningGroupName);
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
		return layer.Parent is SubtypeGroupLayer parent ? parent.Name : layer.Name;
	}

	private static bool NamesMatch(string left, string right) =>
		string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

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
