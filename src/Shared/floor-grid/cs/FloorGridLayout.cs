// Library copy of shared/floor-grid 1.0.0. Do not edit: see src/Shared/floor-grid/VERSION.
using System;
using System.Collections.Generic;

namespace SlShared.FloorGrid
{
    // One cell of a HUD grid: its key (the mod's own id of the cell, for example a trap slot node name), its place in
    // the fixed cell order (for example the slot id), its floor, and its zone.
    internal sealed class GridCell
    {
        public string Key;
        public int Order;
        public int Floor;
        public int Zone;
    }

    // A floor line (IsFloor, Zone 0) or a zone line of the grid, and its grid row (1 is the first row).
    internal sealed class GridLine
    {
        public bool IsFloor;
        public int Floor;
        public int Zone;
        public int Row;
    }

    // The grid place of a cell: the row and the column, each from 1.
    internal struct GridPlace
    {
        public int Row;
        public int Column;
    }

    // The lines in row order, the place of each cell by its key, the row count, and the column count (the most cells
    // of one line, at least 1).
    internal sealed class GridLayout
    {
        public List<GridLine> Lines = new List<GridLine>();
        public Dictionary<string, GridPlace> Cells = new Dictionary<string, GridPlace>();
        public int Rows;
        public int Columns = 1;
    }

    // The layout of a HUD grid by floor and by zone. Each floor with a cell gets a floor line, then the cells of its
    // zones. A floor with one zone has no zone line. A floor with two or more zones has the cells of its own zone (the
    // mod decides which zone that is) right under the floor line, and a zone line over the cells of each other zone;
    // the other zones follow in the order of their first cell. The cells of a zone are in the fixed order, at most
    // perLine to a line. The floors follow floorOrder, then each other floor by its id. No game or BepInEx type here.
    internal static class FloorGridLayout
    {
        public static GridLayout Layout(IReadOnlyList<int> floorOrder, IEnumerable<GridCell> cells, Func<int, int, bool> isOwnZone, int perLine)
        {
            var byFloor = new Dictionary<int, List<GridCell>>();
            foreach (var cell in cells)
            {
                if (!byFloor.TryGetValue(cell.Floor, out var list)) byFloor[cell.Floor] = list = new List<GridCell>();
                list.Add(cell);
            }

            var floors = new List<int>();
            foreach (int floor in floorOrder)
                if (byFloor.ContainsKey(floor) && !floors.Contains(floor)) floors.Add(floor);
            var rest = new List<int>();
            foreach (int floor in byFloor.Keys)
                if (!floors.Contains(floor)) rest.Add(floor);
            rest.Sort();
            floors.AddRange(rest);

            var layout = new GridLayout();
            int row = 0;
            foreach (int floor in floors)
            {
                var floorCells = byFloor[floor];
                floorCells.Sort((a, b) => a.Order.CompareTo(b.Order));
                // The zones in the order of their first cell, then the own zone moved to the front.
                var zones = new List<int>();
                foreach (var cell in floorCells)
                    if (!zones.Contains(cell.Zone)) zones.Add(cell.Zone);
                int own = zones.FindIndex(z => isOwnZone(floor, z));
                if (own > 0)
                {
                    int zone = zones[own];
                    zones.RemoveAt(own);
                    zones.Insert(0, zone);
                }

                layout.Lines.Add(new GridLine { IsFloor = true, Floor = floor, Row = ++row });
                foreach (int zone in zones)
                {
                    bool line = zones.Count > 1 && !(zone == zones[0] && own >= 0);
                    if (line) layout.Lines.Add(new GridLine { Floor = floor, Zone = zone, Row = ++row });
                    int index = 0;
                    foreach (var cell in floorCells)
                    {
                        if (cell.Zone != zone) continue;
                        if (index % perLine == 0) row++;
                        layout.Cells[cell.Key] = new GridPlace { Row = row, Column = index % perLine + 1 };
                        index++;
                    }
                    layout.Columns = Math.Max(layout.Columns, Math.Min(index, perLine));
                }
            }
            layout.Rows = row;
            return layout;
        }
    }
}
