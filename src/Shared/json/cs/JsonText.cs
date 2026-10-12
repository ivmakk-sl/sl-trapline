// Library copy of shared/json 1.0.0. Do not edit: see src/Shared/json/VERSION.
using System;
using System.Globalization;
using System.Text;

namespace SlShared.Json
{
    // The JSON text of the page data, written by hand: netstandard2.1 has no System.Text.Json, and the game's own
    // Newtonsoft.Json is not usable from mod code. The text goes into ExecuteJavaScript, so each string literal is
    // also a valid JavaScript string literal.
    internal static class JsonText
    {
        private const string Hex = "0123456789abcdef";

        // A JSON string literal, with its quotes. Null gives "".
        public static string Str(string s)
        {
            if (s == null || s.Length == 0) return "\"\"";
            int first = FirstEscape(s);
            if (first < 0) return string.Concat("\"", s, "\"");
            var sb = new StringBuilder(s.Length + 8);
            AppendEscaped(sb, s, first);
            return sb.ToString();
        }

        // The literal of Str, written into the builder of the caller with no string of its own. Returns the builder,
        // so it goes in a chain: sb.Append("{\"name\":").AppendStr(name).
        public static StringBuilder AppendStr(this StringBuilder sb, string s)
        {
            if (s == null || s.Length == 0) return sb.Append("\"\"");
            int first = FirstEscape(s);
            if (first < 0) return sb.Append('"').Append(s).Append('"');
            AppendEscaped(sb, s, first);
            return sb;
        }

        // A whole number in the invariant culture: the current culture can have a minus sign that is not JSON
        // (U+2212 for sv-SE under ICU). StringBuilder.Append(int) takes the current culture too.
        public static string Num(long n) => n.ToString(CultureInfo.InvariantCulture);

        // A whole number, or null for no value.
        public static string Num(long? n) => n.HasValue ? n.Value.ToString(CultureInfo.InvariantCulture) : "null";

        // The same for an int: the int format is faster than the long format.
        public static string Num(int n) => n.ToString(CultureInfo.InvariantCulture);

        public static string Num(int? n) => n.HasValue ? n.Value.ToString(CultureInfo.InvariantCulture) : "null";

        // A number with at most two decimals, no trailing zero, and no exponent, in the invariant culture.
        public static string Num(float x) => ((decimal)Math.Round(x, 2)).ToString("0.##", CultureInfo.InvariantCulture);

        // The index of the first character that needs an escape, or -1.
        private static int FirstEscape(string s)
        {
            for (int i = 0; i < s.Length; i++)
                if (NeedsEscape(s[i])) return i;
            return -1;
        }

        // U+2028 and U+2029 are valid in JSON, but end a line in a script. The compiler reads their C# escape as a
        // line break, also in a char literal, so they are written as (char)0x2028.
        private static bool NeedsEscape(char c) => c < 0x20 || c == '"' || c == '\\' || c == (char)0x2028 || c == (char)0x2029;

        private static void AppendEscaped(StringBuilder sb, string s, int first)
        {
            sb.Append('"').Append(s, 0, first);
            int run = first;
            for (int i = first; i < s.Length; i++)
            {
                char c = s[i];
                if (!NeedsEscape(c)) continue;
                if (i > run) sb.Append(s, run, i - run);
                run = i + 1;
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        sb.Append("\\u").Append(Hex[(c >> 12) & 0xF]).Append(Hex[(c >> 8) & 0xF]).Append(Hex[(c >> 4) & 0xF]).Append(Hex[c & 0xF]);
                        break;
                }
            }
            if (s.Length > run) sb.Append(s, run, s.Length - run);
            sb.Append('"');
        }
    }
}
