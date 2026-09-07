using System.Runtime.CompilerServices;
using System.Text.Json;
using SereinFlow.Contracts;

namespace SereinFlow.Client;

internal static class SseEventParser
{
    public static async IAsyncEnumerable<FlowRunEventDto> ReadAsync(
        Stream stream,
        JsonSerializerOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream);
        var data = new List<string>();

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (line.Length == 0)
            {
                var item = ParseEvent(data, options);
                if (item is not null)
                    yield return item;
                data.Clear();
                continue;
            }

            if (line[0] == ':')
                continue;

            var separator = line.IndexOf(':');
            var field = separator < 0 ? line : line[..separator];
            var value = separator < 0 ? string.Empty : line[(separator + 1)..].TrimStart(' ');
            switch (field)
            {
                case "id":
                case "event":
                    break;
                case "data":
                    data.Add(value);
                    break;
            }
        }

        var final = ParseEvent(data, options);
        if (final is not null)
            yield return final;
    }

    private static FlowRunEventDto? ParseEvent(List<string> data, JsonSerializerOptions options)
    {
        if (data.Count == 0)
            return null;
        var json = string.Join('\n', data);
        return JsonSerializer.Deserialize<FlowRunEventDto>(json, options);
    }
}
