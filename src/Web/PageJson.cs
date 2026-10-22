using System;
using System.Collections.Generic;
using System.Text;
using SlShared.Json;

namespace Trapline
{
    // The JSON text that the plugin sends to page.js, built by hand: netstandard2.1 has no
    // System.Text.Json, and the game's own Newtonsoft.Json is not usable from mod code. No game or
    // BepInEx type here.
    public static class PageJson
    {
        // A floor line or a zone line of the HUD grid: its key (f<floor> or z<floor>:<zone>), its grid row, and its name
        // in the current display language.
        public sealed class Line
        {
            public string Key;
            public bool Floor;
            public int Row;
            public string Name;
        }

        // One cell of the HUD grid: the slot node name of its trap slot, its grid place, the trap on it (0 for a
        // free trap slot), and the durability percent of the durability mark (null for no mark).
        public sealed class Cell
        {
            public string Slot;
            public int Row;
            public int Column;
            public long Trap;
            public int? Mark;
        }

        // The data of setData: the mod texts by name (in name order), the groups (GroupsJson), the Auto-replace
        // checkbox, and the pin of the trap list.
        public static string DataJson(IReadOnlyDictionary<string, string> words, string groupsJson, bool replace, bool pin)
        {
            var sb = new StringBuilder("{\"words\":");
            AppendWords(sb, words);
            return sb.Append(",\"groups\":").Append(groupsJson)
                .Append(",\"replace\":").Append(replace ? "true" : "false")
                .Append(",\"pin\":").Append(pin ? "true" : "false").Append('}').ToString();
        }

        // The mod texts as one JSON object, in name order.
        private static void AppendWords(StringBuilder sb, IReadOnlyDictionary<string, string> words)
        {
            sb.Append('{');
            var names = new List<string>(words.Keys);
            names.Sort(StringComparer.Ordinal);
            for (int i = 0; i < names.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.AppendStr(names[i]).Append(':').AppendStr(words[names[i]]);
            }
            sb.Append('}');
        }

        // The data of the trap window script (setState of trappanel.js): the trap slot of the window, its Auto-replace
        // option, and the mod texts.
        public static string TrapPanelJson(string slot, bool on, IReadOnlyDictionary<string, string> words)
        {
            var sb = new StringBuilder("{\"slot\":");
            sb.AppendStr(slot).Append(",\"on\":").Append(on ? "true" : "false").Append(",\"words\":");
            AppendWords(sb, words);
            return sb.Append('}').ToString();
        }

        // The result of a command when the root page has no page script: a new root page, or one that
        // the game built again after a browser crash.
        public const string NoScript = "no script";

        // The push of the data when the page script is already in the root page.
        public static string SetDataCommand(string json) =>
            "window.__trapline?window.__trapline.setData(" + json + "):'" + NoScript + "'";

        // The check, when the data did not change, that the root page still has the page script and the
        // CoreUI1 frame still has the observer. It answers "ok <runs> <passes>" (the observer runs and the
        // apply passes since the last check), or another text that makes the plugin push again.
        public const string CheckCommand = "window.__trapline?window.__trapline.check():'" + NoScript + "'";

        // The page script, then the push of the data: sent only when a command gave NoScript.
        public static string SetDataWithScriptCommand(string script, string json) =>
            script + ";window.__trapline.setData(" + json + ");";

        // The fields of the JSON data of one page message (an object), or null when it is not one JSON object.
        private static Dictionary<string, object> MessageFields(string json)
        {
            var list = FlatJson.ReadArray("[" + json + "]");
            return list != null && list.Count == 1 ? list[0].Fields : null;
        }

        // A whole number field of a page message, or null when it is missing or not a number.
        public static long? MessageLong(string json, string name) =>
            MessageFields(json) is Dictionary<string, object> f && f.TryGetValue(name, out var v) && v is double d && d == Math.Floor(d) ? (long)d : (long?)null;

        // A text field of a page message, or null when it is missing or not a text.
        public static string MessageString(string json, string name) =>
            MessageFields(json) is Dictionary<string, object> f && f.TryGetValue(name, out var v) ? v as string : null;

