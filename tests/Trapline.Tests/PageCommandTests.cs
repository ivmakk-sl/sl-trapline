using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using Trapline;
using Xunit;

public class PageCommandTests
{
    private const string Json = "{\"words\":{\"takeAll\":\"Take All\"},\"groups\":null}";
    private const string Script = "window.__trapline = window.__trapline || (function () { return {}; })();";

    [Fact]
    public void The_fast_command_sends_only_the_data_when_the_page_has_the_script()
    {
        string js = PageJson.SetDataCommand(Json);

        Assert.Equal("window.__trapline?window.__trapline.setData(" + Json + "):'no script'", js);
        Assert.Equal("no script", PageJson.NoScript);
    }

    [Fact]
    public void The_check_command_calls_check_or_gives_no_script()
    {
        Assert.Equal("window.__trapline?window.__trapline.check():'no script'", PageJson.CheckCommand);
    }

    [Fact]
    public void The_full_command_is_the_script_then_the_data()
    {
        string js = PageJson.SetDataWithScriptCommand(Script, Json);

        Assert.StartsWith(Script, js);
        Assert.EndsWith(";window.__trapline.setData(" + Json + ");", js);
    }

    [Fact]
    public void No_command_calls_install()
    {
        Assert.DoesNotContain("install(", PageJson.SetDataCommand(Json));
        Assert.DoesNotContain("install(", PageJson.SetDataWithScriptCommand(Script, Json));
        Assert.DoesNotContain("install(", PageJson.CheckCommand);
    }

    [Fact]
    public void DataJson_matches_the_fixture()
    {
        var floors = new List<PageJson.Floor>
        {
            new PageJson.Floor { Id = 1, Label = "Home", Traps = 3, Slots = 3 },
            new PageJson.Floor { Id = 101, Label = "The \"Cellar\"", Traps = 1, Slots = 2 },
        };
        var trapFloor = new Dictionary<long, int> { [11] = 1, [12] = 101, [13] = 1, [14] = 1 };
        var trapCell = new Dictionary<long, int> { [11] = 0, [12] = 1, [13] = 2 };

        string json = PageJson.DataJson("Take All", PageJson.GroupsJson(floors, trapFloor, trapCell));

        var expected = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "data.json")));
        Assert.True(JsonNode.DeepEquals(expected, JsonNode.Parse(json)), json);
    }
}
