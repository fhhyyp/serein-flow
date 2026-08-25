using System.Text.RegularExpressions;

namespace SereinFlow.Domain;

public sealed class PluginManifest
{
    private static readonly Regex Sha256Pattern = new("^[0-9a-fA-F]{64}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private PluginManifest(
        string assemblyName,
        string assemblyVersion,
        string sha256,
        IReadOnlyList<string> targetRids,
        string apiVersion,
        IReadOnlyList<string> nodeTypes)
    {
        AssemblyName = assemblyName;
        AssemblyVersion = assemblyVersion;
        Sha256 = sha256.ToLowerInvariant();
        TargetRids = targetRids;
        ApiVersion = apiVersion;
        NodeTypes = nodeTypes;
    }

    public string AssemblyName { get; }

    public string AssemblyVersion { get; }

    public string Sha256 { get; }

    public IReadOnlyList<string> TargetRids { get; }

    public string ApiVersion { get; }

    public IReadOnlyList<string> NodeTypes { get; }

    public static PluginManifest Create(
        string assemblyName,
        string assemblyVersion,
        string sha256,
        IEnumerable<string> targetRids,
        string apiVersion,
        IEnumerable<string> nodeTypes)
    {
        if (targetRids is null)
            throw new ArgumentNullException(nameof(targetRids), "Target runtime identifiers cannot be null. 目标运行时标识集合不能为空。");
        if (nodeTypes is null)
            throw new ArgumentNullException(nameof(nodeTypes), "Node types cannot be null. 节点类型集合不能为空。");

        if (string.IsNullOrWhiteSpace(assemblyName) || assemblyName.IndexOfAny(['/', '\\']) >= 0)
        {
            throw new ArgumentException("Assembly name must be a simple name, not a path. 程序集名称必须是简单名称，不能是路径。", nameof(assemblyName));
        }

        if (string.IsNullOrWhiteSpace(assemblyVersion))
        {
            throw new ArgumentException("Assembly version cannot be empty. 程序集版本不能为空。", nameof(assemblyVersion));
        }

        var normalizedHash = sha256 ?? string.Empty;
        if (!Sha256Pattern.IsMatch(normalizedHash))
        {
            throw new ArgumentException("Plugin SHA-256 must be 64 hexadecimal characters. 插件 SHA-256 必须是 64 位十六进制字符。", nameof(sha256));
        }

        if (string.IsNullOrWhiteSpace(apiVersion))
        {
            throw new ArgumentException("Plugin API version cannot be empty. 插件 API 版本不能为空。", nameof(apiVersion));
        }

        return new PluginManifest(
            assemblyName.Trim(),
            assemblyVersion.Trim(),
            normalizedHash,
            targetRids.Where(static rid => !string.IsNullOrWhiteSpace(rid)).Select(static rid => rid.Trim()).Distinct(StringComparer.Ordinal).ToArray(),
            apiVersion.Trim(),
            nodeTypes.Where(static nodeType => !string.IsNullOrWhiteSpace(nodeType)).Select(static nodeType => nodeType.Trim()).Distinct(StringComparer.Ordinal).ToArray());
    }
}
