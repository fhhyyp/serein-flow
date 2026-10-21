using System.Collections;
using System.Globalization;
using System.Text.Json;
using ScriptLang.Runtime;

namespace SereinFlow.ScriptAdapter;

public static partial class ScriptValueConverter
{
    /// <summary>Projects audit data with a shared budget across the complete object graph.</summary>
    public static object? ToBoundedAuditValue(object? value, int maxDepth = 16, int maxItems = 512, int maxTextCharacters = 8192)
    {
        object? operationResult1 = new AuditBudget(maxDepth, maxItems, maxTextCharacters).Project(value, 0);
        return operationResult1;
    }
    private sealed class AuditBudget(int maximumDepth, int remainingItems, int remainingText)
    {
        public object? Project(object? value, int depth)
        {
            if (depth > maximumDepth)
            {
                var operationResult1 = Limit("DepthLimit");
                return operationResult1;
            }

            if (--remainingItems < 0)
            {
                var operationResult2 = Limit("ItemLimit");
                return operationResult2;
            }

            if (value is ArrayValue array)
            {
                var operationResult3 = ProjectSequence(array.Elements, depth);
                return operationResult3;
            }

            if (value is ObjectValue obj)
            {
                var operationResult4 = obj.Properties.Select(static pair =>
                {
                    var operationResult2 = new KeyValuePair<string, object?>(pair.Key, pair.Value);
                    return operationResult2;
                });
                var operationResult5 = ProjectPairs(operationResult4, depth);
                return operationResult5;
            }

            if (value is Value script)
            {
                var operationResult6 = ToAuditValue(script);
                var operationResult7 = Project(operationResult6, depth + 1);
                return operationResult7;
            }

            if (value is string text)
            {
                var operationResult8 = ProjectText(text);
                return operationResult8;
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
                            var entry = new KeyValuePair<string, object?>(pair.Name, pair.Value);
                            return entry;
                        });
                        var projectedObject = ProjectPairs(pairs, depth);
                        return projectedObject;
                    case JsonValueKind.Array:
                        var elements = element.EnumerateArray();
                        var projectedArray = ProjectSequence(elements, depth);
                        return projectedArray;
                    case JsonValueKind.String:
                        var textValue = element.GetString() ?? string.Empty;
                        var projectedText = ProjectText(textValue);
                        return projectedText;
                    default:
                        var scalar = element.Clone();
                        return scalar;
                }
            }

            if (value is IDictionary dictionary)
            {
                var operationResult12 = ReadDictionary(dictionary);
                var operationResult13 = ProjectPairs(operationResult12, depth);
                return operationResult13;
            }

            if (value is IEnumerable sequence)
            {
                var operationResult14 = ProjectSequence(sequence, depth);
                return operationResult14;
            }

            try
            {
                // DTO serialization is capped before materializing a JsonElement.
                using var stream = new AuditBuffer(64 * 1024);
                JsonSerializer.Serialize(stream, value);
                var operationResult15 = stream.ToArray();
                using var document = JsonDocument.Parse(operationResult15);
                var operationResult5 = Project(document.RootElement, depth + 1);
                return operationResult5;
            }
            catch
            {
                var operationResult6 = new Dictionary<string, object?>
                {
                    ["$kind"] = "ClrValue",
                    ["clrType"] = value.GetType().FullName,
                    ["serializable"] = false
                };
                return operationResult6;
            }
        }

        private object ProjectText(string text)
        {
            if (text.Length <= remainingText)
            {
                remainingText -= text.Length;
                return text;
            }

            var operationResult16 = Math.Max(remainingText, 0);
            var length = Math.Min(operationResult16, 256);
            remainingText -= length;
            var operationResult7 = new Dictionary<string, object?>
            {
                ["$kind"] = "TextLimit",
                ["length"] = text.Length,
                ["preview"] = text[..length]
            };
            return operationResult7;
        }

        private object ProjectSequence(IEnumerable values, int depth)
        {
            var result = new List<object?>();
            foreach (var value in values)
            {
                if (remainingItems <= 0 || remainingText <= 0)
                {
                    var operationResult17 = Limit("ItemLimit");
                    result.Add(operationResult17);
                    break;
                }

                var operationResult18 = Project(value, depth + 1);
                result.Add(operationResult18);
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
            Dictionary<string, object?> operationResult8 = new()
            {
                ["$kind"] = kind,
                ["truncated"] = true
            };
            return operationResult8;
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

    /// <summary>A bounded serialization target used before allocating a full JSON string.</summary>
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
