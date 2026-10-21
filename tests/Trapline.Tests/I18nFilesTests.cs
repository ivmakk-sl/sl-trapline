using SlShared.I18n;
using Xunit;

// Checks the i18n files of the mod (src/i18n/), embedded in the test assembly as in the mod DLL.
public class I18nFilesTests
{
    private static readonly System.Reflection.Assembly Assembly = typeof(I18nFilesTests).Assembly;

    [Fact]
    public void TheI18nFilesHaveNoProblem()
    {
        Assert.Empty(I18nCheck.ModProblems(Assembly, "Trapline", null));
    }

    [GameContextFact]
    public void EachTextKeyIsAConstantTextOfTheGame()
    {
        Assert.Empty(I18nCheck.ModProblems(Assembly, "Trapline", ConstantTextTsv.Keys()));
    }

    // The words of the Take All button with no game text are the fallback words that the mod had in code before.
    [Fact]
    public void TheTakeAllWordWithNoGameTextIsTheOldFallbackWord()
    {
        var texts = I18nTexts.Load(Assembly, "Trapline");
        Assert.Equal("Take All", texts.For(1, null, null)["takeAll"]);
        Assert.Equal("全部拾取", texts.For(0, null, null)["takeAll"]);
    }

    // The card of a free trap slot (design A7) in both languages.
    [Fact]
    public void TheFreeSlotCardTextsAreInBothLanguages()
    {
        var texts = I18nTexts.Load(Assembly, "Trapline");
        Assert.Equal("Free trap slot", texts.For(1, null, null)["freeSlot"]);
        Assert.Equal("Click: set a trap", texts.For(1, null, null)["freeSlotHint"]);
        Assert.Equal("空陷阱位", texts.For(0, null, null)["freeSlot"]);
        Assert.Equal("点击：安装陷阱", texts.For(0, null, null)["freeSlotHint"]);
    }

    // The Auto-replace checkbox and its hover tip in both languages (design A19).
    [Fact]
    public void TheAutoReplaceTextsAreInBothLanguages()
    {
        var texts = I18nTexts.Load(Assembly, "Trapline");
        Assert.Equal("Auto-replace", texts.For(1, null, null)["autoReplace"]);
        Assert.Equal("A broken trap is replaced by a trap of the same kind from the backpack or home storage.", texts.For(1, null, null)["autoReplaceTip"]);
        Assert.Equal("自动补装", texts.For(0, null, null)["autoReplace"]);
        Assert.Equal("陷阱损坏后，从背包或家中储物柜取同类陷阱自动补装。", texts.For(0, null, null)["autoReplaceTip"]);
    }

    // With a game text for the key, the button shows the game's own word.
    [Fact]
    public void TheTakeAllWordIsTheGameTextOfItsKey()
    {
        var texts = I18nTexts.Load(Assembly, "Trapline");
        Assert.Equal("Collect", texts.For(1, null, key => key == "WebUI_ItemPickup_1" ? "Collect" : "")["takeAll"]);
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
