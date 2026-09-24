using System.Collections.Generic;

namespace Trapline
{
    // Game-free logic. This file must not use any game or BepInEx type, because the unit tests compile it alone.
    // Lays out placed traps and usable trap slots per floor, for the HUD grid. Does not use RouteLogic.
    public static class SlotLogic
    {
        // One trap slot, read from the game.
        public sealed class Slot
        {
            // The key of the slot in the trap slot config. It sets the fixed cell order of a floor.
            public int Id;
            public string NodeName;
            public int Floor;
            public bool Unlocked;
            public bool BlockedByPreDisaster;
            public bool InHomeMap;
        }

        // One floor line of the HUD grid: X, N, and the cell of each slot node that has a trap.
        public sealed class FloorLayout
        {
            public int Floor;
            public int Traps;
            public int Slots;
            public Dictionary<string, int> Cells = new Dictionary<string, int>();
        }

        // Lays out the cells of each floor in the given floor order. The
        // cells of a floor are its usable slots (a slot with a trap, or a free slot that the player can
        // use now) sorted by slot id, so a placed trap keeps its cell while the usable slots do not change.
        public static List<FloorLayout> LayoutFloors(
            IReadOnlyList<Slot> slots,
            IReadOnlyCollection<string> placedTrapNodeNames,
            IReadOnlyList<int> floorOrder)
        {
            var trapNodes = new HashSet<string>(placedTrapNodeNames);
            var usable = new List<Slot>();
            foreach (var slot in slots)
            {
                if (trapNodes.Contains(slot.NodeName) || (slot.Unlocked && !slot.BlockedByPreDisaster && slot.InHomeMap))
                    usable.Add(slot);
            }
            usable.Sort((a, b) => a.Id.CompareTo(b.Id));

            var layouts = new Dictionary<int, FloorLayout>();
            foreach (var slot in usable)
            {
                if (!layouts.TryGetValue(slot.Floor, out var layout))
                {
                    layout = new FloorLayout { Floor = slot.Floor };
                    layouts[slot.Floor] = layout;
                }
                if (trapNodes.Contains(slot.NodeName))
                {
                    layout.Cells[slot.NodeName] = layout.Slots;
                    layout.Traps++;
                }
                layout.Slots++;
            }

            // Each floor with a usable slot has a line, also with no trap.
            var result = new List<FloorLayout>();
            foreach (var floor in floorOrder)
            {
                if (layouts.TryGetValue(floor, out var layout)) result.Add(layout);
            }

            var orderedFloors = new HashSet<int>(floorOrder);
            var extra = new List<FloorLayout>();
            foreach (var layout in layouts.Values)
            {
                if (!orderedFloors.Contains(layout.Floor)) extra.Add(layout);
            }
            extra.Sort((a, b) => a.Floor.CompareTo(b.Floor));
            result.AddRange(extra);

            return result;
        }
    }
}
