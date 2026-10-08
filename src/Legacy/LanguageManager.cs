using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace P7SExtractor
{
    // LanguageManager: centralizes language dictionaries and provides lookup with fallback to English.
    // Designed for easy addition of new languages (register dictionaries or load from files/resources).
    public static class LanguageManager
    {
        private static string _current = "ro";
        [ThreadStatic]
        private static bool s_settingLanguage;
        public static string CurrentLanguage => _current;

        public static event Action? LanguageChanged;

        // languages: language code -> (key -> text)
        private static readonly Dictionary<string, Dictionary<string, string>> _languages
            = new(StringComparer.OrdinalIgnoreCase);

        static LanguageManager()
        {
            // Ensure default language containers exist. Do not populate here; app can register entries later.
            _languages["en"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _languages["ro"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public static IReadOnlyCollection<string> AvailableLanguages => _languages.Keys;

        public static void RegisterLanguage(string code, IDictionary<string, string> entries)
        {
            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentNullException(nameof(code));
            var key = Normalize(code);
            _languages[key] = new Dictionary<string, string>(entries ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
        }

        public static void RegisterEntry(string languageCode, string key, string text)
        {
            if (string.IsNullOrWhiteSpace(languageCode)) throw new ArgumentNullException(nameof(languageCode));
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));

            var code = Normalize(languageCode);
            if (!_languages.ContainsKey(code))
                _languages[code] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            _languages[code][key] = text ?? string.Empty;
        }

        public static bool TryGetEntry(string key, out string? text)
        {
            text = null;
            if (string.IsNullOrWhiteSpace(key)) return false;

            if (_languages.TryGetValue(_current, out var dict) && dict.TryGetValue(key, out var val))
            {
                text = val;
                return true;
            }

            // fallback to English
            if (_languages.TryGetValue("en", out var edict) && edict.TryGetValue(key, out var eval))
            {
                text = eval;
                return true;
            }

            return false;
        }

        // Returns the localized text for the given key. If missing, returns the key itself as a fallback.
        public static string GetText(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;
            if (TryGetEntry(key, out var text)) return text!;
            return key;
        }

        // Convenience: formatted text
        public static string GetText(string key, params object[] args)
        {
            var txt = GetText(key);
            return (args != null && args.Length > 0) ? string.Format(txt, args) : txt;
        }

        public static void SetLanguage(string? lang)
        {
            // Prevent reentrant calls between LanguageManager and the legacy Localization wrapper
            if (s_settingLanguage)
                return;

            try
            {
                s_settingLanguage = true;

                var code = Normalize(lang);
                _current = code;

                // keep compatibility with existing Localization class used across the app
                try
                {
                    Localization.SetLanguage(_current);
                }
                catch
                {
                    // ignore if Localization is not available or throws; LanguageManager remains authoritative for GetText
                }

                LanguageChanged?.Invoke();
            }
            finally
            {
                s_settingLanguage = false;
            }
        }

        private static string Normalize(string? lang)
        {
            if (string.IsNullOrWhiteSpace(lang)) return "en";
            var code = lang.Trim().ToLowerInvariant();
            // support region tags like en-US -> en
            var parts = code.Split('-', '_');
            return parts[0];
        }

        // Load language entries from a JSON file with simple key:value mapping. Async but helper for convenience.
        public static async Task LoadLanguageFromJsonFileAsync(string languageCode, string filePath)
        {
            if (string.IsNullOrWhiteSpace(languageCode)) throw new ArgumentNullException(nameof(languageCode));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath)) throw new FileNotFoundException("Language file not found", filePath);

            using var fs = File.OpenRead(filePath);
            var dict = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(fs) ?? new Dictionary<string, string>();
            RegisterLanguage(languageCode, dict);
        }
    }
}
