using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;

namespace TemplateEditor;

internal static class AddinConfiguration
{
	private sealed class UserSettingsEnvelope
	{
		public string TemplateConfigFilePath { get; set; }

		public TemplateEditorSettings Settings { get; set; }
	}

	private const string DefaultFeatureLayerGroupNames = "ELECTRICDEVICE,ELECTRICLINE,ELECTRICJUNCTION,STRUCTUREJUNCTION,STRUCTUREBOUNDARY,STRUCTURELINE,FIBER OPTIC";

	private static readonly JsonSerializerOptions _settingsJsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	internal static string UserDataDirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TemplateEditor");

	private static string UserSettingsFilePath => Path.Combine(UserDataDirectoryPath, "user-settings.json");

	public static string DefaultTemplateConfigFilePath { get; private set; }

	public static List<string> GroupFeatureLayerNames { get; private set; }

	public static TemplateEditorSettings Settings { get; private set; }

	public static TemplateConfig Templates { get; private set; }

	public static DisplayTemplate SelectedTemplate { get; private set; }

	public static PlacementMirrorMode PlacementMirrorMode { get; private set; }

	public static string TemplateConfigFilePath => Settings?.TemplateConfigFilePath;

	public static bool ValidateConfig => Settings != null && Settings.ValidateConfig;

	public static event Action SettingsChanged;

	public static TemplateConfig ReloadTemplates()
	{
		TemplateConfig templateConfig = LoadTemplateConfig();
		SetTemplates(templateConfig);
		return templateConfig;
	}

	public static void SetTemplates(TemplateConfig templates)
	{
		Templates = templates;
		TemplateCache.Initialize(templates);
		CommonFunctions.ClearPreviewGeometryCache();
	}

	public static void SetSelectedTemplate(DisplayTemplate template)
	{
		SelectedTemplate = template;
	}

	public static void ClearSelectedTemplate(bool resetMirrorMode = false)
	{
		SelectedTemplate = null;
		if (resetMirrorMode)
		{
			PlacementMirrorMode = PlacementMirrorMode.None;
		}
	}

	public static void SetPlacementMirrorMode(PlacementMirrorMode mirrorMode)
	{
		PlacementMirrorMode = mirrorMode;
	}

	public static void Initialize()
	{
		LoadBuiltInDefaults();
		PlacementAttributeOverrideService.Initialize();
		LoadUserSettings();
		if (GroupFeatureLayerNames == null)
		{
			GroupFeatureLayerNames = new List<string>();
		}
		if (Settings == null)
		{
			Settings = new TemplateEditorSettings();
		}
		Settings.Normalize();
	}

	public static bool HasValidTemplateConfigPath()
	{
		return !string.IsNullOrWhiteSpace(TemplateConfigFilePath) && File.Exists(TemplateConfigFilePath);
	}

	public static TemplateConfig LoadTemplateConfig()
	{
		return LoadTemplateConfig(TemplateConfigFilePath);
	}

