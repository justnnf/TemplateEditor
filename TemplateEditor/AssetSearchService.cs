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

	internal static Task<AssetSearchResult> SearchAndSelectAsync(string fieldName, string value)
	{
		return QueuedTask.Run(() =>
		{
			Map map = MapView.Active?.Map;
			if (map == null)
			{
				return new AssetSearchResult(0, 0, false);
			}

			int selectedCount = 0;
			int failedTables = 0;
			bool limitReached = false;
			IEnumerable<MapMember> members = map.GetLayersAsFlattenedList().OfType<FeatureLayer>().Cast<MapMember>()
				.Concat(map.GetStandaloneTablesAsFlattenedList().Cast<MapMember>());
			foreach (MapMember member in members)
			{
				if (selectedCount == MaxMatches)
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
						if (selectedCount + objectIds.Count == MaxMatches)
						{
							limitReached = true;
							break;
						}
						objectIds.Add(row.GetObjectID());
					}
					if (objectIds.Count > 0)
					{
						using Selection selection = displayTable.Select(new QueryFilter { ObjectIDs = objectIds }, SelectionCombinationMethod.Add);
						selectedCount += objectIds.Count;
					}
				}
				catch (Exception ex)
				{
					failedTables++;
					LogService.LogException("Asset search failed for '" + member.Name + "'.", ex);
				}
			}
			return new AssetSearchResult(selectedCount, failedTables, limitReached);
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
