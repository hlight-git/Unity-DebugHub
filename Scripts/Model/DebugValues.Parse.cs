// Mang từ IngameDebugConsole (Plugins/IngameDebugConsole/Scripts/DebugLogConsole.cs), đã cắt phần hub không dùng.
//
// The MIT License (MIT)
//
// Copyright (c) 2016 Süleyman Yasir KULA
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Hlight.Debug.Hub
{
    internal static partial class DebugValues
    {
        private delegate bool ParseFunction(string input, out object output);

        /// Bảng parse duy nhất của hub: kiểu có ở đây là "parse được" (CanParse / IsInlineValue đọc thẳng
        /// bảng này, không còn bản chép tay).
        private static readonly Dictionary<Type, ParseFunction> Parsers = new()
        {
            { typeof(string), ParseString },
            { typeof(bool), ParseBool },
            { typeof(int), ParseInt },
            { typeof(uint), ParseUInt },
            { typeof(long), ParseLong },
            { typeof(ulong), ParseULong },
            { typeof(byte), ParseByte },
            { typeof(sbyte), ParseSByte },
            { typeof(short), ParseShort },
            { typeof(ushort), ParseUShort },
            { typeof(char), ParseChar },
            { typeof(float), ParseFloat },
            { typeof(double), ParseDouble },
            { typeof(decimal), ParseDecimal },
            { typeof(Vector2), (string s, out object o) => ParseVector(s, typeof(Vector2), out o) },
            { typeof(Vector3), (string s, out object o) => ParseVector(s, typeof(Vector3), out o) },
            { typeof(Vector4), (string s, out object o) => ParseVector(s, typeof(Vector4), out o) },
            { typeof(Quaternion), (string s, out object o) => ParseVector(s, typeof(Quaternion), out o) },
            { typeof(Color), (string s, out object o) => ParseVector(s, typeof(Color), out o) },
            { typeof(Color32), (string s, out object o) => ParseVector(s, typeof(Color32), out o) },
            { typeof(Rect), (string s, out object o) => ParseVector(s, typeof(Rect), out o) },
            { typeof(RectOffset), (string s, out object o) => ParseVector(s, typeof(RectOffset), out o) },
            { typeof(Bounds), (string s, out object o) => ParseVector(s, typeof(Bounds), out o) },
            { typeof(GameObject), ParseGameObject },
            { typeof(Vector2Int), (string s, out object o) => ParseVector(s, typeof(Vector2Int), out o) },
            { typeof(Vector3Int), (string s, out object o) => ParseVector(s, typeof(Vector3Int), out o) },
            { typeof(RectInt), (string s, out object o) => ParseVector(s, typeof(RectInt), out o) },
            { typeof(BoundsInt), (string s, out object o) => ParseVector(s, typeof(BoundsInt), out o) },
        };

        private static readonly Dictionary<Type, string> ReadableNames = new()
        {
            { typeof(string), "String" },
            { typeof(bool), "Boolean" },
            { typeof(int), "Integer" },
            { typeof(uint), "Unsigned Integer" },
            { typeof(long), "Long" },
            { typeof(ulong), "Unsigned Long" },
            { typeof(byte), "Byte" },
            { typeof(sbyte), "Short Byte" },
            { typeof(short), "Short" },
            { typeof(ushort), "Unsigned Short" },
            { typeof(char), "Char" },
            { typeof(float), "Float" },
            { typeof(double), "Double" },
            { typeof(decimal), "Decimal" },
        };

        /// Nhóm bao một đối số: nháy và ngoặc. Ngoặc lồng được (`[[1 2] [3 4]]` cho mảng Vector2).
        private static readonly string[] Delimiters = { "\"\"", "''", "{}", "()", "[]" };

        /// Tách dòng lệnh thành đối số: cách bằng khoảng trắng, nhóm trong nháy/ngoặc là một đối số (bỏ vỏ),
        /// dấu phẩy ngay sau đối số bị bỏ.
        internal static void SplitArguments(string command, List<string> into)
        {
            for (var i = 0; i < command.Length; i++)
            {
                if (char.IsWhiteSpace(command[i])) continue;

                var delimiter = IndexOfDelimiterGroup(command[i]);
                if (delimiter >= 0)
                {
                    var end = IndexOfDelimiterGroupEnd(command, delimiter, i + 1);
                    into.Add(command.Substring(i + 1, end - i - 1));
                    i = end < command.Length - 1 && command[end + 1] == ',' ? end + 1 : end;
                }
                else
                {
                    var end = IndexOfChar(command, ' ', i + 1);
                    into.Add(command.Substring(i, command[end - 1] == ',' ? end - 1 - i : end - i));
                    i = end;
                }
            }
        }

        internal static bool ParseArgument(string input, Type type, out object output)
        {
            if (Parsers.TryGetValue(type, out var parse)) return parse(input, out output);
            if (typeof(Component).IsAssignableFrom(type)) return ParseComponent(input, type, out output);
            if (type.IsEnum) return ParseEnum(input, type, out output);
            if (IsSupportedCollection(type)) return ParseCollection(input, type, out output);
            output = null;
            return false;
        }

        /// "Integer", "Float[]"… — placeholder ô nhập và trang Help.
        internal static string ReadableName(Type type)
        {
            if (ReadableNames.TryGetValue(type, out var name)) return name;
            if (IsSupportedCollection(type))
            {
                var element = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
                return (ReadableNames.TryGetValue(element, out var elementName) ? elementName : element.Name) + "[]";
            }
            return type.Name;
        }

        private static bool IsSupportedCollection(Type type)
        {
            if (type.IsArray)
            {
                if (type.GetArrayRank() != 1) return false;
                type = type.GetElementType();
            }
            else if (type.IsGenericType)
            {
                if (type.GetGenericTypeDefinition() != typeof(List<>)) return false;
                type = type.GetGenericArguments()[0];
            }
            else return false;

            return Parsers.ContainsKey(type) || typeof(Component).IsAssignableFrom(type) || type.IsEnum;
        }

        private static int IndexOfDelimiterGroup(char c)
        {
            for (var i = 0; i < Delimiters.Length; i++)
            {
                if (c == Delimiters[i][0]) return i;
            }
            return -1;
        }

        private static int IndexOfDelimiterGroupEnd(string command, int delimiter, int start)
        {
            var open = Delimiters[delimiter][0];
            var close = Delimiters[delimiter][1];
            var depth = 1;
            for (var i = start; i < command.Length; i++)
            {
                var c = command[i];
                if (c == close && --depth <= 0) return i;
                if (c == open) depth++;
            }
            return command.Length;
        }

        private static int IndexOfChar(string command, char c, int start)
        {
            var index = command.IndexOf(c, start);
            return index < 0 ? command.Length : index;
        }

        private static bool ParseString(string input, out object output)
        {
            output = input;
            return true;
        }

        private static bool ParseBool(string input, out object output)
        {
            if (input == "1" || input.ToLowerInvariant() == "true")
            {
                output = true;
                return true;
            }
            if (input == "0" || input.ToLowerInvariant() == "false")
            {
                output = false;
                return true;
            }
            output = false;
            return false;
        }

        private static bool ParseInt(string input, out object output)
        {
            var ok = int.TryParse(input, out var value);
            output = value;
            return ok;
        }

        private static bool ParseUInt(string input, out object output)
        {
            var ok = uint.TryParse(input, out var value);
            output = value;
            return ok;
        }

        private static bool ParseLong(string input, out object output)
        {
            var ok = long.TryParse(TrimSuffix(input, "L"), out var value);
            output = value;
            return ok;
        }

        private static bool ParseULong(string input, out object output)
        {
            var ok = ulong.TryParse(TrimSuffix(input, "L"), out var value);
            output = value;
            return ok;
        }

        private static bool ParseByte(string input, out object output)
        {
            var ok = byte.TryParse(input, out var value);
            output = value;
            return ok;
        }

        private static bool ParseSByte(string input, out object output)
        {
            var ok = sbyte.TryParse(input, out var value);
            output = value;
            return ok;
        }

        private static bool ParseShort(string input, out object output)
        {
            var ok = short.TryParse(input, out var value);
            output = value;
            return ok;
        }

        private static bool ParseUShort(string input, out object output)
        {
            var ok = ushort.TryParse(input, out var value);
            output = value;
            return ok;
        }

        private static bool ParseChar(string input, out object output)
        {
            var ok = char.TryParse(input, out var value);
            output = value;
            return ok;
        }

        private static bool ParseFloat(string input, out object output)
        {
            var ok = float.TryParse(TrimSuffix(input, "f"), NumberStyles.Float, CultureInfo.InvariantCulture, out var value);
            output = value;
            return ok;
        }

        private static bool ParseDouble(string input, out object output)
        {
            var ok = double.TryParse(TrimSuffix(input, "f"), NumberStyles.Float, CultureInfo.InvariantCulture, out var value);
            output = value;
            return ok;
        }

        private static bool ParseDecimal(string input, out object output)
        {
            var ok = decimal.TryParse(TrimSuffix(input, "f"), NumberStyles.Float, CultureInfo.InvariantCulture, out var value);
            output = value;
            return ok;
        }

        private static string TrimSuffix(string input, string suffix) =>
            input.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? input.Substring(0, input.Length - 1) : input;

        /// Trả true kể cả khi không tìm thấy (output null) — TryParse phía trên mới từ chối null cho kiểu Unity.
        private static bool ParseGameObject(string input, out object output)
        {
            output = input == "null" ? null : GameObject.Find(input);
            return true;
        }

        private static bool ParseComponent(string input, Type type, out object output)
        {
            var gameObject = input == "null" ? null : GameObject.Find(input);
            output = gameObject ? gameObject.GetComponent(type) : null;
            return true;
        }

        /// Tên hoặc số, nối bằng `|` (OR) / `&` (AND), không phân biệt hoa thường.
        private static bool ParseEnum(string input, Type type, out object output)
        {
            const int NONE = 0, OR = 1, AND = 2;
            var result = 0;
            var operation = NONE;
            for (var i = 0; i < input.Length; i++)
            {
                var orIndex = input.IndexOf('|', i);
                var andIndex = input.IndexOf('&', i);
                var token = orIndex < 0
                    ? input.Substring(i, (andIndex < 0 ? input.Length : andIndex) - i).Trim()
                    : input.Substring(i, (andIndex < 0 ? orIndex : Mathf.Min(andIndex, orIndex)) - i).Trim();

                if (!int.TryParse(token, out var value))
                {
                    try
                    {
                        value = Convert.ToInt32(Enum.Parse(type, token, true));
                    }
                    catch
                    {
                        output = null;
                        return false;
                    }
                }

                if (operation == NONE) result = value;
                else if (operation == OR) result |= value;
                else result &= value;

                if (orIndex >= 0)
                {
                    if (andIndex > orIndex)
                    {
                        operation = AND;
                        i = andIndex;
                    }
                    else
                    {
                        operation = OR;
                        i = orIndex;
                    }
                }
                else if (andIndex >= 0)
                {
                    operation = AND;
                    i = andIndex;
                }
                else i = input.Length;
            }

            output = Enum.ToObject(type, result);
            return true;
        }

        private static bool ParseCollection(string input, Type type, out object output)
        {
            var values = new List<string>(2);
            SplitArguments(input, values);

            var result = (IList)Activator.CreateInstance(type, new object[] { values.Count });
            output = result;
            var element = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
            for (var i = 0; i < values.Count; i++)
            {
                if (!ParseArgument(values[i], element, out var item)) return false;
                if (type.IsArray) result[i] = item;
                else result.Add(item);
            }
            return true;
        }

        /// Thiếu thành phần thì điền 0 (Color: đen, Quaternion: identity), thừa thì bỏ.
        private static bool ParseVector(string input, Type type, out object output)
        {
            var tokens = new List<string>(input.Replace(',', ' ').Trim().Split(' '));
            for (var i = tokens.Count - 1; i >= 0; i--)
            {
                tokens[i] = tokens[i].Trim();
                if (tokens[i].Length == 0) tokens.RemoveAt(i);
            }

            var v = new float[tokens.Count];
            for (var i = 0; i < tokens.Count; i++)
            {
                if (!ParseFloat(tokens[i], out var parsed))
                {
                    output = type == typeof(Vector3) ? Vector3.zero : type == typeof(Vector2) ? Vector2.zero : (object)Vector4.zero;
                    return false;
                }
                v[i] = (float)parsed;
            }

            float At(int i) => i < v.Length ? v[i] : 0f;
            int Round(int i) => Mathf.RoundToInt(At(i));

            if (type == typeof(Vector2)) output = new Vector2(At(0), At(1));
            else if (type == typeof(Vector3)) output = new Vector3(At(0), At(1), At(2));
            else if (type == typeof(Vector4)) output = new Vector4(At(0), At(1), At(2), At(3));
            else if (type == typeof(Quaternion))
            {
                var q = Quaternion.identity;
                for (var i = 0; i < v.Length && i < 4; i++) q[i] = v[i];
                output = q;
            }
            else if (type == typeof(Color))
            {
                var c = Color.black;
                for (var i = 0; i < v.Length && i < 4; i++) c[i] = v[i];
                output = c;
            }
            else if (type == typeof(Color32))
            {
                var c = new Color32(0, 0, 0, 255);
                if (v.Length > 0) c.r = (byte)Round(0);
                if (v.Length > 1) c.g = (byte)Round(1);
                if (v.Length > 2) c.b = (byte)Round(2);
                if (v.Length > 3) c.a = (byte)Round(3);
                output = c;
            }
            else if (type == typeof(Rect)) output = new Rect(At(0), At(1), At(2), At(3));
            else if (type == typeof(RectOffset)) output = new RectOffset(Round(0), Round(1), Round(2), Round(3));
            else if (type == typeof(Bounds)) output = new Bounds(new Vector3(At(0), At(1), At(2)), new Vector3(At(3), At(4), At(5)));
            else if (type == typeof(Vector2Int)) output = new Vector2Int(Round(0), Round(1));
            else if (type == typeof(Vector3Int)) output = new Vector3Int(Round(0), Round(1), Round(2));
            else if (type == typeof(RectInt)) output = new RectInt(Round(0), Round(1), Round(2), Round(3));
            else if (type == typeof(BoundsInt))
                output = new BoundsInt(new Vector3Int(Round(0), Round(1), Round(2)), new Vector3Int(Round(3), Round(4), Round(5)));
            else
            {
                output = null;
                return false;
            }
            return true;
        }
    }
}
