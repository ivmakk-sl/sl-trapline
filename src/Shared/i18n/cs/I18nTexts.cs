// Library copy of shared/i18n 1.0.0. Do not edit: see src/Shared/i18n/VERSION.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace SlShared.I18n
{
    // The mod texts of one mod: its i18n files (en.json, zh.json, ...) and its text keys (text-keys.json). For gives
    // the text of each mod text in one display language, in this order: the game's text of its text key, the i18n
    // file of the language, en.json, the name itself. The library is game-free: the mod gives the LanguageType number
    // of the game and a function that returns the game's text of a text key.
    internal sealed class I18nTexts
    {
        public const string EnglishCode = "en";
        public const string TextKeysFile = "text-keys.json";

        // The language code of each LanguageType number of the game (Chinese = 0, English = 1), lower-case BCP 47.
        private static readonly string[] Codes = { "zh", "en" };

        private readonly Dictionary<string, Dictionary<string, string>> byCode = new Dictionary<string, Dictionary<string, string>>();
        private readonly Dictionary<string, string> textKeys = new Dictionary<string, string>();
        private readonly List<string> warnings = new List<string>();
        private readonly HashSet<string> warned = new HashSet<string>();

        private I18nTexts() { }

        // Each warning of the texts, once, in the order it came. The list only grows, so the mod writes each entry
        // past the count that it wrote before.
        public IReadOnlyList<string> Warnings => warnings;

        // The language codes that the library knows.
        public static IReadOnlyList<string> KnownCodes => Codes;

        // The code of a LanguageType number, or null for a number that the library does not know.
        public static string CodeOf(int languageType) =>
            languageType >= 0 && languageType < Codes.Length ? Codes[languageType] : null;

        // The texts of the i18n files that are embedded in the assembly as <prefix>.i18n.<file name>. Read once.
        public static I18nTexts Load(Assembly assembly, string prefix) => Parse(ReadFiles(assembly, prefix));

        // The text of each embedded resource named <prefix>.i18n.<file name>, by its file name.
        public static Dictionary<string, string> ReadFiles(Assembly assembly, string prefix)
        {
            string start = prefix + ".i18n.";
            var files = new Dictionary<string, string>();
            foreach (string resource in assembly.GetManifestResourceNames())
            {
                if (!resource.StartsWith(start, StringComparison.Ordinal)) continue;
                using (var stream = assembly.GetManifestResourceStream(resource))
                {
                    if (stream == null) continue;
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                        files[resource.Substring(start.Length)] = reader.ReadToEnd();
                }
            }
            return files;
        }

        // The texts from the file texts of a mod: the file name (en.json, text-keys.json) to its JSON text. A broken
        // file counts as missing and gives a warning that names it.
        internal static I18nTexts Parse(IDictionary<string, string> files)
        {
            var texts = new I18nTexts();
            foreach (var file in files)
            {
                Dictionary<string, string> fields;
                try
                {
                    fields = I18nJson.Read(file.Value);
                }
                catch (FormatException e)
                {
                    texts.Warn("The i18n file " + file.Key + " is broken (" + e.Message + "): its texts are not used.");
                    continue;
                }
                if (file.Key == TextKeysFile)
                {
                    foreach (var f in fields) texts.textKeys[f.Key] = f.Value;
                }
                else if (file.Key.EndsWith(".json", StringComparison.Ordinal))
                {
                    texts.byCode[file.Key.Substring(0, file.Key.Length - ".json".Length)] = fields;
                }
            }
            return texts;
        }

        // The texts of one display language. gameText gives the game's text of a text key in that language; null
        // reads no text key (a language other than the display language). languageName is the enum name of the
        // number, for the warning of a language that the library does not know.
        public I18nTextSet For(int languageType, Func<string, string> gameText, string languageName = null)
        {
            string code = CodeOf(languageType);
            if (code == null)
            {
                Warn("The display language " + languageType + (languageName != null ? " (" + languageName + ")" : "")
                    + " has no language code in the i18n library: the mod texts show in English.");
                code = EnglishCode;
            }
            byCode.TryGetValue(EnglishCode, out var english);
            byCode.TryGetValue(code, out var language);

            var names = new List<string>();
            var seen = new HashSet<string>();
            foreach (var source in new IEnumerable<string>[] { english?.Keys, language?.Keys, textKeys.Keys })
            {
                if (source == null) continue;
                foreach (var name in source) if (seen.Add(name)) names.Add(name);
            }

            var map = new Dictionary<string, string>();
            var gameNames = new List<string>();
            foreach (var name in names)
            {
                string text = null;
                if (gameText != null && textKeys.TryGetValue(name, out var key))
                {
                    string game = gameText(key);
                    if (!string.IsNullOrEmpty(game) && game != key)
                    {
                        text = game;
                        gameNames.Add(name);
                    }
                    else
                    {
                        Warn("The game has no text for the text key " + key + " of the mod text " + name + ": the text of the i18n file shows.");
                    }
                }
                if (text == null) text = FileText(language, name) ?? FileText(english, name);
                if (text == null)
                {
                    WarnNoText(name);
                    text = name;
                }
                map[name] = text;
            }
            return new I18nTextSet(this, code, map, gameNames);
        }

        // The text with each placeholder {name} replaced by its value, a number in the invariant culture. A
        // placeholder with no value stays as it is. There is no escape: no mod text holds a brace of its own.
        public static string Fill(string text, params (string name, object value)[] values)
        {
            if (text == null || text.IndexOf('{') < 0) return text;
            var sb = new StringBuilder(text.Length + 16);
            int i = 0;
            while (i < text.Length)
            {
                int open = text.IndexOf('{', i);
                int close = open < 0 ? -1 : text.IndexOf('}', open + 1);
                if (close < 0)
                {
                    sb.Append(text, i, text.Length - i);
                    break;
                }
                sb.Append(text, i, open - i);
                string name = text.Substring(open + 1, close - open - 1);
                if (TryValue(values, name, out string value)) sb.Append(value);
                else sb.Append(text, open, close - open + 1);
                i = close + 1;
            }
            return sb.ToString();
        }

        private static bool TryValue((string name, object value)[] values, string name, out string value)
        {
            if (values != null && name.Length > 0)
            {
                foreach (var v in values)
                {
                    if (v.name != name) continue;
                    value = Convert.ToString(v.value, CultureInfo.InvariantCulture);
                    return true;
                }
            }
            value = null;
            return false;
        }

        private static string FileText(Dictionary<string, string> file, string name) =>
            file != null && file.TryGetValue(name, out var text) && !string.IsNullOrEmpty(text) ? text : null;

        internal void WarnNoText(string name) => Warn("The mod text " + name + " has no text: its name shows.");

        private void Warn(string warning)
        {
            if (warned.Add(warning)) warnings.Add(warning);
        }
    }

    // The texts of the mod texts in one display language.
    internal sealed class I18nTextSet
    {
        private readonly I18nTexts owner;
        private readonly Dictionary<string, string> map;

        internal I18nTextSet(I18nTexts owner, string language, Dictionary<string, string> map, IReadOnlyList<string> gameTextNames)
        {
            this.owner = owner;
            Language = language;
            this.map = map;
            GameTextNames = gameTextNames;
        }

        // The language code of the texts (en for a language that the library does not know).
        public string Language { get; }

        // The names of the mod texts that show the game's text of their text key.
        public IReadOnlyList<string> GameTextNames { get; }

        // The text of a mod text. A name that no file has gives the name itself and one warning.
        public string this[string name]
        {
            get
            {
                if (name != null && map.TryGetValue(name, out var text)) return text;
                owner.WarnNoText(name);
                return name;
            }
        }

        // Each mod text with its text, for the page JSON.
        public IReadOnlyDictionary<string, string> ToDictionary() => new Dictionary<string, string>(map);
    }
}
