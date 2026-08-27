using System.Security.Cryptography;
using System.Text;

namespace SereinFlow.Domain;

public sealed record ScriptValueContract(
    string Name,
    string ValueKind,
    bool Required = false,
    string? Id = null,
    string? Description = null);

public sealed class ScriptNodeDefinition
{
    // Changes to the compiler's global-variable contract must invalidate .ssc
    // files even when the source text itself is unchanged.
    // 编译器全局变量契约发生变化时，即使源代码未变化，也必须使 .ssc 缓存失效。
    public const string CompilerCompatibilityVersion = "serein-scriptlang-0.1.0-sf.3";

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
        ArtifactFingerprint = ComputeArtifactFingerprint(source, languageVersion, inputs);
        Inputs = inputs;
        Outputs = outputs;
    }

    public string NodeId { get; }

    public string Source { get; }

    public string LanguageVersion { get; }

    public string SourceHash { get; }

    public string ArtifactFingerprint { get; }

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

        var inputContracts = (inputs ?? []).ToArray();
        ValidateInputs(inputContracts);

        return new ScriptNodeDefinition(
            nodeId.Trim(),
            source,
            languageVersion.Trim(),
            computedHash,
            inputContracts,
            (outputs ?? []).ToArray());
    }

    public static string ComputeSourceHash(string source)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source), "Script input and output definitions cannot be null. 脚本输入和输出定义不能为空。");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
    }

    public static string ComputeArtifactFingerprint(
        string source,
        string languageVersion,
        IEnumerable<ScriptValueContract> inputs)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(languageVersion);
        ArgumentNullException.ThrowIfNull(inputs);

        var builder = new StringBuilder();
        builder.Append(CompilerCompatibilityVersion).Append('\n')
            .Append(languageVersion.Trim()).Append('\n')
            .Append(source).Append('\n');
        foreach (var input in inputs)
        {
            builder.Append(input.Id?.Trim() ?? input.Name.Trim()).Append('\u001f')
                .Append(input.Name.Trim()).Append('\u001f')
                .Append(input.ValueKind?.Trim() ?? string.Empty).Append('\u001f')
                .Append(input.Required ? '1' : '0').Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    private static void ValidateInputs(IReadOnlyList<ScriptValueContract> inputs)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var input in inputs)
        {
            if (string.IsNullOrWhiteSpace(input.Name))
            {
                throw new ArgumentException("Script input names cannot be empty. 脚本输入名称不能为空。", nameof(inputs));
            }

            var id = input.Id?.Trim() ?? input.Name.Trim();
            if (!ids.Add(id))
            {
                throw new ArgumentException($"Script input ID '{id}' is duplicated. 脚本输入 ID“{id}”重复。", nameof(inputs));
            }

            if (!names.Add(input.Name.Trim()))
            {
                throw new ArgumentException($"Script input name '{input.Name}' is duplicated. 脚本输入名称“{input.Name}”重复。", nameof(inputs));
            }
        }
    }
}
