using System.Text.Json.Nodes;
using Trapline;
using Xunit;

public class CardLogicTests
{
    private static readonly CardLogic.Texts English = new CardLogic.Texts
    {
        Durability = "Durability",
        BaitSet = "Bait Set",
        NoBait = "No Bait",
        PreyReady = "Ready to Take",
        NoPrey = "No Prey",
    };

    [Fact]
    public void A_Live_Capture_Cage_with_bait_and_no_prey()
    {
        var card = CardLogic.Build("Live Capture Cage", 600, 1000, 1000, 30001, "../../Res/bait.png", 0, null);

        Assert.Equal("Live Capture Cage", card.Name);
        Assert.Equal(600, card.Current);
        Assert.Equal(1000, card.Max);
        Assert.Equal(60f, card.Fill);
        Assert.Equal("", card.Band);
        Assert.True(card.HasBait);
        Assert.Equal("../../Res/bait.png", card.BaitIcon);
        Assert.False(card.HasPrey);
        Assert.Equal("", card.PreyIcon);
    }

    [Theory]
    // The game's bands: green above 0.5, amber at 0.5 or less, red at 0.2 or less.
    [InlineData(510, 1000, "")]
    [InlineData(500, 1000, "medium")]
    [InlineData(201, 1000, "medium")]
    [InlineData(200, 1000, "low")]
    [InlineData(0, 1000, "low")]
    public void The_bar_color_band_follows_the_game(int current, int max, string band)
    {
        Assert.Equal(band, CardLogic.Build("t", current, max, max, 0, null, 0, null).Band);
    }

    [Fact]
    public void A_max_below_1_takes_the_config_value()
    {
        var card = CardLogic.Build("t", 200, 0, 800, 0, null, 0, null);

        Assert.Equal(800, card.Max);
        Assert.Equal(25f, card.Fill);
    }

    [Fact]
    public void The_fill_stays_from_0_to_100()
    {
        Assert.Equal(100f, CardLogic.Build("t", 1200, 1000, 1000, 0, null, 0, null).Fill);
        Assert.Equal(0f, CardLogic.Build("t", -5, 1000, 1000, 0, null, 0, null).Fill);
    }

    [Fact]
    public void Prey_with_its_icon_and_no_bait()
    {
        var card = CardLogic.Build("t", 1, 2, 2, 0, "", 30101, "../../Res/rat.png");

        Assert.False(card.HasBait);
        Assert.True(card.HasPrey);
        Assert.Equal("../../Res/rat.png", card.PreyIcon);
    }

    [Fact]
    public void CardJson_holds_the_trap_the_card_and_the_game_texts()
    {
        var card = CardLogic.Build("Live \"Cage\"", 600, 1000, 1000, 30001, "b.png", 0, null);

        var json = JsonNode.Parse(PageJson.CardJson(42, card, English));

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(
            "{\"trap\":42,\"name\":\"Live \\\"Cage\\\"\",\"cur\":600,\"max\":1000,\"fill\":60,\"band\":\"\"," +
            "\"bait\":true,\"baitIcon\":\"b.png\",\"prey\":false,\"preyIcon\":\"\"," +
            "\"texts\":{\"durability\":\"Durability\",\"baitSet\":\"Bait Set\",\"noBait\":\"No Bait\",\"preyReady\":\"Ready to Take\",\"noPrey\":\"No Prey\"}}"), json), json.ToJsonString());
    }

    [Fact]
    public void SetCardCommand_calls_setCard_or_gives_no_script()
    {
        Assert.Equal("window.__trapline?window.__trapline.setCard({}):'no script'", PageJson.SetCardCommand("{}"));
    }
}
