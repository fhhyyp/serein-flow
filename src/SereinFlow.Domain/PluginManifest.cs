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
        ArgumentNullException.ThrowIfNull(targetRids);
        ArgumentNullException.ThrowIfNull(nodeTypes);

        if (string.IsNullOrWhiteSpace(assemblyName) || assemblyName.IndexOfAny(['/', '\\']) >= 0)
        {
            throw new ArgumentException("Assembly name must be a simple name, not a path.", nameof(assemblyName));
        }

        if (string.IsNullOrWhiteSpace(assemblyVersion))
        {
            throw new ArgumentException("Assembly version cannot be empty.", nameof(assemblyVersion));
        }

        var normalizedHash = sha256 ?? string.Empty;
        if (!Sha256Pattern.IsMatch(normalizedHash))
        {
            throw new ArgumentException("Plugin SHA-256 must be 64 hexadecimal characters.", nameof(sha256));
        }

        if (string.IsNullOrWhiteSpace(apiVersion))
        {
            throw new ArgumentException("Plugin API version cannot be empty.", nameof(apiVersion));
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
