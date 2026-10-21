using Trapline;
using Xunit;

public class DurabilityLogicTests
{
    [Theory]
    // Capture_Cost 200 at trapping level 1 (no reduction), level 3 (50), and level 5 (150).
    [InlineData(200, 0, 0f, 200)]
    [InlineData(200, 50, 0f, 150)]
    [InlineData(200, 150, 0f, 50)]
    // The endless talent: ratio 0.9 leaves a tenth of the wear.
    [InlineData(200, 0, 0.9f, 20)]
    [InlineData(200, 150, 0.9f, 5)]
    // A ratio above 0.9 counts as 0.9, and one below 0 as 0.
    [InlineData(200, 0, 0.95f, 20)]
    [InlineData(200, 0, -0.5f, 200)]
    // The game rounds half to even.
    [InlineData(150, 0, 0.75f, 38)]
    [InlineData(130, 0, 0.75f, 32)]
    // A reduction above the cost gives no wear.
    [InlineData(200, 250, 0f, 0)]
    public void Wear_is_the_capture_cost_less_the_reduction_times_one_less_the_ratio(int cost, int reduction, float ratio, int wear)
    {
        Assert.Equal(wear, DurabilityLogic.Wear(cost, reduction, ratio));
    }

    [Theory]
    [InlineData(200, 200, 1)]
    [InlineData(400, 200, 2)]
    [InlineData(401, 200, 3)]
    [InlineData(600, 50, 12)]
    [InlineData(1, 150, 1)]
    [InlineData(0, 200, 0)]
    // A wear of 0 or less counts as 1.
    [InlineData(2, 0, 2)]
    [InlineData(3, -5, 3)]
    public void PickupsLeft_is_the_current_durability_over_the_wear_rounded_up(int current, int wear, int left)
    {
        Assert.Equal(left, DurabilityLogic.PickupsLeft(current, wear));
    }

    [Theory]
    [InlineData(200, 800, 25)]
    [InlineData(400, 1000, 40)]
    [InlineData(333, 1000, 33)]
    [InlineData(999, 1000, 99)]
    [InlineData(0, 1000, 0)]
    public void MarkPercent_is_rounded_down(int current, int max, int percent)
    {
        Assert.Equal(percent, DurabilityLogic.MarkPercent(current, max));
    }

    [Theory]
    [InlineData(1000, 800, 1000)]
    [InlineData(0, 800, 800)]
    [InlineData(-1, 2400, 2400)]
    public void MaxDurability_below_1_takes_the_config_value(int max, int configMax, int result)
    {
        Assert.Equal(result, DurabilityLogic.MaxDurability(max, configMax));
    }

    [Fact]
    public void A_Simple_Mousetrap_at_level_1_with_one_pickup_left_shows_25()
    {
        Assert.Equal(25, DurabilityLogic.Mark(200, 800, 800, DurabilityLogic.Wear(200, 0, 0f)));
    }

    [Fact]
    public void A_Live_Capture_Cage_at_level_1_with_two_pickups_left_shows_no_mark()
    {
        Assert.Null(DurabilityLogic.Mark(400, 1000, 1000, DurabilityLogic.Wear(200, 0, 0f)));
    }

    [Fact]
    public void An_Auto_Trap_Cage_at_level_5_with_many_pickups_left_shows_no_mark()
    {
        Assert.Null(DurabilityLogic.Mark(600, 2400, 2400, DurabilityLogic.Wear(200, 150, 0f)));
    }

    [Fact]
    public void The_same_trap_at_the_endless_ratio_has_more_pickups_left_and_no_mark()
    {
        // 200 of 800 with a wear of 20: 10 pickups left.
        Assert.Null(DurabilityLogic.Mark(200, 800, 800, DurabilityLogic.Wear(200, 0, 0.9f)));
        // 40 of 800 with a wear of 20: 2 pickups left.
        Assert.Null(DurabilityLogic.Mark(40, 800, 800, DurabilityLogic.Wear(200, 0, 0.9f)));
        // 20 of 800 with a wear of 20: the next pickup breaks it.
        Assert.Equal(2, DurabilityLogic.Mark(20, 800, 800, DurabilityLogic.Wear(200, 0, 0.9f)));
    }

    [Fact]
    public void A_trap_that_a_pickup_does_not_wear_shows_no_mark()
    {
        // A wear of 0 never breaks the trap, also at durability 1.
        Assert.Null(DurabilityLogic.Mark(1, 800, 800, 0));
    }

    [Fact]
    public void A_max_below_1_takes_the_config_value_for_the_mark()
    {
        Assert.Equal(25, DurabilityLogic.Mark(200, 0, 800, 200));
    }
}
