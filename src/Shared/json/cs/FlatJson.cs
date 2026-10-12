// Library copy of shared/json 1.0.0. Do not edit: see src/Shared/json/VERSION.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SlShared.Json
{
    // A small JSON reader for the lists that the game sends to its web pages: an array of objects.
    // The plugins target netstandard2.1, which has no JSON library, and a library would add DLL files to the release.
    // It reads the fields of each top-level object in any order. A field value is a string, a double, or a bool.
    // A null or a nested object or array is skipped with the value null, so unknown fields of a game update do no harm.
    internal static class FlatJson
    {
        internal sealed class Obj
        {
            // Index of the '{' in the source text.
            public int Start;
            public Dictionary<string, object> Fields = new Dictionary<string, object>();
            // Index of the closing '"' of each string field, so a caller can insert text at the end of the value.
            // Null for an object with no string field.
            public Dictionary<string, int> StringEnds;
        }

        // Null when the text is not one complete JSON array.
        public static List<Obj> ReadArray(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var reader = new Reader(json);
                var list = reader.Array();
                reader.SkipSpace();
                return reader.AtEnd ? list : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private sealed class Reader
        {
            private readonly string s;
            private int i;

            public Reader(string text) { s = text; }

            public bool AtEnd => i >= s.Length;

            public void SkipSpace()
            {
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            }

            private char Peek()
            {
                SkipSpace();
                if (i >= s.Length) throw new FormatException();
                return s[i];
            }

            private void Expect(char c)
            {
                if (Peek() != c) throw new FormatException();
                i++;
            }

            // True when the next character is c, which is then consumed.
            private bool Take(char c)
            {
                if (Peek() != c) return false;
                i++;
                return true;
            }

            public List<Obj> Array()
            {
                var list = new List<Obj>();
                Expect('[');
                if (Take(']')) return list;
                do
                {
                    if (Peek() == '{') list.Add(Object());
                    else Value();
                } while (Take(','));
                Expect(']');
                return list;
            }

            private Obj Object()
            {
                var obj = new Obj { Start = i };
                Expect('{');
                if (Take('}')) return obj;
                do
                {
                    if (Peek() != '"') throw new FormatException();
                    string name = String();
                    Expect(':');
                    object value = Value();
                    obj.Fields[name] = value;
                    if (value is string)
                    {
                        if (obj.StringEnds == null) obj.StringEnds = new Dictionary<string, int>();
                        obj.StringEnds[name] = i - 1;
                    }
                } while (Take(','));
                Expect('}');
                return obj;
            }

            // A string, a double, or a bool. Null for a JSON null and for a nested object or array.
            private object Value()
            {
                char c = Peek();
                if (c == '"') return String();
                if (c == '{') { Object(); return null; }
                if (c == '[') { Array(); return null; }
                if (Word("true")) return true;
                if (Word("false")) return false;
                if (Word("null")) return null;
                return Number();
            }

            private bool Word(string word)
            {
                if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) return false;
                i += word.Length;
                return true;
            }

            private object Number()
            {
                int start = i;
                while (i < s.Length && "+-.eE0123456789".IndexOf(s[i]) >= 0) i++;
                if (!double.TryParse(s.AsSpan(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                    throw new FormatException();
                return number;
            }

            private string String()
            {
                i++;
                // Most strings have no escape: one Substring and no builder.
                int start = i;
                while (i < s.Length && s[i] != '"' && s[i] != '\\') i++;
                if (i >= s.Length) throw new FormatException();
                if (s[i] == '"') return s.Substring(start, i++ - start);
                var sb = new StringBuilder(s, start, i - start, i - start + 16);
                while (true)
                {
                    if (i >= s.Length) throw new FormatException();
                    char c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (i >= s.Length) throw new FormatException();
                    char e = s[i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            if (i + 4 > s.Length || !ushort.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort code))
                                throw new FormatException();
                            sb.Append((char)code);
                            i += 4;
                            break;
                        default: sb.Append(e); break;
                    }
                }
            }
        }
    }
}
