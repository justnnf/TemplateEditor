using System;
using System.Collections.Generic;
using System.Linq;

namespace TemplateEditor;

internal static class TemplateCache
{
	private static Dictionary<string, SimpleTemplate> _simpleTemplatesByName;

	private static Dictionary<string, GroupTemplate> _groupTemplatesByName;
	private static Dictionary<SimpleTemplateReference, SimpleTemplate> _groupParts;

	private static readonly object LockObject = new object();

	private static bool _isInitialized = false;

	public static void Initialize(TemplateConfig config)
	{
		lock (LockObject)
		{
			_simpleTemplatesByName = config?.SimpleTemplates?.Where(t => t != null && !string.IsNullOrWhiteSpace(t.Name)).GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, SimpleTemplate>(StringComparer.OrdinalIgnoreCase);
			_groupTemplatesByName = config?.GroupTemplates?.Where(t => t != null && !string.IsNullOrWhiteSpace(t.Name)).GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, GroupTemplate>(StringComparer.OrdinalIgnoreCase);
			_groupParts = new Dictionary<SimpleTemplateReference, SimpleTemplate>();
			// Repeated part names pair with definitions by occurrence in JSON order.
			var definitionsByName = (config?.SimpleTemplates ?? new List<SimpleTemplate>())
				.Where(template => template != null && !string.IsNullOrWhiteSpace(template.Name))
				.GroupBy(template => template.Name, StringComparer.OrdinalIgnoreCase)
				.ToDictionary(group => group.Key, group => new Queue<SimpleTemplate>(group), StringComparer.OrdinalIgnoreCase);
			var reusableNames = new HashSet<string>(definitionsByName.Where(pair => pair.Value.Count == 1).Select(pair => pair.Key), StringComparer.OrdinalIgnoreCase);
			foreach (GroupTemplate group in config?.GroupTemplates ?? new List<GroupTemplate>())
			{
				foreach (SimpleTemplateReference reference in group?.SimpleTemplates ?? new List<SimpleTemplateReference>())
				{
					if (reference != null && definitionsByName.TryGetValue(reference.Name ?? string.Empty, out Queue<SimpleTemplate> definitions) && definitions.Count > 0)
					{
						_groupParts[reference] = reusableNames.Contains(reference.Name) ? definitions.Peek() : definitions.Dequeue();
					}
				}
			}
			_isInitialized = true;
		}
	}

	public static SimpleTemplate GetSimpleTemplate(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return null;
		}
		if (!_isInitialized)
		{
			lock (LockObject)
			{
				if (!_isInitialized)
				{
					Initialize(AddinConfiguration.Templates);
				}
			}
		}
		lock (LockObject)
		{
			Dictionary<string, SimpleTemplate> simpleTemplatesByName = _simpleTemplatesByName;
			SimpleTemplate value;
			return (simpleTemplatesByName != null && simpleTemplatesByName.TryGetValue(name, out value)) ? value : null;
		}
	}

	public static SimpleTemplate GetSimpleTemplate(SimpleTemplateReference reference)
	{
		if (reference == null)
		{
			return null;
		}
		if (!_isInitialized)
		{
			Initialize(AddinConfiguration.Templates);
		}
		lock (LockObject)
		{
			return _groupParts != null && _groupParts.TryGetValue(reference, out SimpleTemplate template) ? template : null;
		}
	}

	public static GroupTemplate GetGroupTemplate(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return null;
		}
		if (!_isInitialized)
		{
			lock (LockObject)
			{
				if (!_isInitialized)
				{
					Initialize(AddinConfiguration.Templates);
				}
			}
		}
		lock (LockObject)
		{
			Dictionary<string, GroupTemplate> groupTemplatesByName = _groupTemplatesByName;
			GroupTemplate value;
			return (groupTemplatesByName != null && groupTemplatesByName.TryGetValue(name, out value)) ? value : null;
		}
	}

	public static void Clear()
	{
		lock (LockObject)
		{
			_simpleTemplatesByName = null;
			_groupTemplatesByName = null;
			_groupParts = null;
			_isInitialized = false;
		}
	}
}
