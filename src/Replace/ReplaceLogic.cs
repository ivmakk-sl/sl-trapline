using System.Collections.Generic;

namespace Trapline
{
    // Game-free logic. This file must not use any game or BepInEx type, because the unit tests compile it alone.
    // The decision of Auto-replace for a broken trap: the item of the same kind to set on its trap slot, or the reason to
    // set none.
    public static class ReplaceLogic
    {
        public sealed class Decision
        {
            // The item to set, or null.
            public MenuLogic.Entry Pick;
            // The reason for no item, or null.
            public string Skip;
        }

        // loading: the break comes from a save load (TrapManager.LoadGame breaks a saved trap with no durability left).
        // globalOn: the Auto-replace checkbox; slotOn: the Auto-replace option of the trap slot. The item is the first of
        // the same kind in pool order (the backpack first), never one of another kind.
        public static Decision Decide(bool loading, bool globalOn, bool slotOn, int kind, IEnumerable<MenuLogic.Entry> pool)
        {
            if (loading) return new Decision { Skip = "save load" };
            if (!globalOn) return new Decision { Skip = "Auto-replace off" };
            if (!slotOn) return new Decision { Skip = "slot option off" };
            var pick = MenuLogic.PickItem(pool, kind);
            return pick == null ? new Decision { Skip = "no trap of the kind" } : new Decision { Pick = pick };
        }
    }
}
