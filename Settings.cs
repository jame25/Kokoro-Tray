using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

namespace KokoroTray
{
    public class Settings
    {
        private static Settings instance;
        public static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.conf");
        private Dictionary<string, object> settings;

        // Event to notify when a setting changes
        public event EventHandler<string> SettingChanged;

        public static Settings Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new Settings();
                }
                return instance;
            }
        }

        private Settings()
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string jsonContent = File.ReadAllText(SettingsPath);
                    settings = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent);
                }
                else
                {
                    settings = new Dictionary<string, object>
                    {
                        { "Voice", "af_heart" },
                        { "Speed", 1.0f },
                        { "MonitorClipboard", true },
                        { "MinimumTextLength", 1 },
                        { "MaximumTextLength", 5000 },
                        { "UseGPU", 1 },  // 1 for GPU, 0 for CPU
                        { "EnableLogging", false },  // Logging disabled by default
                        // Menu visibility settings
                        { "ShowMonitoring", true },
                        { "ShowStopSpeech", true },
                        { "ShowPauseResume", false },
                        // Default hotkey settings - Monitoring hotkey disabled by default
                        { "Hotkey1Enabled", false },     // Monitoring hotkey disabled by default
                        { "Hotkey1Modifier", "" },       // No default modifier
                        { "Hotkey1Key", "" },            // No default key
                        // Default preset settings
                        { "Preset1Name", "Preset 1" },
                        { "Preset1Voice", "af_heart" },
                        { "Preset1Speed", "1.0" },
                        { "Preset1Enabled", true },
                        { "CurrentPreset", "Preset 1" },  // Set Preset 1 as active
                        // Ensure other presets are disabled by default
                        { "Preset2Enabled", false },
                        { "Preset3Enabled", false },
                        { "Preset4Enabled", false }
                    };
                    SaveSettings();
                }
            }
            catch (Exception ex)
            {
                // Use defaults if loading fails
                settings = new Dictionary<string, object>
                {
                    { "Voice", "af_heart" },
                    { "Speed", 1.0f },
                    { "MonitorClipboard", true },
                    { "MinimumTextLength", 1 },
                    { "MaximumTextLength", 5000 },
                    { "UseGPU", 1 },  // 1 for GPU, 0 for CPU
                    { "EnableLogging", false },  // Logging disabled by default
                    // Menu visibility settings
                    { "ShowMonitoring", true },
                    { "ShowStopSpeech", true },
                    { "ShowPauseResume", false },
                    // Default hotkey settings - Monitoring hotkey disabled by default
                    { "Hotkey1Enabled", false },     // Monitoring hotkey disabled by default
                    { "Hotkey1Modifier", "" },       // No default modifier
                    { "Hotkey1Key", "" },            // No default key
                    // Default preset settings
                    { "Preset1Name", "Preset 1" },
                    { "Preset1Voice", "af_heart" },
                    { "Preset1Speed", "1.0" },
                    { "Preset1Enabled", true },
                    { "CurrentPreset", "Preset 1" },  // Set Preset 1 as active
                    // Ensure other presets are disabled by default
                    { "Preset2Enabled", false },
                    { "Preset3Enabled", false },
                    { "Preset4Enabled", false }
                };
            }
        }

        public void SaveSettings()
        {
            try
            {
                // Only log if logging is enabled
                if (GetSetting<bool>("EnableLogging", true))
                {
                    Logger.Info("Saving settings to settings.conf");
                }
                string jsonContent = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, jsonContent);
            }
            catch (Exception ex)
            {
                // Always log errors
                Logger.Error("Error saving settings", ex);
            }
        }

        public T GetSetting<T>(string key, T defaultValue = default)
        {
            try
            {
                if (settings.TryGetValue(key, out object value))
                {
                    if (value is JsonElement jsonElement)
                    {
                        return jsonElement.Deserialize<T>();
                    }
                    return (T)Convert.ChangeType(value, typeof(T));
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting setting {key}", ex);
            }
            return defaultValue;
        }

        public void SetSetting<T>(string key, T value)
        {
            settings[key] = value;
            SaveSettings();
            SettingChanged?.Invoke(this, key);  // Notify subscribers that the setting changed
        }

        public void BatchSetSettings(Dictionary<string, object> settingsToUpdate)
        {
            foreach (var kvp in settingsToUpdate)
            {
                settings[kvp.Key] = kvp.Value;
                SettingChanged?.Invoke(this, kvp.Key);
            }
            SaveSettings(); // Save only once at the end
        }
    }
} 
