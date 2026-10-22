using Trapline;
using Xunit;

public class ReplaceLogicTests
{
    private const long Backpack = 100;
    private const long Shelf = 200;

    private static MenuLogic.Entry E(long owner, long item, int config, int kind) =>
        new MenuLogic.Entry { Owner = owner, Item = item, ItemConfigId = config, Kind = kind, Count = 1 };

    private static readonly MenuLogic.Entry[] Pool = { E(Backpack, 1, 25001, 1), E(Shelf, 2, 25000, 2), E(Shelf, 3, 25004, 4) };

    [Fact]
    public void A_break_with_Auto_replace_on_takes_an_item_of_the_same_kind()
    {
        var d = ReplaceLogic.Decide(loading: false, globalOn: true, slotOn: true, kind: 2, pool: Pool);

        Assert.Equal(2, d.Pick.Item);
        Assert.Null(d.Skip);
    }

    [Fact]
    public void The_first_item_of_the_kind_in_pool_order_wins()
    {
        var d = ReplaceLogic.Decide(false, true, true, 1, new[] { E(Shelf, 5, 25001, 1), E(Backpack, 6, 25001, 1) });

        // TrapPool puts the backpack first, so the first entry of the kind is the backpack one when it has one.
        Assert.Equal(5, d.Pick.Item);
    }

    [Fact]
    public void Auto_replace_off_sets_no_trap()
    {
        var d = ReplaceLogic.Decide(false, false, true, 2, Pool);

        Assert.Null(d.Pick);
        Assert.Equal("Auto-replace off", d.Skip);
    }

    [Fact]
    public void The_slot_option_off_sets_no_trap()
    {
        var d = ReplaceLogic.Decide(false, true, false, 2, Pool);

        Assert.Null(d.Pick);
        Assert.Equal("slot option off", d.Skip);
    }

    [Fact]
    public void No_item_of_the_kind_sets_no_trap_and_never_another_kind()
    {
        var d = ReplaceLogic.Decide(false, true, true, 3, Pool);

        Assert.Null(d.Pick);
        Assert.Equal("no trap of the kind", d.Skip);
    }

    [Fact]
    public void A_break_during_a_save_load_sets_no_trap()
    {
        var d = ReplaceLogic.Decide(true, true, true, 2, Pool);

        Assert.Null(d.Pick);
        Assert.Equal("save load", d.Skip);
    }
}

public class SaveKeyLogicTests
{
    [Fact]
    public void The_keys()
    {
        Assert.Equal("trapline.replace", SaveKeyLogic.GlobalKey);
        Assert.Equal("trapline.replace.WarehousePos91", SaveKeyLogic.SlotKey("WarehousePos91"));
    }

    [Theory]
    [InlineData(false, 0, true)]
    [InlineData(true, 0, false)]
    [InlineData(true, 1, true)]
    public void A_missing_global_key_is_on(bool has, int value, bool on)
    {
        Assert.Equal(on, SaveKeyLogic.GlobalOn(has, value));
    }

    [Theory]
    [InlineData(false, 0, true)]
    [InlineData(true, 0, false)]
    [InlineData(true, 1, true)]
    public void A_missing_slot_key_is_on(bool has, int value, bool on)
    {
        Assert.Equal(on, SaveKeyLogic.SlotOn(has, value));
    }

    [Fact]
    public void Only_an_off_slot_option_writes_a_key()
    {
        Assert.Equal(0, SaveKeyLogic.SlotValue(false));
        Assert.Null(SaveKeyLogic.SlotValue(true));
    }

    [Fact]
    public void The_global_checkbox_writes_1_or_0()
    {
        Assert.Equal(1, SaveKeyLogic.GlobalValue(true));
        Assert.Equal(0, SaveKeyLogic.GlobalValue(false));
    }
}
