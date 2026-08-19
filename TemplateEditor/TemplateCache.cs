using System;
using System.Collections.Generic;
using System.Linq;

namespace TemplateEditor;

internal static class TemplateCache
{
	private static Dictionary<string, SimpleTemplate> _simpleTemplatesByName;

	private static Dictionary<string, GroupTemplate> _groupTemplatesByName;

	private static readonly object LockObject = new object();

	private static bool _isInitialized = false;

	public static void Initialize(TemplateConfig config)
	{
		lock (LockObject)
		{
			_simpleTemplatesByName = config?.SimpleTemplates?.GroupBy<SimpleTemplate, string>((SimpleTemplate t) => t.Name, StringComparer.OrdinalIgnoreCase).ToDictionary<IGrouping<string, SimpleTemplate>, string, SimpleTemplate>((IGrouping<string, SimpleTemplate> g) => g.Key, (IGrouping<string, SimpleTemplate> g) => g.First(), StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, SimpleTemplate>(StringComparer.OrdinalIgnoreCase);
			_groupTemplatesByName = config?.GroupTemplates?.GroupBy<GroupTemplate, string>((GroupTemplate t) => t.Name, StringComparer.OrdinalIgnoreCase).ToDictionary<IGrouping<string, GroupTemplate>, string, GroupTemplate>((IGrouping<string, GroupTemplate> g) => g.Key, (IGrouping<string, GroupTemplate> g) => g.First(), StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, GroupTemplate>(StringComparer.OrdinalIgnoreCase);
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
			_isInitialized = false;
		}
	}
}
