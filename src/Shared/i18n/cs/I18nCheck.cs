// Library copy of shared/i18n 1.0.0. Do not edit: see src/Shared/i18n/VERSION.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SlShared.I18n
{
    // The checks of the i18n files of a mod, for its unit tests. The game never calls them.
    internal static class I18nCheck
    {
        private static readonly Regex Placeholder = new Regex(@"\{([^{}]+)\}");

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
                    problems.Add(file.Key + ": the file name is not a language code that the i18n library knows, or " + I18nTexts.TextKeysFile + ".");
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

        private static bool IsKnownFileName(string file)
        {
            if (file == I18nTexts.TextKeysFile) return true;
            foreach (string code in I18nTexts.KnownCodes)
                if (file == code + ".json") return true;
            return false;
        }
    }
}
