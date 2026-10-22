// Library copy of shared/i18n 2.0.0. Do not edit: see src/Shared/i18n/VERSION.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SlShared.I18n
{
    // The checks of the i18n files of a mod, for its unit tests. The game never calls them.
    internal static class I18nCheck
    {
        private static readonly Regex Placeholder = new Regex(@"\{([^{}]+)\}");

        // The file name of an i18n file: a BCP 47 language tag (en, zh-TW, zh-Hant-TW), case ignored, and .json.
        private static readonly Regex LanguageFileName = new Regex(@"^[a-z]{2,3}(-[a-z0-9]{2,8})*\.json$", RegexOptions.IgnoreCase);

        private const string ChineseFile = "zh.json";

        // Each problem of the files (the file name to its JSON text), with the file and the mod text or the text key
        // in its text. constantTextKeys is the set of the constant text keys of the game context; null skips the
        // check of the text keys (no game context, as in a clone of a public mod repo).
        public static IReadOnlyList<string> Problems(IDictionary<string, string> files, ICollection<string> constantTextKeys)
        {
            var problems = new List<string>();
            var read = new Dictionary<string, Dictionary<string, string>>();
            foreach (var file in files)
            {
                if (!IsKnownFileName(file.Key))
                {
                    problems.Add(file.Key + ": the file name is not a language code (en, zh-TW) or " + I18nTexts.TextKeysFile + ".");
                    continue;
                }
                try
                {
                    read[file.Key] = I18nJson.Read(file.Value);
                }
                catch (FormatException e)
                {
                    problems.Add(file.Key + ": not a flat JSON object of texts (" + e.Message + ").");
                }
            }

            string englishFile = I18nTexts.EnglishCode + ".json";
            if (!read.TryGetValue(englishFile, out var english))
            {
                if (!files.ContainsKey(englishFile)) problems.Add(englishFile + ": the file is missing.");
                return problems;
            }

            foreach (var file in read)
            {
                if (file.Key == englishFile) continue;
                bool isTextKeys = file.Key == I18nTexts.TextKeysFile;
                foreach (var field in file.Value)
                {
                    if (!english.TryGetValue(field.Key, out var englishText))
                    {
                        problems.Add(file.Key + ": the mod text " + field.Key + " is not in " + englishFile + ".");
                        continue;
                    }
                    if (isTextKeys)
                    {
                        if (constantTextKeys != null && !constantTextKeys.Contains(field.Value))
                            problems.Add(file.Key + ": the text key " + field.Value + " of the mod text " + field.Key + " is not in the constant texts of the game.");
                        continue;
                    }
                    var englishNames = new HashSet<string>();
                    foreach (Match m in Placeholder.Matches(englishText)) englishNames.Add(m.Groups[1].Value);
                    foreach (Match m in Placeholder.Matches(field.Value))
                    {
                        if (!englishNames.Contains(m.Groups[1].Value))
                            problems.Add(file.Key + ": the mod text " + field.Key + " has the placeholder " + m.Value + ", which " + englishFile + " does not have.");
                    }
                }
            }
            return problems;
        }

        // Each problem of the i18n files that the assembly embeds as <prefix>.i18n.<file name>: the problems of
        // Problems, a missing zh.json, and each mod text of en.json that zh.json does not have. A mod ships each text
        // in both base languages of the game. constantTextKeys is as for Problems.
        public static IReadOnlyList<string> ModProblems(Assembly assembly, string prefix, IEnumerable<string> constantTextKeys)
        {
            var files = I18nTexts.ReadFiles(assembly, prefix);
            var problems = new List<string>(Problems(files, constantTextKeys != null ? new HashSet<string>(constantTextKeys) : null));
            string englishFile = I18nTexts.EnglishCode + ".json";
            if (!files.TryGetValue(englishFile, out var englishText)) return problems;
            if (!files.TryGetValue(ChineseFile, out var chineseText))
            {
                problems.Add(ChineseFile + ": the file is missing.");
                return problems;
            }
            Dictionary<string, string> english, chinese;
            try
            {
                english = I18nJson.Read(englishText);
                chinese = I18nJson.Read(chineseText);
            }
            catch (FormatException) { return problems; }
            foreach (string name in english.Keys)
            {
                if (!chinese.ContainsKey(name)) problems.Add(ChineseFile + ": the mod text " + name + " of " + englishFile + " is missing.");
            }
            return problems;
        }

        private static bool IsKnownFileName(string file) =>
            file == I18nTexts.TextKeysFile || LanguageFileName.IsMatch(file);
    }
}
