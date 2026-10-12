// Library copy of shared/i18n 1.0.0. Do not edit: see src/Shared/i18n/VERSION.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SlShared.I18n
{
    // The reader of an i18n file: one flat JSON object of strings. Any other value, a nested value included, makes
    // the file broken. netstandard2.1 has no System.Text.Json, and a shared library does not use another one.
    internal static class I18nJson
    {
        // The name and the text of each field. Throws a FormatException with the position for a broken file.
        public static Dictionary<string, string> Read(string json)
        {
            if (json == null) throw new FormatException("no text");
            var fields = new Dictionary<string, string>();
            int i = 0;
            SkipSpace(json, ref i);
            Expect(json, ref i, '{');
            SkipSpace(json, ref i);
            if (Peek(json, i) == '}')
            {
                i++;
            }
            else
            {
                while (true)
                {
                    SkipSpace(json, ref i);
                    string name = ReadString(json, ref i);
                    SkipSpace(json, ref i);
                    Expect(json, ref i, ':');
                    SkipSpace(json, ref i);
                    if (Peek(json, i) != '"') throw Error(json, i, "a value that is not a string");
                    fields[name] = ReadString(json, ref i);
                    SkipSpace(json, ref i);
                    if (Peek(json, i) == ',') { i++; continue; }
                    Expect(json, ref i, '}');
                    break;
                }
            }
            SkipSpace(json, ref i);
            if (i < json.Length) throw Error(json, i, "text after the object");
            return fields;
        }

        private static char Peek(string s, int i) => i < s.Length ? s[i] : '\0';

        private static void SkipSpace(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\r' || s[i] == '\n' || s[i] == '﻿')) i++;
        }

        private static void Expect(string s, ref int i, char c)
        {
            if (Peek(s, i) != c) throw Error(s, i, "no '" + c + "'");
            i++;
        }

        private static string ReadString(string s, ref int i)
        {
            Expect(s, ref i, '"');
            var sb = new StringBuilder();
            while (true)
            {
                if (i >= s.Length) throw Error(s, i, "an open string");
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c < ' ') throw Error(s, i - 1, "a control character in a string");
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) throw Error(s, i, "an open string");
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length
                            || !ushort.TryParse(s.Substring(i, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort code))
                            throw Error(s, i, "a bad \\u escape");
                        sb.Append((char)code);
                        i += 4;
                        break;
                    default: throw Error(s, i - 1, "a bad escape");
                }
            }
        }

        private static FormatException Error(string s, int i, string what)
        {
            int line = 1;
            for (int k = 0; k < i && k < s.Length; k++) if (s[k] == '\n') line++;
            return new FormatException(what + " at line " + line);
        }
    }
}
