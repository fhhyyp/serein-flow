using Microsoft.Extensions.Configuration;

namespace SereinFlow.Infrastructure.Configuration;

public sealed record SereinFlowStorageOptions(
    string DataRoot,
    string DatabasePath,
    string LibraryDirectory,
    string ScriptArtifactRoot,
    string McpPackageStagingDirectory,
    string WorkpieceDirectory)
{
    public SereinFlowStorageOptions(
        string dataRoot,
        string databasePath,
        string libraryDirectory,
        string scriptArtifactRoot,
        string mcpPackageStagingDirectory)
        : this(
            dataRoot,
            databasePath,
            libraryDirectory,
            scriptArtifactRoot,
            mcpPackageStagingDirectory,
            Path.Combine(dataRoot, "workpieces"))
    {
    }

    public static SereinFlowStorageOptions FromConfiguration(
        IConfiguration configuration,
        string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        if (HasLegacyPathSettings(configuration)
            && string.IsNullOrWhiteSpace(configuration["SereinFlow:DataRoot"]))
        {
            throw new InvalidOperationException(
                "Legacy SereinFlow path settings require an explicit SereinFlow:DataRoot migration setting.");
        }

        var dataRoot = ResolveRoot(configuration["SereinFlow:DataRoot"], contentRootPath);
        var databaseFileName = ReadName(configuration["SereinFlow:DatabaseFileName"], "sereinflow.db", "DatabaseFileName");
        var libraryDirectoryName = ReadName(configuration["SereinFlow:LibraryDirectoryName"], "libraries", "LibraryDirectoryName");
        var scriptDirectoryName = ReadName(configuration["SereinFlow:ScriptArtifactDirectoryName"], "script-artifacts", "ScriptArtifactDirectoryName");
        var stagingDirectoryName = ReadName(configuration["SereinFlow:McpStagingDirectoryName"], "mcp-staging", "McpStagingDirectoryName");
        var workpieceDirectoryName = ReadName(configuration["SereinFlow:WorkpieceDirectoryName"], "workpieces", "WorkpieceDirectoryName");

        var databasePath = ResolveChildFile(dataRoot, databaseFileName, "DatabaseFileName");
        var libraryDirectory = ResolveChildDirectory(dataRoot, libraryDirectoryName, "LibraryDirectoryName");
        var scriptArtifactRoot = ResolveChildDirectory(dataRoot, scriptDirectoryName, "ScriptArtifactDirectoryName");
        var stagingDirectory = ResolveChildDirectory(dataRoot, stagingDirectoryName, "McpStagingDirectoryName");
        var workpieceDirectory = ResolveChildDirectory(dataRoot, workpieceDirectoryName, "WorkpieceDirectoryName");

        Directory.CreateDirectory(dataRoot);
        Directory.CreateDirectory(libraryDirectory);
        Directory.CreateDirectory(scriptArtifactRoot);
        Directory.CreateDirectory(stagingDirectory);
        Directory.CreateDirectory(workpieceDirectory);

        return new(
            dataRoot,
            databasePath,
            libraryDirectory,
            scriptArtifactRoot,
            stagingDirectory,
            workpieceDirectory);
    }

    private static bool HasLegacyPathSettings(IConfiguration configuration)
        => !string.IsNullOrWhiteSpace(configuration["SereinFlow:DatabasePath"])
            || !string.IsNullOrWhiteSpace(configuration["SereinFlow:LibraryDirectory"])
            || !string.IsNullOrWhiteSpace(configuration["SereinFlow:ScriptArtifactRoot"])
            || !string.IsNullOrWhiteSpace(configuration["SereinFlow:Mcp:PackageStagingDirectory"]);

    private static string ResolveRoot(string? configuredRoot, string contentRootPath)
    {
        var value = string.IsNullOrWhiteSpace(configuredRoot) ? "data" : configuredRoot.Trim();
        return Path.GetFullPath(Path.IsPathRooted(value)
            ? value
            : Path.Combine(contentRootPath, value));
    }

    private static string ReadName(string? value, string fallback, string fieldName)
    {
        var name = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        if (Path.IsPathRooted(name)
            || name is "." or ".."
            || name.Contains("..", StringComparison.Ordinal)
            || name.Contains(Path.DirectorySeparatorChar)
            || name.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new InvalidOperationException($"SereinFlow:{fieldName} must be a single relative name.");
        }

        return name;
    }

    private static string ResolveChildFile(string root, string name, string fieldName)
    {
        var path = Path.GetFullPath(Path.Combine(root, name));
        EnsureWithinRoot(path, root, fieldName);
        return path;
    }

    private static string ResolveChildDirectory(string root, string name, string fieldName)
    {
        var path = Path.GetFullPath(Path.Combine(root, name));
        EnsureWithinRoot(path, root, fieldName);
        return path;
    }

    private static void EnsureWithinRoot(string path, string root, string fieldName)
    {
        var prefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"SereinFlow:{fieldName} must remain inside SereinFlow:DataRoot.");
        }
    }
}
