using System;
using System.Collections.Generic;
using System.Linq;
using ArcGIS.Desktop.Mapping;

namespace TemplateEditor;

internal static class MapMemberLookupService
{
    public static FeatureLayer GetFeatureLayerByName(string subtypeLayerName, string groupLayerName)
    {
        if (subtypeLayerName != null)
        {
            MapView active = MapView.Active;
            if (active == null)
            {
                return null;
            }
            return active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>()
                .FirstOrDefault((FeatureLayer n) =>
                    string.Equals(((MapMember)n).Name, subtypeLayerName, StringComparison.OrdinalIgnoreCase) &&
                    ((Layer)n).Parent is SubtypeGroupLayer parent &&
                    string.Equals(((MapMember)parent).Name, groupLayerName, StringComparison.OrdinalIgnoreCase));
        }
        MapView active2 = MapView.Active;
        return active2 != null
            ? active2.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>()
                .FirstOrDefault((FeatureLayer n) => string.Equals(((MapMember)n).Name, groupLayerName, StringComparison.OrdinalIgnoreCase))
            : null;
    }

    public static StandaloneTable GetTableByName(string subtypeLayerName, string groupLayerName)
    {
        if (subtypeLayerName != null)
        {
            MapView active = MapView.Active;
            if (active == null)
            {
                return null;
            }
            return active.Map.GetStandaloneTablesAsFlattenedList().OfType<StandaloneTable>()
                .FirstOrDefault((StandaloneTable n) =>
                    string.Equals(((MapMember)n).Name, subtypeLayerName, StringComparison.OrdinalIgnoreCase) &&
                    n.Parent is SubtypeGroupTable parent &&
                    string.Equals(((MapMember)parent).Name, groupLayerName, StringComparison.OrdinalIgnoreCase));
        }
        MapView active2 = MapView.Active;
        return active2 != null
            ? active2.Map.GetStandaloneTablesAsFlattenedList().OfType<StandaloneTable>()
                .FirstOrDefault((StandaloneTable n) => string.Equals(((MapMember)n).Name, groupLayerName, StringComparison.OrdinalIgnoreCase))
            : null;
    }

    public static FeatureLayer GetFeatureLayerByName(string layerName)
    {
        MapView active = MapView.Active;
        return active != null
            ? active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>()
                .FirstOrDefault((FeatureLayer n) => string.Equals(((MapMember)n).Name, layerName, StringComparison.OrdinalIgnoreCase))
            : null;
    }

    public static SubtypeGroupLayer GetGroupLayerByName(string layerName)
    {
        MapView active = MapView.Active;
        return active != null
            ? active.Map.GetLayersAsFlattenedList().OfType<SubtypeGroupLayer>()
                .FirstOrDefault((SubtypeGroupLayer n) => string.Equals(((MapMember)n).Name, layerName, StringComparison.OrdinalIgnoreCase))
            : null;
    }

    public static IEnumerable<FeatureLayer> GetFeatureLayersForGroups(IEnumerable<string> groupNames)
    {
        MapView active = MapView.Active;
        if (active == null || groupNames == null)
        {
            return Enumerable.Empty<FeatureLayer>();
        }
        HashSet<string> groupNameLookup = groupNames.Where((string name) => !string.IsNullOrWhiteSpace(name)).Select((string name) => name.ToUpperInvariant()).ToHashSet();
        return active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().Where(delegate(FeatureLayer layer)
        {
            string text = GetOwningGroupName(layer);
            return groupNameLookup.Contains(layer.Name.ToUpperInvariant()) || (!string.IsNullOrWhiteSpace(text) && groupNameLookup.Contains(text.ToUpperInvariant()));
        }).ToList();
    }

    public static string GetOwningGroupName(FeatureLayer layer)
    {
        if (layer == null)
        {
            return null;
        }
        return ((Layer)layer).Parent is SubtypeGroupLayer ? ((MapMember)(SubtypeGroupLayer)((Layer)layer).Parent).Name : layer.Name;
    }
}
