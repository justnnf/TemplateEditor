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
	private static bool _userSettingsLoaded;

	internal static string UserDataDirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TemplateEditor");

	internal static string LocalUserDataDirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TemplateEditor");

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
		_userSettingsLoaded = false;
		MigrateLocalUserData();
		_userSettingsLoaded = LoadUserSettings();
		if (GroupFeatureLayerNames == null)
		{
			GroupFeatureLayerNames = new List<string>();
		}
		if (Settings == null)
		{
			Settings = new TemplateEditorSettings();
		}
		Settings.Normalize();
		PlacementAttributeOverrideService.Initialize();
	}

	public static void ReloadUserSettingsIfMissing()
	{
		if (_userSettingsLoaded)
		{
			return;
		}
		MigrateLocalUserData();
		_userSettingsLoaded = LoadUserSettings();
		if (_userSettingsLoaded)
		{
			PlacementAttributeOverrideService.Initialize();
			AssociationRuleCatalog.Reload();
			SettingsChanged?.Invoke();
		}
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
		PlacementAttributeOverrideService.Initialize();
		CommonFunctions.ClearPreviewGeometryCache();
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
		Settings.FavouriteTemplateKeys ??= new List<string>();
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
			Settings.RecentTemplateKeys ??= new List<string>();
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

	private static void MigrateLocalUserData()
	{
		foreach (string fileName in new[] { "user-settings.json", "AllowedAssociationRules.json", "placement-override-favourites.json" })
		{
			string source = Path.Combine(LocalUserDataDirectoryPath, fileName);
			string destination = Path.Combine(UserDataDirectoryPath, fileName);
			try
			{
				if (CopyMissingFile(source, destination))
				{
					LogService.Write("Copied existing user data to roaming profile: " + fileName);
				}
			}
			catch (Exception exception)
			{
				LogService.LogException("Could not copy existing user data to roaming profile: " + fileName, exception);
			}
		}
	}

	internal static bool CopyMissingFile(string source, string destination)
	{
		if (!File.Exists(source) || File.Exists(destination))
		{
			return false;
		}
		string directory = Path.GetDirectoryName(destination);
		Directory.CreateDirectory(directory);
		string temporaryFile = Path.Combine(directory, Path.GetRandomFileName());
		try
		{
			File.Copy(source, temporaryFile);
			File.Move(temporaryFile, destination);
			return true;
		}
		finally
		{
			if (File.Exists(temporaryFile))
			{
				File.Delete(temporaryFile);
			}
		}
	}

	private static bool LoadUserSettings()
	{
		// Leave unreadable files in place. A Citrix profile may still be loading,
		// and a later user action can retry without discarding preferences.
		string text = UserSettingsFilePath;
		try
		{
			if (!File.Exists(text))
			{
				LogService.Write("User settings not yet available at '" + text + "'. Using defaults until the next retry.");
				return false;
			}
			UserSettingsEnvelope userSettingsEnvelope = JsonSerializer.Deserialize<UserSettingsEnvelope>(File.ReadAllText(text));
			if (userSettingsEnvelope == null)
			{
				throw new InvalidDataException("The settings file did not contain a settings object.");
			}
			TemplateEditorSettings loadedSettings = userSettingsEnvelope.Settings ?? new TemplateEditorSettings();
			if (!string.IsNullOrWhiteSpace(userSettingsEnvelope?.TemplateConfigFilePath))
			{
				loadedSettings.TemplateConfigFilePath = userSettingsEnvelope.TemplateConfigFilePath;
			}
			loadedSettings.Normalize();
			Settings = loadedSettings;
			return true;
		}
		catch (Exception exception)
		{
			LogService.LogException("User settings could not be loaded from '" + text + "'. Retaining the file for a later retry.", exception);
			return false;
		}
	}

	private static void SaveUserSettings()
	{
		// Settings are written atomically so an ArcGIS Pro crash or machine shutdown
		// cannot leave a half-written JSON file behind.
		try
		{
			if (!_userSettingsLoaded && File.Exists(UserSettingsFilePath))
			{
				LogService.Write("User settings were not saved because the existing file could not be loaded. The file was left intact: " + UserSettingsFilePath);
				return;
			}
			Directory.CreateDirectory(UserDataDirectoryPath);
			UserSettingsEnvelope value = new UserSettingsEnvelope
			{
				TemplateConfigFilePath = Settings.TemplateConfigFilePath,
				Settings = Settings
			};
			AtomicFileService.WriteAllText(UserSettingsFilePath, JsonSerializer.Serialize(value, _settingsJsonOptions));
			_userSettingsLoaded = true;
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
		templateConfig.SimpleTemplates ??= new List<SimpleTemplate>();
		templateConfig.GroupTemplates ??= new List<GroupTemplate>();
		return templateConfig;
	}

}
