using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using SereinFlow.Domain;
using ScriptLang;
using ScriptLang.Runtime.ByteCode;
using SereinFlow.Contracts;

namespace SereinFlow.ScriptAdapter;

/// <summary>
/// Represents script artifact data used by SereinFlow.
/// 表示 SereinFlow 使用的脚本制品数据。
/// </summary>
public sealed record ScriptArtifact(string ProjectId, string NodeId, string SourceHash, string ArtifactFingerprint, string LanguageVersion, string Path);
/// <summary>
/// Captures the outcome of script artifact build operations.
/// 记录脚本制品Build操作的结果。
/// </summary>
public sealed record ScriptArtifactBuildResult(bool IsSuccess, IReadOnlyDictionary<string, ScriptArtifact> Artifacts, IReadOnlyList<ScriptExecutionDiagnostic> Diagnostics);

/// <summary>
/// Content-addressed artifacts: only bytecode is shared, never engines, globals or tasks.
/// 按指纹发布不可变制品；只缓存字节码，不共享引擎、全局变量或执行任务。
/// </summary>
public sealed class ScriptArtifactStore
{
    private static readonly JsonSerializerOptions JsonOptions = SereinJsonSerialization.CreateWebOptions();
    private readonly string _rootPath;
    private readonly ConcurrentDictionary<string, Lazy<ByteCodeChunk>> _chunks = new(StringComparer.Ordinal);

    public ScriptArtifactStore(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        _rootPath = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(_rootPath);
    }

    public string GetProjectPath(string projectId)
    {
        ValidateSegment(projectId);
        var operationResult1 = Path.Combine(_rootPath, projectId);
        return operationResult1;
    }

    public ScriptArtifactBuildResult RebuildProject(string projectId, IEnumerable<ScriptNodeDefinition> definitions)
    {
        ValidateSegment(projectId);
        ArgumentNullException.ThrowIfNull(definitions);
        var nodes = definitions.ToArray();
        var artifacts = new Dictionary<string, ScriptArtifact>(StringComparer.Ordinal);
        if (nodes.Select(static node => node.NodeId).Distinct(StringComparer.Ordinal).Count() != nodes.Length)
        {
            ScriptExecutionDiagnostic[] operationResult1 = [new(ScriptErrorCodes.DuplicateNode, "Duplicate script node. 脚本节点重复。")];
            ScriptArtifactBuildResult operationResult2 = new(false, artifacts, operationResult1);
            return operationResult2;
        }

        foreach (var node in nodes)
        {
            try
            {
                ValidateSegment(node.NodeId);
            }
            catch (ArgumentException exception)
            {
                ScriptExecutionDiagnostic[] operationResult3 = [new(ScriptErrorCodes.NodeIdInvalid, exception.Message, NodeId: node.NodeId)];
                ScriptArtifactBuildResult operationResult2 = new(false, artifacts, operationResult3);
                return operationResult2;
            }

            try
            {
                var path = GetArtifactPath(projectId, node);
                if (!TryLoad(projectId, node, out _, out _))
                {
                    BuildArtifact(projectId, node, path);
                }

                artifacts[node.NodeId] = new(projectId, node.NodeId, node.SourceHash, node.ArtifactFingerprint, node.LanguageVersion, path);
            }
            catch (Exception exception)
            {
                ScriptExecutionDiagnostic[] diagnostics = [new(ScriptErrorCodes.CompileFailed, $"Script artifact compilation failed. 脚本缓存编译失败。 {exception.Message}", NodeId: node.NodeId)];
                ScriptArtifactBuildResult operationResult3 = new(false, artifacts, diagnostics);
                return operationResult3;
            }
        }

        ScriptArtifactBuildResult operationResult4 = new(true, artifacts, []);
        return operationResult4;
    }

