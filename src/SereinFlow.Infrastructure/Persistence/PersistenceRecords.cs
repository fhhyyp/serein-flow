using SqlSugar;

namespace SereinFlow.Infrastructure.Persistence;

// These records are Infrastructure-only persistence shapes. Domain objects and
// API contracts never depend on SqlSugar attributes or storage columns.
// 这些记录只存在于 Infrastructure 层；领域对象和 API 契约不依赖 SqlSugar
// 特性或数据库列名。
[SugarTable("Projects")]
public sealed class ProjectRecord
{
    [SugarColumn(IsPrimaryKey = true)] public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
}

[SugarTable("FlowDefinitions")]
public sealed class FlowDefinitionRecord
{
    [SugarColumn(IsPrimaryKey = true)] public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public long Version { get; set; }
    public string DefinitionJson { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;
}

[SugarTable("FlowDefinitionVersions")]
public sealed class FlowDefinitionVersionRecord
{
    [SugarColumn(IsPrimaryKey = true)] public string FlowId { get; set; } = string.Empty;
    [SugarColumn(IsPrimaryKey = true)] public long Version { get; set; }
    public string DefinitionJson { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;
}

[SugarTable("FlowRuns")]
public sealed class FlowRunRecord
{
    [SugarColumn(IsPrimaryKey = true)] public string Id { get; set; } = string.Empty;
    public string? ProjectId { get; set; }
    public string FlowId { get; set; } = string.Empty;
    public long FlowVersion { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? StartedAt { get; set; }
    public string? EndedAt { get; set; }
    public string? ErrorSummary { get; set; }
    public string? CreatedAt { get; set; }
    public string? CancellationReason { get; set; }
    public string? Deadline { get; set; }
    public int MaxSteps { get; set; }
    public int MaxNodeVisits { get; set; }
    public string? ProjectInputsJson { get; set; }
}

[SugarTable("FlowRunDefinitions")]
public sealed class FlowRunDefinitionRecord
{
    [SugarColumn(IsPrimaryKey = true)] public string RunId { get; set; } = string.Empty;
    public string FlowId { get; set; } = string.Empty;
    public long FlowVersion { get; set; }
    public int SchemaVersion { get; set; }
    public string Checksum { get; set; } = string.Empty;
    public string DefinitionJson { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}

[SugarTable("FlowRunEvents")]
public sealed class FlowRunEventRecord
{
    [SugarColumn(IsPrimaryKey = true)] public string RunId { get; set; } = string.Empty;
    [SugarColumn(IsPrimaryKey = true)] public long Sequence { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? NodeId { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
}

[SugarTable("Libraries")]
public sealed class LibraryRecord
{
    [SugarColumn(IsPrimaryKey = true)] public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string UploadedAt { get; set; } = string.Empty;
    public string PackagePath { get; set; } = string.Empty;
    public string NodeCatalogJson { get; set; } = "[]";
}
