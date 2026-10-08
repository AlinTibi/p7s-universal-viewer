using System;
using System.IO;
using System.Text.Json;

namespace P7SExtractor
{
    // Legacy compatibility wrapper. Use LanguageManager.GetText(key) instead.
    public static class Localization
    {
        public static string CurrentLanguage { get; private set; } = "ro";

        public static void SetLanguage(string? lang)
        {
            CurrentLanguage = string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "ro";
            LanguageManager.SetLanguage(lang);
        }

        // Existing code calls T(ro, en). We register these entries in LanguageManager and return the localized value.
        public static string T(string ro, string en)
        {
            var key = string.IsNullOrWhiteSpace(en) ? ro : en;
            try
            {
                LanguageManager.RegisterEntry("en", key, en ?? key);
                LanguageManager.RegisterEntry("ro", key, ro ?? en ?? key);
            }
            catch { }

            return LanguageManager.GetText(key);
        }
    }

    public sealed class AppSettings
    {
        public string DefaultSaveFolder { get; set; } = string.Empty;
        public bool AutoOpenAfterSave { get; set; }
        public bool AutoSaveAfterAnalyze { get; set; }
        public bool KeepTempPreviewFiles { get; set; }
        // Optional donation prompt settings
        public bool ShowDonationPromptDaily { get; set; } = false;
        public string LastDonationPromptDate { get; set; } = string.Empty;
        public string Language { get; set; } = "ro"; // ro | en

        public static string GetSettingsFilePath()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "P7SExtractor");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "settings.json");
        }

        public static AppSettings LoadSettings()
        {
            try
            {
                string path = GetSettingsFilePath();
                if (!File.Exists(path))
                {
                    var s = new AppSettings();
                    Localization.SetLanguage(s.Language);
                    return s;
                }

                string json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                var loaded = settings ?? new AppSettings();
                Localization.SetLanguage(loaded.Language);
                return loaded;
            }
            catch
            {
                var s = new AppSettings();
                Localization.SetLanguage(s.Language);
                return s;
            }
        }

        public void SaveSettings()
        {
            string path = GetSettingsFilePath();
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(path, json);
        }
    }
}
