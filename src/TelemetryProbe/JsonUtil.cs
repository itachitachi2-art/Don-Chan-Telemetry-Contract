using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DonChan.TelemetryProbe
{
    internal static class JsonUtil
    {
        public static string Serialize(object value)
        {
            var sb = new StringBuilder(1024);
            Write(sb, value);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, object value)
        {
            if (value == null) { sb.Append("null"); return; }
            if (value is string) { Quote(sb, (string)value); return; }
            if (value is char) { Quote(sb, value.ToString()); return; }
            if (value is bool) { sb.Append((bool)value ? "true" : "false"); return; }
            if (value is DateTime) { Quote(sb, ((DateTime)value).ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)); return; }
            if (value is Enum) { Quote(sb, value.ToString()); return; }
            if (IsNumber(value)) { sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture)); return; }

            var generic = value as IDictionary<string, object>;
            if (generic != null)
            {
                sb.Append('{');
                bool first = true;
                foreach (var kv in generic)
                {
                    if (!first) sb.Append(','); first = false;
                    Quote(sb, kv.Key); sb.Append(':'); Write(sb, kv.Value);
                }
                sb.Append('}');
                return;
            }

            var dict = value as IDictionary;
            if (dict != null)
            {
                sb.Append('{');
                bool first = true;
                foreach (DictionaryEntry e in dict)
                {
                    if (!first) sb.Append(','); first = false;
                    Quote(sb, Convert.ToString(e.Key, CultureInfo.InvariantCulture)); sb.Append(':'); Write(sb, e.Value);
                }
                sb.Append('}');
                return;
            }

            var enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                sb.Append('[');
                bool first = true;
                foreach (var item in enumerable)
                {
                    if (!first) sb.Append(','); first = false;
                    Write(sb, item);
                }
                sb.Append(']');
                return;
            }

            Quote(sb, value.ToString());
        }

        private static bool IsNumber(object value)
        {
            switch (Type.GetTypeCode(value.GetType()))
            {
                case TypeCode.Byte: case TypeCode.SByte: case TypeCode.UInt16: case TypeCode.UInt32:
                case TypeCode.UInt64: case TypeCode.Int16: case TypeCode.Int32: case TypeCode.Int64:
                case TypeCode.Decimal: case TypeCode.Double: case TypeCode.Single: return true;
                default: return false;
            }
        }

        private static void Quote(StringBuilder sb, string text)
        {
            sb.Append('"');
            if (text != null)
            {
                foreach (char c in text)
                {
                    switch (c)
                    {
                        case '"': sb.Append("\""); break;
                        case '\': sb.Append("\\"); break;
                        case '': sb.Append("\b"); break;
                        case '': sb.Append("\f"); break;
                        case '
': sb.Append("\n"); break;
                        case '': sb.Append("\r"); break;
                        case '	': sb.Append("\t"); break;
                        default:
                            if (c < 32) sb.Append("\u" + ((int)c).ToString("x4")); else sb.Append(c);
                            break;
                    }
                }
            }
            sb.Append('"');
        }
    }
}