    public bool TryLoad(string projectId, ScriptNodeDefinition definition, out ByteCodeChunk? chunk, out string? artifactPath)
    {
        ValidateSegment(projectId);
        chunk = null;
        artifactPath = null;
        string path;
        try
        {
            path = GetArtifactPath(projectId, definition);
        }
        catch (ArgumentException)
        {
            return false;
        }

        var cached = _chunks.GetOrAdd(path, _ =>
        {
            var operationResult6 = new Lazy<ByteCodeChunk>(() =>
            {
                var artifactDirectory = Path.GetDirectoryName(path)!;
                var manifestPath = Path.Combine(artifactDirectory, "manifest.json");
                var manifestJson = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<ArtifactManifest>(manifestJson, JsonOptions);
                if (manifest is null || manifest.ProjectId != projectId || manifest.NodeId != definition.NodeId || manifest.ArtifactFingerprint != definition.ArtifactFingerprint || manifest.SourceHash != definition.SourceHash || manifest.LanguageVersion != definition.LanguageVersion)
                {
                    throw new InvalidDataException("Script artifact manifest mismatch. 脚本制品清单与定义不匹配。");
                }

                var operationResult5 = ByteCodeChunk.Load(path);
                return operationResult5;
            }, LazyThreadSafetyMode.ExecutionAndPublication);
            return operationResult6;
        });
        try
        {
            chunk = cached.Value;
            artifactPath = path;
            return true;
        }
        catch
        {
            var operationResult6 = new KeyValuePair<string, Lazy<ByteCodeChunk>>(path, cached);
            _chunks.TryRemove(operationResult6);
            return false;
        }
    }

    private string GetArtifactPath(string projectId, ScriptNodeDefinition node)
    {
        ValidateSegment(node.NodeId);
        ValidateSegment(node.ArtifactFingerprint);
        var projectPath = GetProjectPath(projectId);
        var artifactPath = Path.Combine(projectPath, node.NodeId, node.ArtifactFingerprint, "node.ssc");
        return artifactPath;
    }

    private void BuildArtifact(string projectId, ScriptNodeDefinition node, string path)
    {
        var artifactDirectory = Path.GetDirectoryName(path)!;
        var nodeDirectory = Path.GetDirectoryName(artifactDirectory)!;
        Directory.CreateDirectory(nodeDirectory);
        var operationResult12 = Path.Combine(nodeDirectory, node.ArtifactFingerprint + ".lock");
        // Deduplicate compilation across Worker processes as well as threads.
        using var buildLock = AcquireBuildLock(operationResult12);
        if (TryLoad(projectId, node, out _, out _))
        {
            return;
        }

        var operationResult13 = ".staging-" + Guid.NewGuid().ToString("N");
        var staging = Path.Combine(nodeDirectory, operationResult13);
        Directory.CreateDirectory(staging);
        try
        {
            var engine = new ScriptEngine();
            var operationResult8 = node.Inputs.Select(static input => input.Name);
            var chunk = engine.CompileSource(node.Source, $"{node.NodeId}.script", operationResult8);
            var operationResult9 = Path.Combine(staging, "node.ssc");
            ByteCodeChunk.Save(chunk, operationResult9);
            var manifest = new ArtifactManifest(projectId, node.NodeId, node.SourceHash, node.ArtifactFingerprint, node.LanguageVersion);
            var operationResult10 = Path.Combine(staging, "manifest.json");
            var operationResult11 = JsonSerializer.Serialize(manifest, JsonOptions);
            File.WriteAllText(operationResult10, operationResult11);
            // Only an unreadable/corrupt fingerprint reaches this repair path.
            if (Directory.Exists(artifactDirectory))
            {
                DeleteOwnedDirectory(artifactDirectory);
            }

            Directory.Move(staging, artifactDirectory);
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                DeleteOwnedDirectory(staging);
            }
        }
    }

    private static FileStream AcquireBuildLock(string path)
    {
        var watch = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                var operationResult8 = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                return operationResult8;
            }
            catch (IOException)when (watch.Elapsed < TimeSpan.FromSeconds(30))
            {
                Thread.Sleep(25);
            }
        }
    }

    private void DeleteOwnedDirectory(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var operationResult14 = Path.TrimEndingDirectorySeparator(_rootPath) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(operationResult14, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("Script artifact path is outside its root. 脚本制品路径超出根目录。");
        }

        Directory.Delete(fullPath, recursive: true);
    }

    private static void ValidateSegment(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        if (string.IsNullOrWhiteSpace(value) || value is "." or ".." || value != Path.GetFileName(value) || value.Contains(Path.DirectorySeparatorChar) || value.Contains(Path.AltDirectorySeparatorChar) || value.IndexOfAny(invalidCharacters) >= 0)
        {
            throw new ArgumentException("Artifact identifiers must be simple directory names. 制品标识必须为简单目录名称。", nameof(value));
        }
    }

    private sealed record ArtifactManifest(string ProjectId, string NodeId, string SourceHash, string ArtifactFingerprint, string LanguageVersion);
}
