// Library copy of shared/i18n 2.0.0. Do not edit: see src/Shared/i18n/VERSION.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace SlShared.I18n
{
    // The mod texts of one mod: its i18n files (en.json, zh.json, cs.json, ...) and its text keys (text-keys.json).
    // For gives the text of each mod text in one display language, in this order: the game's text of its text key,
    // the i18n file of the language, en.json, the name itself. The library is game-free: the mod gives the
    // LanguageType number of the game, the code of the active community language pack, and a function that returns
    // the game's text of a text key. The library alone picks the file of the language (CodeOrder).
    internal sealed class I18nTexts
    {
        public const string EnglishCode = "en";
        public const string TextKeysFile = "text-keys.json";

        // The LanguageType numbers of the game (GameCore.HotUpdate.LanguageType).
        private const int Chinese = 0;
        private const int Community = 2;

        private readonly Dictionary<string, Dictionary<string, string>> byCode =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> textKeys = new Dictionary<string, string>();
        private readonly List<string> warnings = new List<string>();
        private readonly HashSet<string> warned = new HashSet<string>();

        private I18nTexts() { }

        // Each warning of the texts, once, in the order it came. The list only grows, so the mod writes each entry
        // past the count that it wrote before.
        public IReadOnlyList<string> Warnings => warnings;

        // The language codes of the i18n files to try for a display language, first to last: Chinese is zh, en;
        // English is en; a community language pack is its code, the main part of its code (cs for cs-CZ), en. A
        // Chinese pack (zh-TW) so reaches zh.json through its main part, and no other pack reaches Chinese. No number
        // (the config is not ready), a pack with no code, and a number that the library does not know give en.
        public static IReadOnlyList<string> CodeOrder(int? languageType, string communityCode)
        {
            var order = new List<string>();
            if (languageType == Chinese) order.Add("zh");
            else if (languageType == Community && !string.IsNullOrWhiteSpace(communityCode))
            {
                string code = communityCode.Trim();
                order.Add(code);
                int dash = code.IndexOf('-');
                if (dash > 0) Add(order, code.Substring(0, dash));
            }
            Add(order, EnglishCode);
            return order;
        }

        private static void Add(List<string> order, string code)
        {
            foreach (string c in order) if (string.Equals(c, code, StringComparison.OrdinalIgnoreCase)) return;
            order.Add(code);
        }

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

        // The texts of one display language: the LanguageType number of the game (null when the mod cannot read it)
        // and the code of the active community language pack (null for none). The first i18n file of CodeOrder gives
        // the texts, and en.json each text that it leaves out. gameText gives the game's text of a text key in that
        // language; null reads no text key (a language other than the display language).
        public I18nTextSet For(int? languageType, string communityCode, Func<string, string> gameText)
        {
            string code = EnglishCode;
            Dictionary<string, string> language = null;
            foreach (string c in CodeOrder(languageType, communityCode))
            {
                if (!byCode.TryGetValue(c, out language)) continue;
                code = FileCode(c);
                break;
            }
            byCode.TryGetValue(EnglishCode, out var english);

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

        // The code as the file name of the mod writes it (cs-CZ for a pack code CS-cz).
        private string FileCode(string code)
        {
            foreach (string c in byCode.Keys) if (string.Equals(c, code, StringComparison.OrdinalIgnoreCase)) return c;
            return code;
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

        // The language code of the i18n file that gives the texts (en when no file of the language is there).
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
