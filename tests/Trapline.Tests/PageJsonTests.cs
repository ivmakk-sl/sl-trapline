using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using SlShared.Json;
using Trapline;
using Xunit;

public class PageJsonTests
{
    // The groups of tests/fixtures/groups.json: the floor "Home" with the zone lines "Warehouse Interior" (traps 11 and
    // 13, trap 13 with the durability mark 25, and a free trap slot) and "Outdoors" (trap 15), the floor "The \"Cellar\"" (trap 12 and a free trap slot),
    // and trap 14 with no cell.
    internal static string FixtureGroups() => PageJson.GroupsJson(3, 7,
        new List<PageJson.Line>
        {
            new PageJson.Line { Key = "f1", Floor = true, Row = 1, Name = "Home" },
            new PageJson.Line { Key = "z1:7", Floor = false, Row = 2, Name = "Warehouse Interior" },
            new PageJson.Line { Key = "z1:8", Floor = false, Row = 4, Name = "Outdoors" },
            new PageJson.Line { Key = "f101", Floor = true, Row = 6, Name = "The \"Cellar\"" },
        },
        new List<PageJson.Cell>
        {
            new PageJson.Cell { Slot = "WarehousePos91", Row = 3, Column = 1, Trap = 11 },
            new PageJson.Cell { Slot = "WarehousePos92", Row = 3, Column = 2 },
            new PageJson.Cell { Slot = "WarehousePos93", Row = 3, Column = 3, Trap = 13, Mark = 25 },
            new PageJson.Cell { Slot = "WarehousePos99", Row = 5, Column = 1, Trap = 15 },
            new PageJson.Cell { Slot = "CellarPos19", Row = 7, Column = 1, Trap = 12 },
            new PageJson.Cell { Slot = "CellarPos20", Row = 7, Column = 2 },
        });

