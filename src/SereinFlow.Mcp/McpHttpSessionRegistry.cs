using System.Collections.Concurrent;

namespace SereinFlow.Mcp;

public sealed class McpHttpSessionRegistry
{
    private readonly ConcurrentDictionary<string, string> _sessions = new(StringComparer.Ordinal);

    public string Create(string principalId)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        _sessions[sessionId] = principalId;
        return sessionId;
    }

    public bool Validate(string sessionId, string principalId)
        => !string.IsNullOrWhiteSpace(sessionId)
            && _sessions.TryGetValue(sessionId, out var owner)
            && string.Equals(owner, principalId, StringComparison.Ordinal);

    public bool Remove(string sessionId, string principalId)
        => Validate(sessionId, principalId) && _sessions.TryRemove(sessionId, out _);
}
