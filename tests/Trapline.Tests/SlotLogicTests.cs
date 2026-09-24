using System.Collections.Generic;
using Trapline;
using Xunit;

public class SlotLogicTests
{
    private static SlotLogic.Slot Usable(int id, string node, int floor = 1) =>
        new SlotLogic.Slot { Id = id, NodeName = node, Floor = floor, Unlocked = true, BlockedByPreDisaster = false, InHomeMap = true };

    [Fact]
    public void LayoutFloors_CellIndexesFollowSlotIdOrder_NotInputOrder()
    {
        var slots = new List<SlotLogic.Slot> { Usable(30, "c"), Usable(10, "a"), Usable(20, "b") };
        var placedTrapNodeNames = new List<string> { "c", "a", "b" };

        var floor = Assert.Single(SlotLogic.LayoutFloors(slots, placedTrapNodeNames, new List<int> { 1 }));

        Assert.Equal(0, floor.Cells["a"]);
        Assert.Equal(1, floor.Cells["b"]);
        Assert.Equal(2, floor.Cells["c"]);
    }

    [Fact]
    public void LayoutFloors_FreeSlotInTheMiddle_LeavesItsIndexEmpty()
    {
        var slots = new List<SlotLogic.Slot> { Usable(1, "a"), Usable(2, "b"), Usable(3, "c") };
        var placedTrapNodeNames = new List<string> { "a", "c" };

        var floor = Assert.Single(SlotLogic.LayoutFloors(slots, placedTrapNodeNames, new List<int> { 1 }));

        Assert.Equal(2, floor.Cells.Count);
        Assert.Equal(0, floor.Cells["a"]);
        Assert.Equal(2, floor.Cells["c"]);
        Assert.Equal(2, floor.Traps);
        Assert.Equal(3, floor.Slots);
    }

    [Fact]
    public void LayoutFloors_SlotsThatAreNotUsable_GetNoIndex()
    {
        var slots = new List<SlotLogic.Slot>
        {
            new SlotLogic.Slot { Id = 1, NodeName = "locked", Floor = 1, Unlocked = false, BlockedByPreDisaster = false, InHomeMap = true },
            new SlotLogic.Slot { Id = 2, NodeName = "blocked", Floor = 1, Unlocked = true, BlockedByPreDisaster = true, InHomeMap = true },
            new SlotLogic.Slot { Id = 3, NodeName = "away", Floor = 1, Unlocked = true, BlockedByPreDisaster = false, InHomeMap = false },
            Usable(4, "a"),
        };
        var placedTrapNodeNames = new List<string> { "a" };

        var floor = Assert.Single(SlotLogic.LayoutFloors(slots, placedTrapNodeNames, new List<int> { 1 }));

        Assert.Equal(0, floor.Cells["a"]);
        Assert.Equal(1, floor.Slots);
    }

    [Fact]
    public void LayoutFloors_TrapOnNotUsableSlot_StillGetsAnIndex()
    {
        var slots = new List<SlotLogic.Slot>
        {
            Usable(1, "a"),
            new SlotLogic.Slot { Id = 2, NodeName = "locked", Floor = 1, Unlocked = false, BlockedByPreDisaster = false, InHomeMap = true },
            Usable(3, "c"),
        };
        var placedTrapNodeNames = new List<string> { "locked" };

        var floor = Assert.Single(SlotLogic.LayoutFloors(slots, placedTrapNodeNames, new List<int> { 1 }));

        Assert.Equal(1, floor.Cells["locked"]);
        Assert.Equal(1, floor.Traps);
        Assert.Equal(3, floor.Slots);
    }

    [Fact]
    public void LayoutFloors_FloorWithFreeSlotsAndNoTrap_HasALine()
    {
        var slots = new List<SlotLogic.Slot> { Usable(1, "a", 1), Usable(2, "b", 2), Usable(3, "c", 2) };
        var placedTrapNodeNames = new List<string> { "a" };

        var result = SlotLogic.LayoutFloors(slots, placedTrapNodeNames, new List<int> { 1, 2 });

        Assert.Equal(new List<int> { 1, 2 }, result.ConvertAll(f => f.Floor));
        Assert.Equal(0, result[1].Traps);
        Assert.Equal(2, result[1].Slots);
        Assert.Empty(result[1].Cells);
    }

    [Fact]
    public void LayoutFloors_FloorWithNoUsableSlotAndNoTrap_HasNoLine()
    {
        var slots = new List<SlotLogic.Slot>
        {
            Usable(1, "a", 1),
            new SlotLogic.Slot { Id = 2, NodeName = "b", Floor = 2, Unlocked = false, BlockedByPreDisaster = false, InHomeMap = true },
        };
        var placedTrapNodeNames = new List<string> { "a" };

        var result = SlotLogic.LayoutFloors(slots, placedTrapNodeNames, new List<int> { 1, 2 });

        Assert.Equal(new List<int> { 1 }, result.ConvertAll(f => f.Floor));
    }

    [Fact]
    public void LayoutFloors_TrapOnNodeThatIsNotASlot_GetsNoCell()
    {
        var slots = new List<SlotLogic.Slot> { Usable(1, "a") };
        var placedTrapNodeNames = new List<string> { "not-a-slot" };

        var floor = Assert.Single(SlotLogic.LayoutFloors(slots, placedTrapNodeNames, new List<int> { 1 }));

        Assert.Empty(floor.Cells);
        Assert.Equal(0, floor.Traps);
        Assert.Equal(1, floor.Slots);
    }

    [Fact]
    public void LayoutFloors_FloorOrder_GivenOrderFirstThenOtherFloorsById()
    {
        var slots = new List<SlotLogic.Slot> { Usable(1, "a", 1), Usable(2, "b", 102), Usable(3, "c", 101), Usable(4, "d", 2) };
        var placedTrapNodeNames = new List<string> { "a" };

        var result = SlotLogic.LayoutFloors(slots, placedTrapNodeNames, new List<int> { 2, 1 });

        Assert.Equal(new List<int> { 2, 1, 101, 102 }, result.ConvertAll(f => f.Floor));
    }
}