        // A true or false field of a page message, or null when it is missing or not true or false.
        public static bool? MessageBool(string json, string name) =>
            MessageFields(json) is Dictionary<string, object> f && f.TryGetValue(name, out var v) && v is bool b ? b : (bool?)null;

        // One line of the Set Trap menu: the trap kind, the icon and the name of its first item, and the count.
        public sealed class MenuLine
        {
            public int Kind;
            public string Icon;
            public string Name;
            public int Count;
        }

        // The push of the Set Trap menu of a free trap slot (one send when the player opens it).
        public static string SetMenuCommand(string json) =>
            "window.__trapline?window.__trapline.setMenu(" + json + "):'" + NoScript + "'";

        // The data of setMenu: the slot and the lines.
        public static string MenuJson(string slot, IReadOnlyList<MenuLine> lines)
        {
            var sb = new StringBuilder();
            sb.Append("{\"slot\":").AppendStr(slot).Append(",\"lines\":[");
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var l = lines[i];
                sb.Append("{\"kind\":").Append(l.Kind)
                    .Append(",\"icon\":").AppendStr(l.Icon)
                    .Append(",\"name\":").AppendStr(l.Name)
                    .Append(",\"count\":").Append(l.Count)
                    .Append('}');
            }
            return sb.Append("]}").ToString();
        }

        // The push of a trap card (one send when the pointer comes onto a trap cell).
        public static string SetCardCommand(string json) =>
            "window.__trapline?window.__trapline.setCard(" + json + "):'" + NoScript + "'";

        // The data of setCard: the trap, the card, and the game's texts of the card.
        public static string CardJson(long trap, CardLogic.Card card, CardLogic.Texts texts)
        {
            var sb = new StringBuilder();
            sb.Append("{\"trap\":").Append(trap)
                .Append(",\"name\":").AppendStr(card.Name)
                .Append(",\"cur\":").Append(card.Current)
                .Append(",\"max\":").Append(card.Max)
                .Append(",\"fill\":").Append(JsonText.Num(card.Fill))
                .Append(",\"band\":").AppendStr(card.Band)
                .Append(",\"bait\":").Append(card.HasBait ? "true" : "false")
                .Append(",\"baitIcon\":").AppendStr(card.BaitIcon)
                .Append(",\"prey\":").Append(card.HasPrey ? "true" : "false")
                .Append(",\"preyIcon\":").AppendStr(card.PreyIcon)
                .Append(",\"texts\":{\"durability\":").AppendStr(texts.Durability)
                .Append(",\"baitSet\":").AppendStr(texts.BaitSet)
                .Append(",\"noBait\":").AppendStr(texts.NoBait)
                .Append(",\"preyReady\":").AppendStr(texts.PreyReady)
                .Append(",\"noPrey\":").AppendStr(texts.NoPrey)
                .Append("}}");
            return sb.ToString();
        }

        // The groups that page.js lays out: the column count, the row count, the floor lines and zone lines, and each
        // cell with its slot and its trap. A trap with no cell is in no cell, and page.js puts it on a line after all rows.
        public static string GroupsJson(int columns, int rows, IReadOnlyList<Line> lines, IReadOnlyList<Cell> cells)
        {
            var sb = new StringBuilder();
            sb.Append("{\"cols\":").Append(columns).Append(",\"rows\":").Append(rows).Append(",\"lines\":[");
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var l = lines[i];
                sb.Append("{\"key\":").AppendStr(l.Key)
                    .Append(",\"kind\":").Append(l.Floor ? "\"floor\"" : "\"zone\"")
                    .Append(",\"row\":").Append(l.Row)
                    .Append(",\"name\":").AppendStr(l.Name)
                    .Append('}');
            }
            sb.Append("],\"cells\":[");
            for (int i = 0; i < cells.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var c = cells[i];
                sb.Append("{\"slot\":").AppendStr(c.Slot)
                    .Append(",\"row\":").Append(c.Row)
                    .Append(",\"col\":").Append(c.Column);
                if (c.Trap != 0) sb.Append(",\"trap\":").Append(c.Trap);
                if (c.Mark.HasValue) sb.Append(",\"mark\":").Append(c.Mark.Value);
                sb.Append('}');
            }
            return sb.Append("]}").ToString();
        }
    }
}
