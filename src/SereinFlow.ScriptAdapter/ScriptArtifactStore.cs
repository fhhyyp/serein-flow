using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using SereinFlow.Domain;
using ScriptLang;
using ScriptLang.Runtime.ByteCode;
using SereinFlow.Contracts;

namespace SereinFlow.ScriptAdapter;
public sealed record ScriptArtifact(string ProjectId, string NodeId, string SourceHash, string ArtifactFingerprint, string LanguageVersion, string Path);
public sealed record ScriptArtifactBuildResult(bool IsSuccess, IReadOnlyDictionary<string, ScriptArtifact> Artifacts, IReadOnlyList<ScriptExecutionDiagnostic> Diagnostics);
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
        {
            throw new ArgumentException("Artifact root cannot be empty. 缓存根目录不能为空。", nameof(rootPath));
        }

        _rootPath = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(_rootPath);
    }

    public string GetProjectPath(string projectId)
    {
        ValidateProjectId(projectId);
        var combineResult = Path.Combine(_rootPath, projectId);
        return combineResult;
    }

    public ScriptArtifactBuildResult RebuildProject(string projectId, IEnumerable<ScriptNodeDefinition> definitions)
    {
        ValidateProjectId(projectId);
        if (definitions is null)
        {
            throw new ArgumentNullException(nameof(definitions), "Script definitions cannot be null. 脚本定义集合不能为空。");
        }

        var nodes = definitions.ToArray();
        var groupByResult = nodes.GroupBy(node => node.NodeId, StringComparer.Ordinal);
        var duplicate = groupByResult.FirstOrDefault(group =>
        {
            var evaluatedValue = group.Count() > 1;
            return evaluatedValue;
        });
        if (duplicate is not null)
        {
            var dictionary = new Dictionary<string, ScriptArtifact>();
            IReadOnlyList<ScriptExecutionDiagnostic> evaluatedValue1 = [new(ScriptErrorCodes.DuplicateNode, $"Script node '{duplicate.Key}' is defined more than once. 脚本节点“{duplicate.Key}”被重复定义。", NodeId: duplicate.Key)];
            ScriptArtifactBuildResult evaluatedValue2 = new(false, dictionary, evaluatedValue1);
            return evaluatedValue2;
        }

        var projectPath = GetProjectPath(projectId);
        if (Directory.Exists(projectPath))
        {
            Directory.Delete(projectPath, recursive: true);
        }

        var evaluatedValue14 = $".staging-{projectId}-{Guid.NewGuid():N}";
        var stagingPath = Path.Combine(_rootPath, evaluatedValue14);
        Directory.CreateDirectory(stagingPath);
        var artifacts = new Dictionary<string, ScriptArtifact>(StringComparer.Ordinal);
        var diagnostics = new List<ScriptExecutionDiagnostic>();
        try
        {
            foreach (var node in nodes)
            {
                if (!IsSafeSegment(node.NodeId))
                {
                    ScriptExecutionDiagnostic evaluatedValue3 = new(ScriptErrorCodes.NodeIdInvalid, $"Script node ID '{node.NodeId}' cannot be used as an artifact name. 脚本节点 ID“{node.NodeId}”不能用作缓存名称。", NodeId: node.NodeId);
                    diagnostics.Add(evaluatedValue3);
                    var dictionary4 = new Dictionary<string, ScriptArtifact>();
                    ScriptArtifactBuildResult evaluatedValue5 = new(false, dictionary4, diagnostics);
                    return evaluatedValue5;
                }

                var engine = new ScriptEngine();
                var sourceName = $"{node.NodeId}.script";
                var selectResult = node.Inputs.Select(input => input.Name);
                var chunk = engine.CompileSource(node.Source, sourceName, selectResult);
                var artifactPath = Path.Combine(stagingPath, $"{node.NodeId}.ssc");
                ByteCodeChunk.Save(chunk, artifactPath);
                artifacts[node.NodeId] = new ScriptArtifact(projectId, node.NodeId, node.SourceHash, node.ArtifactFingerprint, node.LanguageVersion, artifactPath);
            }

            var selectResult7 = artifacts.Values.Select(ToManifest);
            var toArrayResult = selectResult7.ToArray();
            var manifest = new ArtifactManifest(projectId, DateTimeOffset.UtcNow, toArrayResult);
            var combineResult8 = Path.Combine(stagingPath, "manifest.json");
            var serializeResult = JsonSerializer.Serialize(manifest, JsonOptions);
            File.WriteAllText(combineResult8, serializeResult);
            Directory.Move(stagingPath, projectPath);
            var published = artifacts.ToDictionary(pair => pair.Key, pair =>
            {
                var evaluatedValue6 = pair.Value with
                {
                    Path = Path.Combine(projectPath, $"{pair.Key}.ssc")
                };
                return evaluatedValue6;
            }, StringComparer.Ordinal);
            ScriptArtifactBuildResult evaluatedValue9 = new(true, published, diagnostics);
            return evaluatedValue9;
        }
        catch (Exception exception)
        {
            var evaluatedValue10 = nodes.FirstOrDefault()?.NodeId;
            ScriptExecutionDiagnostic evaluatedValue11 = new(ScriptErrorCodes.CompileFailed, $"Script artifact compilation failed. 脚本缓存编译失败。 {exception.Message}", NodeId: evaluatedValue10);
            diagnostics.Add(evaluatedValue11);
            var dictionary12 = new Dictionary<string, ScriptArtifact>();
            ScriptArtifactBuildResult evaluatedValue13 = new(false, dictionary12, diagnostics);
            return evaluatedValue13;
        }
        finally
        {
            if (Directory.Exists(stagingPath))
            {
                Directory.Delete(stagingPath, recursive: true);
            }
        }
    }

    public bool TryLoad(string projectId, ScriptNodeDefinition definition, out ByteCodeChunk? chunk, out string? artifactPath)
    {
        ValidateProjectId(projectId);
        chunk = null;
        artifactPath = null;
        if (!IsSafeSegment(definition.NodeId))
        {
            return false;
        }

        var getProjectPathResult15 = GetProjectPath(projectId);
        var path = Path.Combine(getProjectPathResult15, $"{definition.NodeId}.ssc");
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var getProjectPathResult = GetProjectPath(projectId);
            var manifestPath = Path.Combine(getProjectPathResult, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                return false;
            }

            var readAllTextResult = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize<ArtifactManifest>(readAllTextResult, JsonOptions);
            var entry = manifest?.Artifacts.FirstOrDefault(item =>
            {
                var equalsResult = string.Equals(item.NodeId, definition.NodeId, StringComparison.Ordinal);
                return equalsResult;
            });
            if (entry is null || !string.Equals(entry.ArtifactFingerprint, definition.ArtifactFingerprint, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

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
    {
        ScriptArtifactStore.ArtifactManifestEntry evaluatedValue16 = new(artifact.NodeId, artifact.SourceHash, artifact.ArtifactFingerprint, artifact.LanguageVersion);
        return evaluatedValue16;
    }

    private static void ValidateProjectId(string projectId)
    {
        if (!IsSafeSegment(projectId))
        {
            throw new ArgumentException("Project ID must be a simple directory name. 项目 ID 必须是简单目录名称。", nameof(projectId));
        }
    }

    private static bool IsSafeSegment(string value)
    {
        var evaluatedValue17 = !string.IsNullOrWhiteSpace(value) && value is not "." and not ".." && value == Path.GetFileName(value) && !value.Contains(Path.DirectorySeparatorChar) && !value.Contains(Path.AltDirectorySeparatorChar) && resolveIndexOfAnyResult() < 0;
        return evaluatedValue17;
        int resolveIndexOfAnyResult()
        {
            var getInvalidFileNameCharsResult = Path.GetInvalidFileNameChars();
            var indexOfAnyResult = value.IndexOfAny(getInvalidFileNameCharsResult);
            return indexOfAnyResult;
        }
    }

    private sealed record ArtifactManifest(string ProjectId, DateTimeOffset BuiltAt, IReadOnlyList<ArtifactManifestEntry> Artifacts);
    private sealed record ArtifactManifestEntry(string NodeId, string SourceHash, string ArtifactFingerprint, string LanguageVersion);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };
}
