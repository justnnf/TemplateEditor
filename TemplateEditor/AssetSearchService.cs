using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace TemplateEditor;

internal static class AssetSearchService
{
	private const int MaxMatches = 100;
	internal enum ResultAction { AddToSelection, ZoomTo, PanTo }

	internal static async Task ApplyResultAsync(AssetSearchMatch match, ResultAction action)
	{
		MapView view = MapView.Active;
		if (view == null || !ReferenceEquals(view.Map, match.Map))
		{
			throw new InvalidOperationException("The search result belongs to a different map. Run the search again.");
		}
		if (action == ResultAction.AddToSelection)
		{
			await QueuedTask.Run(() =>
			{
				bool stillInMap = view.Map.GetLayersAsFlattenedList().Contains(match.Member as Layer) ||
					view.Map.GetStandaloneTablesAsFlattenedList().Contains(match.Member as StandaloneTable);
				if (!stillInMap || match.Member is not IDisplayTable displayTable)
				{
					throw new InvalidOperationException("The search result is no longer in the map. Run the search again.");
				}
				using Selection selection = displayTable.Select(
					new QueryFilter { ObjectIDs = new[] { match.ObjectID } }, SelectionCombinationMethod.Add);
				if (selection == null)
				{
					throw new InvalidOperationException("The search result could not be selected.");
				}
			});
			return;
		}
		if (match.Member is not FeatureLayer layer ||
			!await QueuedTask.Run(() => view.Map.GetLayersAsFlattenedList().Contains(layer)))
		{
			throw new InvalidOperationException("This result has no feature to navigate to. Run the search again if the layer changed.");
		}
		if (action == ResultAction.ZoomTo)
		{
			await view.ZoomToAsync(layer, match.ObjectID, TimeSpan.FromMilliseconds(350));
		}
		else
		{
			await view.PanToAsync(layer, match.ObjectID, TimeSpan.FromMilliseconds(350));
		}
	}

	internal static Task<AssetSearchResult> SearchAsync(string fieldName, string value)
	{
		return QueuedTask.Run(() =>
		{
			Map map = MapView.Active?.Map;
			if (map == null)
			{
				return new AssetSearchResult(Array.Empty<AssetSearchMatch>(), 0, false);
			}

			int matchCount = 0;
			int failedTables = 0;
			bool limitReached = false;
			List<AssetSearchMatch> matches = new List<AssetSearchMatch>();
			IEnumerable<MapMember> members = map.GetLayersAsFlattenedList().OfType<FeatureLayer>().Cast<MapMember>()
				.Concat(map.GetStandaloneTablesAsFlattenedList().Cast<MapMember>());
			foreach (MapMember member in members)
			{
				if (matchCount == MaxMatches)
				{
					limitReached = true;
					break;
				}
				if (member is not IDisplayTable displayTable || !displayTable.IsSelectable)
				{
					continue;
				}
				try
				{
					using Table table = displayTable.GetTable();
					using TableDefinition definition = table?.GetDefinition();
					Field field = definition?.GetFields().FirstOrDefault(f => string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase));
					string objectIdField = definition?.GetObjectIDField();
					if (field == null || string.IsNullOrEmpty(objectIdField))
					{
						continue;
					}
					string whereClause = BuildWhereClause(field.Name, field.FieldType, value);
					if (whereClause == null)
					{
						continue;
					}
					using RowCursor cursor = displayTable.Search(new QueryFilter { WhereClause = whereClause, SubFields = objectIdField });
					if (cursor == null)
					{
						throw new InvalidOperationException("The search returned no cursor.");
					}
					List<long> objectIds = new List<long>();
					while (cursor.MoveNext())
					{
						using Row row = cursor.Current;
						if (matchCount + objectIds.Count == MaxMatches)
						{
							limitReached = true;
							break;
						}
						objectIds.Add(row.GetObjectID());
					}
					matchCount += objectIds.Count;
					string kind = member is FeatureLayer ? "Feature" : "Table row";
					matches.AddRange(objectIds.Select(id => new AssetSearchMatch(map, member, kind, id)));
				}
				catch (Exception ex)
				{
					failedTables++;
					LogService.LogException("Asset search failed for '" + member.Name + "'.", ex);
				}
			}
			return new AssetSearchResult(matches, failedTables, limitReached);
		});
	}

	internal static string BuildWhereClause(string fieldName, FieldType fieldType, string value)
	{
		if (string.IsNullOrWhiteSpace(fieldName) || string.IsNullOrWhiteSpace(value))
		{
			return null;
		}
		if (fieldType == FieldType.GlobalID || fieldType == FieldType.GUID)
		{
			return Guid.TryParse(value, out Guid guid) ? fieldName + " = '{" + guid.ToString().ToUpperInvariant() + "}'" : null;
		}
		if (fieldType == FieldType.Integer || fieldType == FieldType.SmallInteger || fieldType == FieldType.BigInteger || fieldType == FieldType.OID)
		{
			return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long number)
				? fieldName + " = " + number.ToString(CultureInfo.InvariantCulture) : null;
		}
		return fieldType == FieldType.String ? fieldName + " = '" + value.Trim().Replace("'", "''") + "'" : null;
	}
}
