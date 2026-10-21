namespace Trapline
{
    // Game-free logic. This file must not use any game or BepInEx type, because the unit tests compile it alone.
    // The keys of Auto-replace in the game's save dictionary GameCounterDict (string to int): the checkbox, off when its
    // key is missing, and the option of each trap slot by its slot node name, on when its key is missing. A slot node
    // name is a config value that does not change, so no key moves at a load.
    public static class SaveKeyLogic
    {
        public const string GlobalKey = "trapline.replace";

        public static string SlotKey(string slot) => GlobalKey + "." + slot;

        public static bool GlobalOn(bool has, int value) => has && value != 0;

        public static bool SlotOn(bool has, int value) => !has || value != 0;

        public static int GlobalValue(bool on) => on ? 1 : 0;

        // The value to write for the option of a trap slot: 0 for off, and null (remove the key) for on, so only an off
        // option has a key.
        public static int? SlotValue(bool on) => on ? (int?)null : 0;
    }
}