    [Fact]
    public void GroupsJson_matches_the_fixture()
    {
        string json = FixtureGroups();

        var expected = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "groups.json")));
        Assert.True(JsonNode.DeepEquals(expected, JsonNode.Parse(json)), json);
    }

    [Fact]
    public void GroupsJson_gives_each_cell_its_slot_node_name_and_a_free_cell_no_trap()
    {
        string json = PageJson.GroupsJson(1, 2,
            new List<PageJson.Line> { new PageJson.Line { Key = "f1", Floor = true, Row = 1, Name = "Home" } },
            new List<PageJson.Cell> { new PageJson.Cell { Slot = "HomeBuildingPos25", Row = 2, Column = 1 } });

        Assert.Equal("{\"cols\":1,\"rows\":2,\"lines\":[{\"key\":\"f1\",\"kind\":\"floor\",\"row\":1,\"name\":\"Home\"}]," +
                     "\"cells\":[{\"slot\":\"HomeBuildingPos25\",\"row\":2,\"col\":1}]}", json);
    }

    [Fact]
    public void GroupsJson_carries_the_mark_of_a_trap_with_two_pickups_left_or_less_and_none_for_the_others()
    {
        string json = PageJson.GroupsJson(2, 2,
            new List<PageJson.Line>(),
            new List<PageJson.Cell>
            {
                new PageJson.Cell { Slot = "a", Row = 2, Column = 1, Trap = 1, Mark = DurabilityLogic.Mark(200, 800, 800, 200) },
                new PageJson.Cell { Slot = "b", Row = 2, Column = 2, Trap = 2, Mark = DurabilityLogic.Mark(600, 2400, 2400, 50) },
            });

        Assert.Equal("{\"cols\":2,\"rows\":2,\"lines\":[],\"cells\":[{\"slot\":\"a\",\"row\":2,\"col\":1,\"trap\":1,\"mark\":25}," +
                     "{\"slot\":\"b\",\"row\":2,\"col\":2,\"trap\":2}]}", json);
    }

    [Fact]
    public void GroupsJson_writes_a_mark_of_0()
    {
        string json = PageJson.GroupsJson(1, 1, new List<PageJson.Line>(),
            new List<PageJson.Cell> { new PageJson.Cell { Slot = "a", Row = 1, Column = 1, Trap = 1, Mark = 0 } });

        Assert.Contains("\"mark\":0", json);
    }

    [Fact]
    public void GroupsJson_with_no_line_and_no_cell_is_empty_groups()
    {
        Assert.Equal("{\"cols\":1,\"rows\":0,\"lines\":[],\"cells\":[]}",
            PageJson.GroupsJson(1, 0, new List<PageJson.Line>(), new List<PageJson.Cell>()));
    }

    [Fact]
    public void DataJson_holds_the_words_in_name_order_the_groups_and_the_Auto_replace_checkbox()
    {
        var words = new Dictionary<string, string> { ["takeAll"] = "Take All", ["autoReplace"] = "Auto-replace" };

        Assert.Equal("{\"words\":{\"autoReplace\":\"Auto-replace\",\"takeAll\":\"Take All\"},\"groups\":null,\"replace\":true,\"pin\":false}",
            PageJson.DataJson(words, "null", true, false));
        Assert.EndsWith("\"replace\":false,\"pin\":true}", PageJson.DataJson(words, "null", false, true));
    }

    [Theory]
    [InlineData("{\"on\":true}", true)]
    [InlineData("{\"on\":false}", false)]
    [InlineData("{}", null)]
    public void A_TRAPLINE_PIN_message_gives_the_pin(string json, bool? on)
    {
        Assert.Equal(on, PageJson.MessageBool(json, "on"));
    }

    [Theory]
    [InlineData("{\"slot\":\"WarehousePos91\",\"on\":false}", "WarehousePos91", false)]
    [InlineData("{\"slot\":\"WarehousePos91\",\"on\":true}", "WarehousePos91", true)]
    [InlineData("{\"on\":true}", null, true)]
    public void A_TRAPLINE_SLOT_OPTION_message_gives_the_slot_and_the_option(string json, string slot, bool on)
    {
        Assert.Equal(slot, PageJson.MessageString(json, "slot"));
        Assert.Equal(on, PageJson.MessageBool(json, "on"));
    }

    [Fact]
    public void TrapPanelJson_holds_the_slot_the_option_and_the_words()
    {
        Assert.Equal("{\"slot\":\"WarehousePos91\",\"on\":false,\"words\":{\"autoReplace\":\"Auto-replace\"}}",
            PageJson.TrapPanelJson("WarehousePos91", false, new Dictionary<string, string> { ["autoReplace"] = "Auto-replace" }));
    }

    [Fact]
    public void MenuJson_holds_the_slot_and_one_line_for_each_kind_with_its_icon_name_and_count()
    {
        string json = PageJson.MenuJson("WarehousePos91", new List<PageJson.MenuLine>
        {
            new PageJson.MenuLine { Kind = 2, Icon = "../../Res/cage.png", Name = "Live Capture Cage", Count = 3 },
            new PageJson.MenuLine { Kind = 4, Icon = "", Name = "Iron \"Animal\" Trap", Count = 2 },
        });

        Assert.Equal("{\"slot\":\"WarehousePos91\",\"lines\":[" +
                     "{\"kind\":2,\"icon\":\"../../Res/cage.png\",\"name\":\"Live Capture Cage\",\"count\":3}," +
                     "{\"kind\":4,\"icon\":\"\",\"name\":\"Iron \\\"Animal\\\" Trap\",\"count\":2}]}", json);
    }

    [Fact]
    public void SetMenuCommand_calls_setMenu_or_gives_no_script()
    {
        Assert.Equal("window.__trapline?window.__trapline.setMenu({}):'no script'", PageJson.SetMenuCommand("{}"));
    }

    [Theory]
    [InlineData("{\"slot\":\"WarehousePos91\",\"trap\":2}", "WarehousePos91", 2L)]
    [InlineData("{\"slot\":\"WarehousePos91\"}", "WarehousePos91", null)]
    public void A_TRAPLINE_SET_message_gives_the_slot_and_the_trap_kind(string json, string slot, long? kind)
    {
        Assert.Equal(slot, PageJson.MessageString(json, "slot"));
        Assert.Equal(kind, PageJson.MessageLong(json, "trap"));
    }

    [Theory]
    [InlineData("{\"trap\":42}", 42L)]
    [InlineData("{\"trap\":8589962897}", 8589962897L)]
    [InlineData("{\"other\":1}", null)]
    [InlineData("{\"trap\":\"42\"}", null)]
    [InlineData("not json", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void MessageLong_reads_a_number_field_of_a_page_message(string json, long? value)
    {
        Assert.Equal(value, PageJson.MessageLong(json, "trap"));
    }

    [Theory]
    [InlineData("{\"slot\":\"WarehousePos91\"}", "WarehousePos91")]
    [InlineData("{\"slot\":7}", null)]
    [InlineData("{}", null)]
    public void MessageString_reads_a_text_field_of_a_page_message(string json, string value)
    {
        Assert.Equal(value, PageJson.MessageString(json, "slot"));
    }

    [Theory]
    [InlineData("{\"on\":true}", true)]
    [InlineData("{\"on\":false}", false)]
    [InlineData("{\"on\":1}", null)]
    [InlineData("{}", null)]
    public void MessageBool_reads_a_true_or_false_field_of_a_page_message(string json, bool? value)
    {
        Assert.Equal(value, PageJson.MessageBool(json, "on"));
    }

    [Theory]
    [InlineData("Home", "\"Home\"")]
    [InlineData("a\"b", "\"a\\\"b\"")]
    [InlineData("a\\b", "\"a\\\\b\"")]
    [InlineData("a\nb\rc\td", "\"a\\nb\\rc\\td\"")]
    [InlineData("a\u0001b", "\"a\\u0001b\"")]
    [InlineData("全部拾取", "\"全部拾取\"")]
    [InlineData(null, "\"\"")]
    public void Str_is_a_quoted_JSON_string(string text, string expected)
    {
        Assert.Equal(expected, JsonText.Str(text));
    }
}
