using System.Security.Cryptography;
using System.Text;

namespace SereinFlow.Domain;

public sealed record ScriptValueContract(string Name, string ValueKind, bool Required = false);

public sealed class ScriptNodeDefinition
{
    private ScriptNodeDefinition(
        string nodeId,
        string source,
        string languageVersion,
        string sourceHash,
        IReadOnlyList<ScriptValueContract> inputs,
        IReadOnlyList<ScriptValueContract> outputs)
    {
        NodeId = nodeId;
        Source = source;
        LanguageVersion = languageVersion;
        SourceHash = sourceHash;
        Inputs = inputs;
        Outputs = outputs;
    }

    public string NodeId { get; }

    public string Source { get; }

    public string LanguageVersion { get; }

    public string SourceHash { get; }

    public IReadOnlyList<ScriptValueContract> Inputs { get; }

    public IReadOnlyList<ScriptValueContract> Outputs { get; }

    public static ScriptNodeDefinition Create(
        string nodeId,
        string source,
        string languageVersion,
        string? sourceHash = null,
        IEnumerable<ScriptValueContract>? inputs = null,
        IEnumerable<ScriptValueContract>? outputs = null)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new ArgumentException("Script node ID cannot be empty.", nameof(nodeId));
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("Script source cannot be empty.", nameof(source));
        }

        if (string.IsNullOrWhiteSpace(languageVersion))
        {
            throw new ArgumentException("Script language version cannot be empty.", nameof(languageVersion));
        }

        var computedHash = ComputeSourceHash(source);
        if (sourceHash is not null && !string.Equals(sourceHash, computedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The supplied source hash does not match the source.", nameof(sourceHash));
        }

        return new ScriptNodeDefinition(
            nodeId.Trim(),
            source,
            languageVersion.Trim(),
            computedHash,
            (inputs ?? []).ToArray(),
            (outputs ?? []).ToArray());
    }

    public static string ComputeSourceHash(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
    }
}
