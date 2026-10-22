using System.Collections.Generic;
using System.IO;
using System.Linq;
using SlShared.FloorGrid;
using Trapline;
using Xunit;

public class GridLogicTests
{
    private static GridLogic.Slot Usable(int id, string node, int floor = 1, int zone = 1) =>
        new GridLogic.Slot { Id = id, NodeName = node, Floor = floor, Zone = zone, Unlocked = true, BlockedByPreDisaster = false, InHomeMap = true };

    private static GridLayout Layout(List<GridLogic.Slot> slots, List<string> placed, List<int> floors) =>
        GridLogic.Layout(slots, placed, floors);

    private static string Lines(GridLayout layout) =>
        string.Join(" ", layout.Lines.Select(l => (l.IsFloor ? "F" + l.Floor : "Z" + l.Zone) + "@" + l.Row));

    private static string Place(GridLayout layout, string node) => layout.Cells[node].Row + ":" + layout.Cells[node].Column;

    [Fact]
    public void Layout_CellsFollowSlotIdOrder_NotInputOrder()
    {
        var slots = new List<GridLogic.Slot> { Usable(30, "c"), Usable(10, "a"), Usable(20, "b") };

        var layout = Layout(slots, new List<string> { "c", "a", "b" }, new List<int> { 1 });

        Assert.Equal("2:1", Place(layout, "a"));
        Assert.Equal("2:2", Place(layout, "b"));
        Assert.Equal("2:3", Place(layout, "c"));
    }

    [Fact]
    public void Layout_FreeSlotInTheMiddle_KeepsItsCell()
    {
        var slots = new List<GridLogic.Slot> { Usable(1, "a"), Usable(2, "b"), Usable(3, "c") };

        var layout = Layout(slots, new List<string> { "a", "c" }, new List<int> { 1 });

        Assert.Equal(3, layout.Cells.Count);
        Assert.Equal("2:2", Place(layout, "b"));
        Assert.Equal("2:3", Place(layout, "c"));
    }

    [Fact]
    public void Layout_SlotsThatAreNotUsable_GetNoCell()
    {
        var slots = new List<GridLogic.Slot>
        {
            new GridLogic.Slot { Id = 1, NodeName = "locked", Floor = 1, Zone = 1, Unlocked = false, InHomeMap = true },
            new GridLogic.Slot { Id = 2, NodeName = "blocked", Floor = 1, Zone = 1, Unlocked = true, BlockedByPreDisaster = true, InHomeMap = true },
            new GridLogic.Slot { Id = 3, NodeName = "away", Floor = 1, Zone = 1, Unlocked = true, InHomeMap = false },
            Usable(4, "a"),
        };

        var layout = Layout(slots, new List<string> { "a" }, new List<int> { 1 });

        Assert.Equal(new[] { "a" }, layout.Cells.Keys.ToArray());
        Assert.Equal("2:1", Place(layout, "a"));
    }

    [Fact]
    public void Layout_TrapOnNotUsableSlot_StillGetsACell()
    {
        var slots = new List<GridLogic.Slot>
        {
            Usable(1, "a"),
            new GridLogic.Slot { Id = 2, NodeName = "locked", Floor = 1, Zone = 1, Unlocked = false, InHomeMap = true },
            Usable(3, "c"),
        };

        var layout = Layout(slots, new List<string> { "locked" }, new List<int> { 1 });

        Assert.Equal("2:2", Place(layout, "locked"));
        Assert.Equal(3, layout.Cells.Count);
    }

    [Fact]
    public void Layout_FloorWithFreeSlotsAndNoTrap_HasALine()
    {
        var slots = new List<GridLogic.Slot> { Usable(1, "a", 1, 1), Usable(2, "b", 2, 2), Usable(3, "c", 2, 2) };

        var layout = Layout(slots, new List<string> { "a" }, new List<int> { 1, 2 });

        Assert.Equal("F1@1 F2@3", Lines(layout));
        Assert.Equal("4:2", Place(layout, "c"));
    }

    [Fact]
    public void Layout_FloorWithNoUsableSlotAndNoTrap_HasNoLine()
    {
        var slots = new List<GridLogic.Slot>
        {
            Usable(1, "a", 1, 1),
            new GridLogic.Slot { Id = 2, NodeName = "b", Floor = 2, Zone = 2, Unlocked = false, InHomeMap = true },
        };

        var layout = Layout(slots, new List<string> { "a" }, new List<int> { 1, 2 });

        Assert.Equal("F1@1", Lines(layout));
    }

    [Fact]
    public void Layout_TrapOnNodeThatIsNotASlot_GetsNoCell()
    {
        var layout = Layout(new List<GridLogic.Slot> { Usable(1, "a") }, new List<string> { "not-a-slot" }, new List<int> { 1 });

        Assert.False(layout.Cells.ContainsKey("not-a-slot"));
        Assert.Single(layout.Cells);
    }

