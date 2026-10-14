// Library copy of shared/i18n 2.0.0. Do not edit: see src/Shared/i18n/VERSION.
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using GameCore.HotUpdate;

namespace SlShared.I18n
{
    // The mod texts of a mod in the game's display language. It reads the LanguageType number, the code of the active
    // community language pack, and the game's text of each text key, and writes each new warning of the library
    // through the mod's callback. The mod names no language: the library picks the file (I18nTexts.CodeOrder).
    internal sealed class GameTexts
    {
        private readonly I18nTexts texts;
        private readonly Action<string> warn;
        private int warningsWritten;

        // The i18n files that the assembly embeds as <prefix>.i18n.<file name>, read once. warn gets each warning
        // once, with no mod name.
        public GameTexts(Assembly assembly, string prefix, Action<string> warn)
        {
            texts = I18nTexts.Load(assembly, prefix);
            this.warn = warn;
        }

        // The language of the last Current call, for the Verbose language line of the mod.
        public LanguageChoice Choice { get; private set; }

        // The mod texts in the display language. The language can change while the game runs, so a mod calls this
        // at each refresh and does not keep the set.
        public I18nTextSet Current()
        {
            int? languageType = LanguageType();
            string pack = languageType == CommunityLanguage ? CommunityCode() : null;
            var set = texts.For(languageType, pack, key => ConstantTextTools.ToConstantTextOrEmpty(key));
            Choice = new LanguageChoice(set.Language, languageType, pack);
            var warnings = texts.Warnings;
            while (warningsWritten < warnings.Count) warn?.Invoke(warnings[warningsWritten++]);
            return set;
        }

        // LanguageType.Community of the game (1.1.18599 and later).
        private const int CommunityLanguage = 2;

        // ConfigManager.customCache.LanguageType, or null before the config loads.
        private static int? LanguageType()
        {
            try
            {
                var cache = ConfigManager.Instance?.customCache;
                return cache != null ? (int)cache.LanguageType : (int?)null;
            }
            catch (Exception) { return null; }
        }

        // ConfigManager.ActiveCommunityCode, or null. The member is new in game 1.1.18599: on an older game the
        // runtime throws when it compiles the method that reads it, so the read is in its own method, called inside
        // the try.
        private static string CommunityCode()
        {
            try { return ReadCommunityCode(); }
            catch (Exception) { return null; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string ReadCommunityCode() => ConfigManager.ActiveCommunityCode;
    }

    // The display language of a text set: the code of the i18n file that gives the texts, the LanguageType number
    // (null before the config loads), and the code of the community pack (null for none).
    internal readonly struct LanguageChoice : IEquatable<LanguageChoice>
    {
        public LanguageChoice(string fileCode, int? languageType, string communityCode)
        {
            FileCode = fileCode;
            LanguageType = languageType;
            CommunityCode = communityCode;
        }

        public string FileCode { get; }
        public int? LanguageType { get; }
        public string CommunityCode { get; }

        public bool Equals(LanguageChoice other) =>
            FileCode == other.FileCode && LanguageType == other.LanguageType && CommunityCode == other.CommunityCode;

        public override bool Equals(object obj) => obj is LanguageChoice other && Equals(other);

        public override int GetHashCode() => (FileCode, LanguageType, CommunityCode).GetHashCode();

        // language=<file code> type=<number or none> pack=<code or none>
        public override string ToString() =>
            "language=" + FileCode + " type=" + (LanguageType?.ToString() ?? "none") + " pack=" + (CommunityCode ?? "none");
    }
}
