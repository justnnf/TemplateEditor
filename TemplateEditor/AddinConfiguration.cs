using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.Win32;

namespace TemplateEditor;

internal static class AddinConfiguration
{
	private const string TemplateConfigFilePathKey = "TemplateConfigFilePath";

	private const string LegacyTemplateConfigFilePathKey = "FramingConfigFilePath";

	private const string ValidateConfigKey = "ValidateConfig";

	private const string FeatureLayerGroupNamesKey = "FeatureLayerGroupNames";

	private static readonly JsonSerializerOptions _settingsJsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	private static string UserSettingsDirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FortisAlberta", "TemplateEditor");

	private static string LegacyUserSettingsDirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FortisAlberta", "FramingEditor");

	private static string UserSettingsFilePath => Path.Combine(UserSettingsDirectoryPath, "user-settings.json");

	private static string LegacyUserSettingsFilePath => Path.Combine(LegacyUserSettingsDirectoryPath, "user-settings.json");

	public static string DefaultTemplateConfigFilePath { get; private set; }

	public static List<string> GroupFeatureLayerNames { get; private set; }

	public static TemplateEditorSettings Settings { get; private set; }

	public static TemplateConfig Templates { get; set; }

	public static DisplayTemplate SelectedTemplate { get; set; }

	public static string TemplateConfigFilePath => Settings?.TemplateConfigFilePath;

	public static bool ValidateConfig => Settings != null && Settings.ValidateConfig;

	public static void Initialize()
	{
		LoadPackagedDefaults();
		LoadUserSettings();
		GroupFeatureLayerNames ??= new List<string>();
		Settings ??= new TemplateEditorSettings();
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
		string preferredPath = !string.IsNullOrWhiteSpace(initialPath) ? initialPath : TemplateConfigFilePath;
		if (!string.IsNullOrWhiteSpace(preferredPath))
		{
			string directoryName = Path.GetDirectoryName(preferredPath);
			if (!string.IsNullOrWhiteSpace(directoryName) && Directory.Exists(directoryName))
			{
				openFileDialog.InitialDirectory = directoryName;
			}
			openFileDialog.FileName = Path.GetFileName(preferredPath);
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
		return openFileDialog.ShowDialog() == true ? openFileDialog.FileName : null;
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
		Settings.FavouriteTemplateKeys ??= new List<string>();
		bool removed = Settings.FavouriteTemplateKeys.RemoveAll(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) > 0;
		if (!removed)
		{
			Settings.FavouriteTemplateKeys.Add(key);
		}
		SaveUserSettings();
		return !removed;
	}

	public static void RecordRecentTemplate(string key)
	{
		if (string.IsNullOrWhiteSpace(key) || Settings == null)
		{
			return;
		}
		Settings.RecentTemplateKeys ??= new List<string>();
		Settings.RecentTemplateKeys.RemoveAll(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
		Settings.RecentTemplateKeys.Insert(0, key);
		int max = Math.Max(1, Settings.MaxRecentTemplates);
		if (Settings.RecentTemplateKeys.Count > max)
		{
			Settings.RecentTemplateKeys.RemoveRange(max, Settings.RecentTemplateKeys.Count - max);
		}
		SaveUserSettings();
	}

	private static void LoadPackagedDefaults()
	{
		string appConfigPath = Assembly.GetExecutingAssembly().Location + ".config";
		GroupFeatureLayerNames = new List<string>();
		Settings = new TemplateEditorSettings();
		if (!File.Exists(appConfigPath))
		{
			return;
		}
		ExeConfigurationFileMap configFileMap = new ExeConfigurationFileMap
		{
			ExeConfigFilename = appConfigPath
		};
		Configuration configuration = ConfigurationManager.OpenMappedExeConfiguration(configFileMap, ConfigurationUserLevel.None);
		DefaultTemplateConfigFilePath = GetAppSetting(configuration, TemplateConfigFilePathKey) ?? GetAppSetting(configuration, LegacyTemplateConfigFilePathKey);
		Settings.TemplateConfigFilePath = DefaultTemplateConfigFilePath;
		Settings.ValidateConfig = bool.TryParse(GetAppSetting(configuration, ValidateConfigKey), out bool result) && result;
		string appSetting = GetAppSetting(configuration, FeatureLayerGroupNamesKey);
		if (!string.IsNullOrWhiteSpace(appSetting))
		{
			GroupFeatureLayerNames = appSetting.Split(',').Select((string name) => name.Trim()).Where((string name) => !string.IsNullOrWhiteSpace(name)).ToList();
		}
		Settings.Normalize();
	}

	private static void LoadUserSettings()
	{
		string settingsPath = File.Exists(UserSettingsFilePath) ? UserSettingsFilePath : LegacyUserSettingsFilePath;
		if (!File.Exists(settingsPath))
		{
			return;
		}
		UserSettingsEnvelope userSettingsEnvelope;
		try
		{
			userSettingsEnvelope = JsonSerializer.Deserialize<UserSettingsEnvelope>(File.ReadAllText(settingsPath));
		}
		catch (Exception)
		{
			TryMoveCorruptSettingsFile(settingsPath);
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

	private static void TryMoveCorruptSettingsFile(string settingsPath)
	{
		try
		{
			string backupPath = settingsPath + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss");
			File.Move(settingsPath, backupPath);
		}
		catch
		{
			// Ignore backup failures and keep the packaged/default settings active.
		}
	}

	private static void SaveUserSettings()
	{
		Directory.CreateDirectory(UserSettingsDirectoryPath);
		UserSettingsEnvelope value = new UserSettingsEnvelope
		{
			TemplateConfigFilePath = Settings.TemplateConfigFilePath,
			Settings = Settings
		};
		File.WriteAllText(UserSettingsFilePath, JsonSerializer.Serialize(value, _settingsJsonOptions));
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
		templateConfig.SimpleTemplates ??= new List<SimpleTemplate>();
		templateConfig.GroupTemplates ??= new List<GroupTemplate>();
		return templateConfig;
	}

	private static string GetAppSetting(Configuration configuration, string key)
	{
		return configuration.AppSettings.Settings[key]?.Value;
	}

	private sealed class UserSettingsEnvelope
	{
		public string TemplateConfigFilePath { get; set; }

		public TemplateEditorSettings Settings { get; set; }
	}
}
