using System.Collections.Generic;
using System.Linq;
using Trapline;
using Xunit;

public class MenuLogicTests
{
    private const long Backpack = 100;
    private const long ShelfA = 200;
    private const long ShelfB = 300;

    // The pool in its order: the backpack first, then the home storages in the order of the furniture list.
    private static MenuLogic.Entry E(long owner, long item, int config, int kind, int count = 1) =>
        new MenuLogic.Entry { Owner = owner, Item = item, ItemConfigId = config, Kind = kind, Count = count };

    private static string Lines(IEnumerable<MenuLogic.Line> lines) =>
        string.Join(" ", lines.Select(l => l.Kind + ":" + l.ItemConfigId + "x" + l.Count));

    [Fact]
    public void One_line_for_each_kind_in_kind_order_with_the_count_in_the_backpack_and_home_storage()
    {
        var pool = new[]
        {
            E(Backpack, 1, 25000, 2),
            E(ShelfA, 2, 25004, 4, 2),
            E(ShelfA, 3, 25000, 2),
            E(ShelfB, 4, 25000, 2),
        };

        Assert.Equal("2:25000x3 4:25004x2", Lines(MenuLogic.Lines(pool)));
    }

    [Fact]
    public void The_two_Live_Capture_Cage_items_make_one_line_with_the_name_of_the_first()
    {
        var pool = new[] { E(ShelfA, 1, 25002, 2), E(ShelfB, 2, 25000, 2) };

        Assert.Equal("2:25002x2", Lines(MenuLogic.Lines(pool)));
    }

    [Fact]
    public void An_item_that_is_not_a_trap_makes_no_line()
    {
        var pool = new[] { E(Backpack, 1, 30001, 0, 5), E(ShelfA, 2, 25001, 1) };

        Assert.Equal("1:25001x1", Lines(MenuLogic.Lines(pool)));
    }

    [Fact]
    public void No_trap_gives_no_line()
    {
        Assert.Empty(MenuLogic.Lines(new MenuLogic.Entry[0]));
    }

    [Fact]
    public void PickItem_takes_the_backpack_first()
    {
        var pool = new[] { E(Backpack, 7, 25000, 2), E(ShelfA, 8, 25000, 2) };

        var pick = MenuLogic.PickItem(pool, 2);

        Assert.Equal(7, pick.Item);
        Assert.Equal(Backpack, pick.Owner);
    }

    [Fact]
    public void PickItem_takes_the_first_home_storage_with_the_kind_when_the_backpack_has_none()
    {
        var pool = new[] { E(Backpack, 6, 25004, 4), E(ShelfA, 7, 25001, 1), E(ShelfB, 8, 25002, 2), E(ShelfB, 9, 25000, 2) };

        Assert.Equal(8, MenuLogic.PickItem(pool, 2).Item);
    }

    [Fact]
    public void PickItem_gives_none_with_no_item_of_the_kind()
    {
        Assert.Null(MenuLogic.PickItem(new[] { E(Backpack, 1, 25001, 1) }, 4));
    }

    [Fact]
    public void MenuLeave_closes_after_the_pointer_is_over_the_world_for_the_delay()
    {
        var leave = new MenuLogic.Leave();

        Assert.False(leave.Step(false, 10f));
        Assert.False(leave.Step(false, 10f + MenuLogic.Leave.Delay - 0.01f));
        Assert.True(leave.Step(false, 10f + MenuLogic.Leave.Delay));
    }

    [Fact]
    public void MenuLeave_never_closes_while_the_pointer_is_over_the_HUD()
    {
        var leave = new MenuLogic.Leave();

        Assert.False(leave.Step(true, 0f));
        Assert.False(leave.Step(true, 100f));
    }

    [Fact]
    public void MenuLeave_starts_again_when_the_pointer_comes_back_to_the_HUD()
    {
        // The gap between the cell and the menu is not a HUD area: a quick cross must not close the menu.
        var leave = new MenuLogic.Leave();

        Assert.False(leave.Step(false, 0f));
        Assert.False(leave.Step(true, 0.2f));
        Assert.False(leave.Step(false, 0.25f));
        Assert.False(leave.Step(false, 0.25f + MenuLogic.Leave.Delay - 0.01f));
        Assert.True(leave.Step(false, 0.25f + MenuLogic.Leave.Delay));
    }

    [Fact]
    public void MenuLeave_Reset_forgets_an_earlier_time_over_the_world()
    {
        var leave = new MenuLogic.Leave();
        leave.Step(false, 0f);

        leave.Reset();

        Assert.False(leave.Step(false, 5f));
    }
}
