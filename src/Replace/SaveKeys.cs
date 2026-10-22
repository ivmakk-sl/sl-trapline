using GameCore.HotUpdate;

namespace Trapline
{
    // The Auto-replace checkbox and the option of each trap slot in the game's save dictionary GameCounterDict, so they
    // stay after a save and a load, and go back with the game when it restores an earlier day (SaveKeyLogic).
    internal static class SaveKeys
    {
        private static Il2CppSystem.Collections.Generic.Dictionary<string, int> Dict() =>
            BaseSingleton<GameSaveManager>.Instance?.gameSave?.CurSave?.GameCounterDict;

        internal static bool GlobalOn()
        {
            var dict = Dict();
            bool has = dict != null && dict.ContainsKey(SaveKeyLogic.GlobalKey);
            return SaveKeyLogic.GlobalOn(has, has ? dict[SaveKeyLogic.GlobalKey] : 0);
        }

        internal static void SetGlobal(bool on)
        {
            var dict = Dict();
            if (dict != null) dict[SaveKeyLogic.GlobalKey] = SaveKeyLogic.GlobalValue(on);
        }

        internal static bool SlotOn(string slot)
        {
            var dict = Dict();
            string key = SaveKeyLogic.SlotKey(slot);
            bool has = dict != null && dict.ContainsKey(key);
            return SaveKeyLogic.SlotOn(has, has ? dict[key] : 0);
        }

        internal static void SetSlot(string slot, bool on)
        {
            var dict = Dict();
            if (dict == null) return;
            string key = SaveKeyLogic.SlotKey(slot);
            int? value = SaveKeyLogic.SlotValue(on);
            if (value.HasValue) dict[key] = value.Value;
            else if (dict.ContainsKey(key)) dict.Remove(key);
        }
    }
}