	public static string PromptForTemplateConfigFilePath(string initialPath)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = "Select Template Configuration",
			Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
			CheckFileExists = true,
			Multiselect = false
		};
		string text = ((!string.IsNullOrWhiteSpace(initialPath)) ? initialPath : TemplateConfigFilePath);
		if (!string.IsNullOrWhiteSpace(text))
		{
			string directoryName = Path.GetDirectoryName(text);
			if (!string.IsNullOrWhiteSpace(directoryName) && Directory.Exists(directoryName))
			{
				openFileDialog.InitialDirectory = directoryName;
			}
			openFileDialog.FileName = Path.GetFileName(text);
		}
		else if (!string.IsNullOrWhiteSpace(DefaultTemplateConfigFilePath))
		{
			string directoryName2 = Path.GetDirectoryName(DefaultTemplateConfigFilePath);
			if (!string.IsNullOrWhiteSpace(directoryName2) && Directory.Exists(directoryName2))
			{
				openFileDialog.InitialDirectory = directoryName2;
			}
			openFileDialog.FileName = Path.GetFileName(DefaultTemplateConfigFilePath);
		}
		return (openFileDialog.ShowDialog() == true) ? openFileDialog.FileName : null;
	}

	public static bool ShowSettingsWindow()
	{
		TemplateSettingsWindow templateSettingsWindow = new TemplateSettingsWindow(Settings);
		if (templateSettingsWindow.ShowDialog() != true)
		{
			return false;
		}
		ApplySettings(templateSettingsWindow.Settings);
		return true;
	}

	public static void ApplySettings(TemplateEditorSettings settings)
	{
		Settings = settings?.Clone() ?? new TemplateEditorSettings();
		if (string.IsNullOrWhiteSpace(Settings.TemplateConfigFilePath))
		{
			Settings.TemplateConfigFilePath = DefaultTemplateConfigFilePath;
		}
		Settings.Normalize();
		SaveUserSettings();
		AssociationRuleCatalog.Reload();
		SettingsChanged?.Invoke();
	}

	public static void SaveSettings()
	{
		SaveUserSettings();
	}

	public static bool ToggleFavourite(string key)
	{
		if (string.IsNullOrWhiteSpace(key) || Settings == null)
		{
			return false;
		}
		TemplateEditorSettings settings = Settings;
		if (settings.FavouriteTemplateKeys == null)
		{
			List<string> list = (settings.FavouriteTemplateKeys = new List<string>());
		}
		bool flag = Settings.FavouriteTemplateKeys.RemoveAll((string k) => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) > 0;
		if (!flag)
		{
			Settings.FavouriteTemplateKeys.Add(key);
		}
		SaveUserSettings();
		return !flag;
	}

	public static void RecordRecentTemplate(string key)
	{
		if (!string.IsNullOrWhiteSpace(key) && Settings != null)
		{
			TemplateEditorSettings settings = Settings;
			if (settings.RecentTemplateKeys == null)
			{
				List<string> list = (settings.RecentTemplateKeys = new List<string>());
			}
			Settings.RecentTemplateKeys.RemoveAll((string k) => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
			Settings.RecentTemplateKeys.Insert(0, key);
			int num = Math.Max(1, Settings.MaxRecentTemplates);
			if (Settings.RecentTemplateKeys.Count > num)
			{
				Settings.RecentTemplateKeys.RemoveRange(num, Settings.RecentTemplateKeys.Count - num);
			}
			SaveUserSettings();
		}
	}

	private static void LoadBuiltInDefaults()
	{
		// These values used to live in TemplateEditor.dll.config. Keeping immutable
		// startup defaults in code avoids shipping a second add-in config file, while
		// user choices continue to persist in the add-in's local application data folder.
		Settings = new TemplateEditorSettings();
		DefaultTemplateConfigFilePath = string.Empty;
		Settings.TemplateConfigFilePath = DefaultTemplateConfigFilePath;
		Settings.ValidateConfig = false;
		GroupFeatureLayerNames = ParseCsv(DefaultFeatureLayerGroupNames).ToList();
		Settings.Normalize();
	}

	private static void LoadUserSettings()
	{
		// User settings overlay the built-in defaults. If the saved JSON is corrupt,
		// it is moved aside so a bad preference file cannot prevent the add-in from loading.
		string text = UserSettingsFilePath;
		if (File.Exists(text))
		{
			UserSettingsEnvelope userSettingsEnvelope;
			try
			{
				userSettingsEnvelope = JsonSerializer.Deserialize<UserSettingsEnvelope>(File.ReadAllText(text));
			}
			catch (Exception exception)
			{
				LogService.LogException("User settings could not be loaded from '" + text + "'. Falling back to packaged/default settings.", exception);
				TryMoveCorruptSettingsFile(text);
				return;
			}
			if (userSettingsEnvelope?.Settings != null)
			{
				Settings = userSettingsEnvelope.Settings;
			}
			if (Settings == null)
			{
				Settings = new TemplateEditorSettings();
			}
			if (!string.IsNullOrWhiteSpace(userSettingsEnvelope?.TemplateConfigFilePath))
			{
				Settings.TemplateConfigFilePath = userSettingsEnvelope.TemplateConfigFilePath;
			}
			Settings.Normalize();
		}
	}

	private static void TryMoveCorruptSettingsFile(string settingsPath)
	{
		try
		{
			string destFileName = settingsPath + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss");
			File.Move(settingsPath, destFileName);
		}
		catch (Exception exception)
		{
			LogService.LogException("Could not move corrupt settings file '" + settingsPath + "' to a backup path.", exception);
		}
	}

	private static void SaveUserSettings()
	{
		// Settings are written atomically so an ArcGIS Pro crash or machine shutdown
		// cannot leave a half-written JSON file behind.
		try
		{
			Directory.CreateDirectory(UserDataDirectoryPath);
			UserSettingsEnvelope value = new UserSettingsEnvelope
			{
				TemplateConfigFilePath = Settings.TemplateConfigFilePath,
				Settings = Settings
			};
			AtomicFileService.WriteAllText(UserSettingsFilePath, JsonSerializer.Serialize(value, _settingsJsonOptions));
		}
		catch (Exception exception)
		{
			LogService.LogException("User settings could not be saved; the current session will continue without persisting this change.", exception);
		}
	}

	private static IEnumerable<string> ParseCsv(string csv)
	{
		return from name in (csv ?? string.Empty).Split(',')
			select name.Trim() into name
			where !string.IsNullOrWhiteSpace(name)
			select name;
	}

	private static TemplateConfig LoadTemplateConfig(string configFilePath)
	{
		if (string.IsNullOrWhiteSpace(configFilePath))
		{
			throw new InvalidOperationException("No template configuration file is selected.");
		}
		if (!File.Exists(configFilePath))
		{
			throw new FileNotFoundException("The template configuration file could not be found.", configFilePath);
		}
		TemplateConfig templateConfig = JsonSerializer.Deserialize<TemplateConfig>(File.ReadAllText(configFilePath));
		if (templateConfig == null)
		{
			throw new InvalidOperationException("The selected template configuration file is empty or invalid.");
		}
		TemplateConfig templateConfig2 = templateConfig;
		if (templateConfig2.SimpleTemplates == null)
		{
			List<SimpleTemplate> list = (templateConfig2.SimpleTemplates = new List<SimpleTemplate>());
		}
		templateConfig2 = templateConfig;
		if (templateConfig2.GroupTemplates == null)
		{
			List<GroupTemplate> list3 = (templateConfig2.GroupTemplates = new List<GroupTemplate>());
		}
		return templateConfig;
	}

}
