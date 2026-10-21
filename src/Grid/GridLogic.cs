using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using SlShared.FloorGrid;

namespace Trapline
{
    // Game-free logic. This file must not use any game or BepInEx type, because the unit tests compile it alone.
    // Lays out placed traps and usable trap slots by floor and by zone, for the HUD grid. Does not use RouteLogic.
    public static class GridLogic
    {
        // The most cells of one line of the grid.
        public const int CellsPerLine = 8;

        // One trap slot, read from the game.
        public sealed class Slot
        {
            // The key of the slot in the trap slot config. It sets the fixed cell order of a zone.
            public int Id;
            public string NodeName;
            public int Floor;
            // The zone of the slot (Config_TrapSlot.Room_Id).
            public int Zone;
            public bool Unlocked;
            public bool BlockedByPreDisaster;
            public bool InHomeMap;
        }

        // Lays out the floor lines, the zone lines, and the cells of the HUD grid in the given floor order. The cells
        // are the usable slots (a slot with a trap, or a free slot that the player can use now), keyed by the slot node
        // name, in slot id order in each zone, so a placed trap keeps its cell while the usable slots do not change.
        // The main zone of a floor (the lowest zone id of the home's slots on it: Warehouse Interior, or the 2nd Floor
        // zone next to a Balcony) has no zone line: its cells go right under the floor line. All homes share the floor
        // ids, so only the slots of the home count.
        internal static GridLayout Layout(
            IReadOnlyList<Slot> slots,
            IReadOnlyCollection<string> placedTrapNodeNames,
            IReadOnlyList<int> floorOrder)
        {
            var trapNodes = new HashSet<string>(placedTrapNodeNames);
            var cells = new List<GridCell>();
            var mainZone = new Dictionary<int, int>();
            foreach (var slot in slots)
            {
                if (trapNodes.Contains(slot.NodeName) || (slot.Unlocked && !slot.BlockedByPreDisaster && slot.InHomeMap))
                    cells.Add(new GridCell { Key = slot.NodeName, Order = slot.Id, Floor = slot.Floor, Zone = slot.Zone });
                if (slot.InHomeMap && (!mainZone.TryGetValue(slot.Floor, out int zone) || slot.Zone < zone))
                    mainZone[slot.Floor] = slot.Zone;
            }
            return FloorGridLayout.Layout(floorOrder, cells,
                (floor, zone) => mainZone.TryGetValue(floor, out int main) && main == zone, CellsPerLine);
        }

        // Only "id" is read out of the floor-button JSON of the game (State_Web_CoreUI1.FloorButtonsJson);
        // it and the array order are the one thing nothing else gives. The JSON's own "name" field is not
        // used - see TrapGroups.FloorLabel.
        private static readonly Regex FloorIdPattern = new Regex("\"id\":(-?\\d+)", RegexOptions.Compiled);

        // The floor ids of the floor switcher in its button order, each id once.
        public static List<int> ParseFloorButtonIds(string json)
        {
            var ids = new List<int>();
            if (string.IsNullOrEmpty(json)) return ids;
            foreach (Match m in FloorIdPattern.Matches(json))
                if (int.TryParse(m.Groups[1].Value, out int id) && !ids.Contains(id))
                    ids.Add(id);
            return ids;
        }
    }
}
