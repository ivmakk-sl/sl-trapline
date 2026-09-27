using System.Collections.Generic;
using System.Text;

namespace Trapline
{
    // The JSON text that the plugin sends to page.js, built by hand: netstandard2.1 has no
    // System.Text.Json, and the game's own Newtonsoft.Json is not usable from mod code. No game or
    // BepInEx type here.
    public static class PageJson
    {
        // One floor line of the HUD grid, with its label in the current display language.
        public sealed class Floor
        {
            public int Id;
            public string Label;
            public int Traps;
            public int Slots;
        }

        // The data of setData: the button word and the floor groups (GroupsJson).
        public static string DataJson(string takeAll, string groupsJson) =>
            "{\"words\":{\"takeAll\":" + Str(takeAll) + "},\"groups\":" + groupsJson + "}";

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

        // The floor groups that page.js lays out: the floors in display order, the floor of each trap,
        // and the cell of each trap on its floor line. A trap with no cell is left out of trapCell, and
        // page.js puts it on a line after all floors.
        public static string GroupsJson(IReadOnlyList<Floor> floors, IEnumerable<KeyValuePair<long, int>> trapFloor, IEnumerable<KeyValuePair<long, int>> trapCell)
        {
            var sb = new StringBuilder();
            sb.Append("{\"floors\":[");
            for (int i = 0; i < floors.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var f = floors[i];
                sb.Append("{\"id\":").Append(f.Id)
                    .Append(",\"label\":").Append(Str(f.Label))
                    .Append(",\"traps\":").Append(f.Traps)
                    .Append(",\"slots\":").Append(f.Slots)
                    .Append('}');
            }
            sb.Append("],\"trapFloor\":");
            AppendMap(sb, trapFloor);
            sb.Append(",\"trapCell\":");
            AppendMap(sb, trapCell);
            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendMap(StringBuilder sb, IEnumerable<KeyValuePair<long, int>> map)
        {
            sb.Append('{');
            bool first = true;
            foreach (var kv in map)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(kv.Key).Append("\":").Append(kv.Value);
            }
            sb.Append('}');
        }

        // A JSON string literal, with its quotes. Also a JavaScript string literal.
        internal static string Str(string s)
        {
            var sb = new StringBuilder((s ?? "").Length + 2);
            sb.Append('"');
            foreach (char c in s ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
