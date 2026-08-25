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
            throw new ArgumentException("Script node ID cannot be empty. 脚本节点 ID 不能为空。", nameof(nodeId));
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("Script source cannot be empty. 脚本源代码不能为空。", nameof(source));
        }

        if (string.IsNullOrWhiteSpace(languageVersion))
        {
            throw new ArgumentException("Script language version cannot be empty. 脚本语言版本不能为空。", nameof(languageVersion));
        }

        var computedHash = ComputeSourceHash(source);
        if (sourceHash is not null && !string.Equals(sourceHash, computedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The supplied source hash does not match the source. 提供的源代码哈希与源代码不匹配。", nameof(sourceHash));
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
        if (source is null)
            throw new ArgumentNullException(nameof(source), "Script input and output definitions cannot be null. 脚本输入和输出定义不能为空。");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
    }
}
