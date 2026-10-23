using System.Collections;
using System.Globalization;
using System.Text.Json;
using ScriptLang.Runtime;

namespace SereinFlow.ScriptAdapter;

public static partial class ScriptValueConverter
{
    /// <summary>
    /// Projects audit data with a shared budget across the complete object graph.
    /// 在整个对象图上共用预算，对审计数据进行有界投影。
    /// </summary>
    public static object? ToBoundedAuditValue(object? value, int maxDepth = 16, int maxItems = 512, int maxTextCharacters = 8192)
    {
        return new AuditBudget(maxDepth, maxItems, maxTextCharacters).Project(value, 0);
    }
    private sealed class AuditBudget(int maximumDepth, int remainingItems, int remainingText)
    {
        public object? Project(object? value, int depth)
        {
            if (depth > maximumDepth)
            {
                return Limit("DepthLimit");
            }

            if (--remainingItems < 0)
            {
                return Limit("ItemLimit");
            }

            if (value is ArrayValue array)
            {
                return ProjectSequence(array.Elements, depth);
            }

            if (value is ObjectValue obj)
            {
                var values = obj.Properties.Select(static pair =>
                {
                    return new KeyValuePair<string, object?>(pair.Key, pair.Value);
                });
                return ProjectPairs(values, depth);
            }

            if (value is Value script)
            {
                var auditValue = ToAuditValue(script);
                return Project(auditValue, depth + 1);
            }

            if (value is string text)
            {
                return ProjectText(text);
            }

            if (value is null or bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal or DateTime or DateTimeOffset or TimeSpan or Guid or Enum)
            {
                return value;
            }

            if (value is JsonElement element)
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.Object:
                        var pairs = element.EnumerateObject().Select(static pair =>
                        {
                            return new KeyValuePair<string, object?>(pair.Name, pair.Value);
                        });
                        return ProjectPairs(pairs, depth);
                    case JsonValueKind.Array:
                        var elements = element.EnumerateArray();
                        return ProjectSequence(elements, depth);
                    case JsonValueKind.String:
                        var textValue = element.GetString() ?? string.Empty;
                        return ProjectText(textValue);
                    default:
                        return element.Clone();
                }
            }

            if (value is IDictionary dictionary)
            {
                var dictionaryEntries = ReadDictionary(dictionary);
                return ProjectPairs(dictionaryEntries, depth);
            }

            if (value is IEnumerable sequence)
            {
                return ProjectSequence(sequence, depth);
            }

            try
            {
                // DTO serialization is capped before materializing a JsonElement.
                // 在物化 JsonElement 前限制 DTO 序列化大小。
                using var stream = new AuditBuffer(64 * 1024);
                JsonSerializer.Serialize(stream, value);
                using var document = JsonDocument.Parse(stream.ToArray());
                return Project(document.RootElement, depth + 1);
            }
            catch
            {
                return new Dictionary<string, object?>
                {
                    ["$kind"] = "ClrValue",
                    ["clrType"] = value.GetType().FullName,
                    ["serializable"] = false
                };
            }
        }

        private object ProjectText(string text)
        {
            if (text.Length <= remainingText)
            {
                remainingText -= text.Length;
                return text;
            }
            var length = Math.Min(Math.Max(remainingText, 0), 256);
            remainingText -= length;
            return new Dictionary<string, object?>
            {
                ["$kind"] = "TextLimit",
                ["length"] = text.Length,
                ["preview"] = text[..length]
            };
        }

        private object ProjectSequence(IEnumerable values, int depth)
        {
            var result = new List<object?>();
            foreach (var value in values)
            {
                if (remainingItems <= 0 || remainingText <= 0)
                {
                    result.Add(Limit("ItemLimit"));
                    break;
                }
                result.Add(Project(value, depth + 1));
            }

            return result;
        }

        private object ProjectPairs(IEnumerable<KeyValuePair<string, object?>> values, int depth)
        {
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var pair in values)
            {
                if (remainingItems <= 0 || pair.Key.Length > remainingText)
                {
                    result["$truncated"] = true;
                    break;
                }
                remainingText -= pair.Key.Length;
                result[pair.Key] = Project(pair.Value, depth + 1);
            }
            return result;
        }

        private static Dictionary<string, object?> Limit(string kind)
        {
            return new()
            {
                ["$kind"] = kind,
                ["truncated"] = true
            };
        }
        private static IEnumerable<KeyValuePair<string, object?>> ReadDictionary(IDictionary dictionary)
        {
            foreach (DictionaryEntry pair in dictionary)
            {
                var key = Convert.ToString(pair.Key, CultureInfo.InvariantCulture) ?? string.Empty;
                yield return new(key, pair.Value);
            }
        }
    }

    /// <summary>
    /// A bounded serialization target used before allocating a full JSON string.
    /// 在分配完整 JSON 字符串之前使用的有界序列化目标。
    /// </summary>
    public sealed class AuditBuffer(int maximumBytes) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureCapacityWithinLimit(count);
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureCapacityWithinLimit(buffer.Length);
            base.Write(buffer);
        }

        private void EnsureCapacityWithinLimit(int count)
        {
            if (Length + count > maximumBytes)
            {
                throw new InvalidDataException("Audit payload byte limit exceeded. 审计载荷超过字节上限。");
            }
        }
    }
}