    [Fact]
    public void Layout_FloorOrder_GivenOrderFirstThenOtherFloorsById()
    {
        var slots = new List<GridLogic.Slot> { Usable(1, "a", 1, 1), Usable(2, "b", 102, 102), Usable(3, "c", 101, 101), Usable(4, "d", 2, 2) };

        var layout = Layout(slots, new List<string> { "a" }, new List<int> { 2, 1 });

        Assert.Equal("F2@1 F1@3 F101@5 F102@7", Lines(layout));
    }

    [Fact]
    public void Layout_ZonesOfAFloor_ComeFromTheRoomOfEachSlot_AndTheLowestZoneIsTheMainZone()
    {
        var slots = new List<GridLogic.Slot> { Usable(6, "b1", 102, 4), Usable(5, "own", 102, 3), Usable(7, "b2", 102, 4) };

        var layout = Layout(slots, new List<string>(), new List<int> { 102 });

        Assert.Equal("F102@1 Z4@3", Lines(layout));
        Assert.Equal("2:1", Place(layout, "own"));
        Assert.Equal("4:2", Place(layout, "b2"));
    }

    [Fact]
    public void Layout_MainZone_ComesOnlyFromTheSlotsOfTheHome()
    {
        // All homes share the floor ids: a slot of another home with a lower zone does not make the main zone.
        var other = new GridLogic.Slot { Id = 1, NodeName = "other", Floor = 1, Zone = 1, Unlocked = true, InHomeMap = false };
        var slots = new List<GridLogic.Slot> { other, Usable(10, "in", 1, 7), Usable(20, "out", 1, 8) };

        var layout = Layout(slots, new List<string>(), new List<int> { 1 });

        Assert.Equal("F1@1 Z8@3", Lines(layout));
        Assert.Equal("2:1", Place(layout, "in"));
    }

    [Fact]
    public void Layout_ALineHoldsUpTo8Cells()
    {
        var slots = Enumerable.Range(1, 9).Select(i => Usable(i, "s" + i)).ToList();

        var layout = Layout(slots, new List<string>(), new List<int> { 1 });

        Assert.Equal("2:8", Place(layout, "s8"));
        Assert.Equal("3:1", Place(layout, "s9"));
    }

    // The trap slots of one home from the game's TrapSlot.tsv (the node names start with its prefix), with the floor of
    // each slot (Map_Id) and its zone (Room_Id), and the English names of MapPoint.tsv and MapRoom.tsv for the lines.
    private static string HomeLines(string nodePrefix, string notPrefix = null)
    {
        var slots = GameTables.Rows("TrapSlot")
            .Where(r => r["Position"].StartsWith(nodePrefix) && (notPrefix == null || !r["Position"].StartsWith(notPrefix)))
            .Select(r => Usable(int.Parse(r["ID"]), r["Position"], int.Parse(r["Map_Id"]), int.Parse(r["Room_Id"])))
            .ToList();
        var floorNames = GameTables.Rows("MapPoint").ToDictionary(r => int.Parse(r["ID"]), r => r["Name_en"]);
        var zoneNames = GameTables.Rows("MapRoom").ToDictionary(r => int.Parse(r["ID"]), r => r["Name_en"]);
        var layout = GridLogic.Layout(slots, new List<string>(), new List<int> { 102, 1, 101 });
        // Each line with the count of the cells between it and the next line.
        return string.Join(" | ", layout.Lines.Select((l, i) =>
        {
            int next = i + 1 < layout.Lines.Count ? layout.Lines[i + 1].Row : int.MaxValue;
            int cells = layout.Cells.Values.Count(c => c.Row > l.Row && c.Row < next);
            return (l.IsFloor ? floorNames[l.Floor] : "zone " + zoneNames[l.Zone]) + " " + cells;
        }));
    }

    [GameContextFact]
    public void TheWageSlave2ndFloor_HasItsOwnZoneUnderTheFloorLine_AndABalconyLine()
    {
        Assert.Equal("2nd Floor 1 | zone Balcony 2 | Home 4 | Basement 4", HomeLines("Home"));
    }

    [GameContextFact]
    public void TheWarehouseHomeFloor_HasInteriorUnderTheFloorLine_AnOutdoorsLine_AndIts2ndFloorOnlyTheFloorLine()
    {
        Assert.Equal("2nd Floor 2 | Home 8 | zone Outdoors 3", HomeLines("WarehousePos"));
    }

    [GameContextFact]
    public void TheStudent2ndFloor_HasOnlyBalconySlots_AndNoZoneLine()
    {
        Assert.Equal("2nd Floor 3 | Home 4 | Basement 4", HomeLines("NeighborGirl"));
    }
}

// The config tables of the game context (game/config/tables/<name>.tsv), read by their header.
internal static class GameTables
{
    public static IEnumerable<Dictionary<string, string>> Rows(string table)
    {
        string path = Path.Combine(Path.GetDirectoryName(ConstantTextTsv.Path), "..", "tables", table + ".tsv");
        var lines = File.ReadAllLines(path);
        var head = lines[0].TrimStart('﻿').Split('\t');
        foreach (var line in lines.Skip(1))
        {
            var cells = line.Split('\t');
            var row = new Dictionary<string, string>();
            for (int i = 0; i < head.Length && i < cells.Length; i++) row[head[i]] = cells[i];
            yield return row;
        }
    }
}
