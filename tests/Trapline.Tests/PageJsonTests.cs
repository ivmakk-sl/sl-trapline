using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using Trapline;
using Xunit;

public class PageJsonTests
{
    // The groups of tests/fixtures/groups.json: two floors, a floor label with a quote, and trap 14
    // with a floor but no cell.
    private static List<PageJson.Floor> FixtureFloors() => new List<PageJson.Floor>
    {
        new PageJson.Floor { Id = 1, Label = "Home", Traps = 3, Slots = 3 },
        new PageJson.Floor { Id = 101, Label = "The \"Cellar\"", Traps = 1, Slots = 2 },
    };

    private static Dictionary<long, int> FixtureTrapFloor() => new Dictionary<long, int> { [11] = 1, [12] = 101, [13] = 1, [14] = 1 };

    private static Dictionary<long, int> FixtureTrapCell() => new Dictionary<long, int> { [11] = 0, [12] = 1, [13] = 2 };

    [Fact]
    public void GroupsJson_matches_the_fixture()
    {
        string json = PageJson.GroupsJson(FixtureFloors(), FixtureTrapFloor(), FixtureTrapCell());

        var expected = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "groups.json")));
        Assert.True(JsonNode.DeepEquals(expected, JsonNode.Parse(json)), json);
    }

    [Fact]
    public void GroupsJson_with_no_floor_and_no_trap_is_empty_groups()
    {
        Assert.Equal("{\"floors\":[],\"trapFloor\":{},\"trapCell\":{}}",
            PageJson.GroupsJson(new List<PageJson.Floor>(), new Dictionary<long, int>(), new Dictionary<long, int>()));
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
        Assert.Equal(expected, PageJson.Str(text));
    }
}
