using System.Collections.Generic;

namespace Trapline
{
    // Game-free logic. This file must not use any game or BepInEx type, because the unit tests compile it alone.
    // The Set Trap menu of a free trap slot and the item pick of a Set Trap: from the trap items of the backpack and home
    // storage, in pool order (the backpack first, then the home storages in the order of the furniture list).
    public static class MenuLogic
    {
        // One stack of the pool: its owner, its item instance id, its item config id, its trap kind (Config_Item.Trap, 0
        // for an item that is not a trap), and its count.
        public sealed class Entry
        {
            public long Owner;
            public long Item;
            public int ItemConfigId;
            public int Kind;
            public int Count;
        }

        // One line of the menu: a trap kind, the item of its name and icon (the first item of that kind in the pool), and the
        // count of that kind.
        public sealed class Line
        {
            public int Kind;
            public int ItemConfigId;
            public int Count;
        }

        // One line for each trap kind of the pool, in kind order. The two items of the Live Capture Cage share one kind, so
        // they make one line.
        public static List<Line> Lines(IEnumerable<Entry> pool)
        {
            var byKind = new SortedDictionary<int, Line>();
            foreach (var e in pool)
            {
                if (e.Kind <= 0 || e.Count <= 0) continue;
                if (!byKind.TryGetValue(e.Kind, out var line)) byKind[e.Kind] = line = new Line { Kind = e.Kind, ItemConfigId = e.ItemConfigId };
                line.Count += e.Count;
            }
            return new List<Line>(byKind.Values);
        }

        // The item of a Set Trap: the first item of the kind in pool order, so the backpack comes first; null for none.
        public static Entry PickItem(IEnumerable<Entry> pool, int kind)
        {
            foreach (var e in pool)
                if (e.Kind == kind && e.Count > 0) return e;
            return null;
        }

        // The close of an open menu when the pointer is over the game world, not over a HUD area, for Delay seconds in
        // a row. The page cannot see this itself: a fast move onto the world can reach the HUD page with no leave event.
        // The delay lets the pointer cross the gap between the cell and the menu, which is not a HUD area.
        public sealed class Leave
        {
            public const float Delay = 0.3f;

            private float outSince = -1f;

            public void Reset() => outSince = -1f;

            // One frame: whether the pointer is over a HUD area, and the time in seconds. True when the menu closes.
            public bool Step(bool overHud, float now)
            {
                if (overHud)
                {
                    outSince = -1f;
                    return false;
                }
                if (outSince < 0f) outSince = now;
                return now - outSince >= Delay;
            }
        }
    }
}
