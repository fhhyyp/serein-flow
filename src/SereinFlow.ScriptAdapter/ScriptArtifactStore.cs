using System.Text.Encodings.Web;
using System.Text.Json;
using SereinFlow.Domain;
using ScriptLang;
using ScriptLang.Runtime.ByteCode;

namespace SereinFlow.ScriptAdapter;

public sealed record ScriptArtifact(
    string ProjectId,
    string NodeId,
    string SourceHash,
    string ArtifactFingerprint,
    string LanguageVersion,
    string Path);

public sealed record ScriptArtifactBuildResult(
    bool IsSuccess,
    IReadOnlyDictionary<string, ScriptArtifact> Artifacts,
    IReadOnlyList<ScriptExecutionDiagnostic> Diagnostics);

/// <summary>
/// Owns disposable per-project .ssc artifacts. A failed rebuild never leaves
/// an older artifact eligible for execution.
/// 管理每个项目的临时 .ssc 构建缓存；重建失败时不会留下可执行的旧缓存。
/// </summary>
public sealed class ScriptArtifactStore
{
    private readonly string _rootPath;

    public ScriptArtifactStore(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("Artifact root cannot be empty. 缓存根目录不能为空。", nameof(rootPath));
        _rootPath = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(_rootPath);
    }

    public string GetProjectPath(string projectId)
    {
        ValidateProjectId(projectId);
        return Path.Combine(_rootPath, projectId);
    }

    public ScriptArtifactBuildResult RebuildProject(
        string projectId,
        IEnumerable<ScriptNodeDefinition> definitions)
    {
        ValidateProjectId(projectId);
        if (definitions is null)
            throw new ArgumentNullException(nameof(definitions), "Script definitions cannot be null. 脚本定义集合不能为空。");

        var nodes = definitions.ToArray();
        var duplicate = nodes.GroupBy(node => node.NodeId, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            return new(false, new Dictionary<string, ScriptArtifact>(),
                [new("script.duplicate_node", $"Script node '{duplicate.Key}' is defined more than once. 脚本节点“{duplicate.Key}”被重复定义。", NodeId: duplicate.Key)]);
        }

        var projectPath = GetProjectPath(projectId);
        if (Directory.Exists(projectPath))
            Directory.Delete(projectPath, recursive: true);

        var stagingPath = Path.Combine(_rootPath, $".staging-{projectId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingPath);
        var artifacts = new Dictionary<string, ScriptArtifact>(StringComparer.Ordinal);
        var diagnostics = new List<ScriptExecutionDiagnostic>();

        try
        {
            foreach (var node in nodes)
            {
                if (!IsSafeSegment(node.NodeId))
                {
                    diagnostics.Add(new("script.node_id_invalid", $"Script node ID '{node.NodeId}' cannot be used as an artifact name. 脚本节点 ID“{node.NodeId}”不能用作缓存名称。", NodeId: node.NodeId));
                    return new(false, new Dictionary<string, ScriptArtifact>(), diagnostics);
                }
                var engine = new ScriptEngine();
                var sourceName = $"{node.NodeId}.script";
                var chunk = engine.CompileSource(node.Source, sourceName, node.Inputs.Select(input => input.Name));
                var artifactPath = Path.Combine(stagingPath, $"{node.NodeId}.ssc");
                ByteCodeChunk.Save(chunk, artifactPath);
                artifacts[node.NodeId] = new ScriptArtifact(
                    projectId,
                    node.NodeId,
                    node.SourceHash,
                    node.ArtifactFingerprint,
                    node.LanguageVersion,
                    artifactPath);
            }

            var manifest = new ArtifactManifest(projectId, DateTimeOffset.UtcNow, artifacts.Values.Select(ToManifest).ToArray());
            File.WriteAllText(Path.Combine(stagingPath, "manifest.json"), JsonSerializer.Serialize(manifest, JsonOptions));
            Directory.Move(stagingPath, projectPath);

            var published = artifacts.ToDictionary(
                pair => pair.Key,
                pair => pair.Value with { Path = Path.Combine(projectPath, $"{pair.Key}.ssc") },
                StringComparer.Ordinal);
            return new(true, published, diagnostics);
        }
        catch (Exception exception)
        {
            diagnostics.Add(new(
                "script.compile_failed",
                $"Script artifact compilation failed. 脚本缓存编译失败。 {exception.Message}",
                NodeId: nodes.FirstOrDefault()?.NodeId));
            return new(false, new Dictionary<string, ScriptArtifact>(), diagnostics);
        }
        finally
        {
            if (Directory.Exists(stagingPath))
                Directory.Delete(stagingPath, recursive: true);
        }
    }

    public bool TryLoad(
        string projectId,
        ScriptNodeDefinition definition,
        out ByteCodeChunk? chunk,
        out string? artifactPath)
    {
        ValidateProjectId(projectId);
        chunk = null;
        artifactPath = null;
        if (!IsSafeSegment(definition.NodeId))
            return false;
        var path = Path.Combine(GetProjectPath(projectId), $"{definition.NodeId}.ssc");
        if (!File.Exists(path))
            return false;

        try
        {
            var manifestPath = Path.Combine(GetProjectPath(projectId), "manifest.json");
            if (!File.Exists(manifestPath))
                return false;
            var manifest = JsonSerializer.Deserialize<ArtifactManifest>(File.ReadAllText(manifestPath), JsonOptions);
            var entry = manifest?.Artifacts.FirstOrDefault(item => string.Equals(item.NodeId, definition.NodeId, StringComparison.Ordinal));
            if (entry is null
                || !string.Equals(entry.ArtifactFingerprint, definition.ArtifactFingerprint, StringComparison.OrdinalIgnoreCase))
                return false;
            chunk = ByteCodeChunk.Load(path);
            artifactPath = path;
            return true;
        }
        catch
        {
            chunk = null;
            artifactPath = null;
            return false;
        }
    }

    private static ArtifactManifestEntry ToManifest(ScriptArtifact artifact)
        => new(artifact.NodeId, artifact.SourceHash, artifact.ArtifactFingerprint, artifact.LanguageVersion);

    private static void ValidateProjectId(string projectId)
    {
        if (!IsSafeSegment(projectId))
            throw new ArgumentException("Project ID must be a simple directory name. 项目 ID 必须是简单目录名称。", nameof(projectId));
    }

    private static bool IsSafeSegment(string value)
        => !string.IsNullOrWhiteSpace(value)
            && value is not "." and not ".."
            && value == Path.GetFileName(value)
            && !value.Contains(Path.DirectorySeparatorChar)
            && !value.Contains(Path.AltDirectorySeparatorChar)
            && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    private sealed record ArtifactManifest(string ProjectId, DateTimeOffset BuiltAt, IReadOnlyList<ArtifactManifestEntry> Artifacts);
    private sealed record ArtifactManifestEntry(
        string NodeId,
        string SourceHash,
        string ArtifactFingerprint,
        string LanguageVersion);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };
}
