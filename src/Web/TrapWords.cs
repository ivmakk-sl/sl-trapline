using GameCore.HotUpdate;

namespace Trapline
{
    // The words that Trapline shows in the web UI.
    internal static class TrapWords
    {
        private const string TakeAllKey = "WebUI_ItemPickup_1";
        private const string FallbackEnglish = "Take All";
        private const string FallbackChinese = "全部拾取";

        // The Take All button's word. Reads the game's own word fresh on every call, because the display
        // language can change while the game runs. Falls back to the same word of the game today
        // (WebUI_ItemPickup_1 resolves to "Take All" / "全部拾取"), by the current display language, only
        // when the game gives no text for the key: a safety net for a game update that removes or renames
        // the key.
        internal static string TakeAll()
        {
            string text = ConstantTextTools.ToConstantTextOrEmpty(TakeAllKey);
            if (!string.IsNullOrEmpty(text)) return text;
            bool chinese = ConfigManager.Instance?.customCache?.LanguageType == LanguageType.Chinese;
            return chinese ? FallbackChinese : FallbackEnglish;
        }
    }
}
