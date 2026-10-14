using SlShared.I18n;

namespace Trapline
{
    // The mod texts (src/i18n/) through the i18n library, which reads the display language. No other code of the mod
    // reads the language. The Take All word is the game's own word (text key WebUI_ItemPickup_1), and the i18n files
    // give it only when the game has no text for the key, for example after a game update that removes or renames it.
    internal static class ModTexts
    {
        private static readonly GameTexts texts =
            new GameTexts(typeof(ModTexts).Assembly, "Trapline", warning => Plugin.Log.LogWarning("Trapline: " + warning));
        private static LanguageChoice logged;

        // The mod texts in the display language. The language can change while the game runs, so the texts are read
        // again at each push of the trap data.
        internal static I18nTextSet Current()
        {
            var set = texts.Current();
            if (!texts.Choice.Equals(logged))
            {
                logged = texts.Choice;
                if (Plugin.Verbose.Value) Plugin.Log.LogDebug("Trapline texts: " + logged);
            }
            return set;
        }
    }
}
