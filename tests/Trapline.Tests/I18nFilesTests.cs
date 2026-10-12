using SlShared.I18n;
using Xunit;

// Checks the i18n files of the mod (src/i18n/), embedded in the test assembly as in the mod DLL.
public class I18nFilesTests
{
    private static Dictionary<string, string> Files() => I18nTexts.ReadFiles(typeof(I18nFilesTests).Assembly, "Trapline");

    [Fact]
    public void TheI18nFilesHaveNoProblem()
    {
        Assert.Contains("en.json", Files().Keys);
        Assert.Empty(I18nCheck.Problems(Files(), null));
    }

    [GameContextFact]
    public void EachTextKeyIsAConstantTextOfTheGame()
    {
        Assert.Empty(I18nCheck.Problems(Files(), ConstantTextTsv.Keys()));
    }

    // The words of the Take All button with no game text are the fallback words that the mod had in code before.
    [Fact]
    public void TheTakeAllWordWithNoGameTextIsTheOldFallbackWord()
    {
        var texts = I18nTexts.Load(typeof(I18nFilesTests).Assembly, "Trapline");
        Assert.Equal("Take All", texts.For(1, null)["takeAll"]);
        Assert.Equal("全部拾取", texts.For(0, null)["takeAll"]);
    }

    // With a game text for the key, the button shows the game's own word.
    [Fact]
    public void TheTakeAllWordIsTheGameTextOfItsKey()
    {
        var texts = I18nTexts.Load(typeof(I18nFilesTests).Assembly, "Trapline");
        Assert.Equal("Collect", texts.For(1, key => key == "WebUI_ItemPickup_1" ? "Collect" : "")["takeAll"]);
    }

    [Fact]
    public void ChineseHasEachModTextOfEnglish()
    {
        var english = I18nTexts.Load(typeof(I18nFilesTests).Assembly, "Trapline").For(1, null).ToDictionary();
        var files = Files();
        Assert.Contains("zh.json", files.Keys);
        foreach (string name in english.Keys)
            Assert.Contains("\"" + name + "\"", files["zh.json"]);
    }
}

internal static class ConstantTextTsv
{
    // game/config/views/constant_text.tsv, found by a walk up from the test folder, or null.
    public static readonly string Path = Find();

    // The key column of each row.
    public static HashSet<string> Keys() =>
        new HashSet<string>(File.ReadLines(Path).Skip(1).Select(line => line.Split('\t')[0]));

    private static string Find()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string path = System.IO.Path.Combine(dir.FullName, "game", "config", "views", "constant_text.tsv");
            if (File.Exists(path)) return path;
        }
        return null;
    }
}

internal sealed class GameContextFactAttribute : FactAttribute
{
    public GameContextFactAttribute()
    {
        if (ConstantTextTsv.Path == null) Skip = "The game context (game/config/views/constant_text.tsv) is missing.";
    }
}
