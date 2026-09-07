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
    public string Track { get; set; } = "Development";
    public string Operation { get; set; } = "Imported";
    public long? ParentVersion { get; set; }
    public long? SourceVersion { get; set; }
    public string Remark { get; set; } = string.Empty;
    public string? CreatedAt { get; set; }
}

[SugarTable("FlowProductionHeads")]
public sealed class FlowProductionHeadRecord
{
    [SugarColumn(IsPrimaryKey = true)] public string FlowId { get; set; } = string.Empty;
    public long Version { get; set; }
    public string UpdatedAt { get; set; } = string.Empty;
}

[SugarTable("FlowVersionCounters")]
public sealed class FlowVersionCounterRecord
{
    [SugarColumn(IsPrimaryKey = true)] public string FlowId { get; set; } = string.Empty;
    public long NextVersion { get; set; }
}

[SugarTable("FlowRuns")]
public sealed class FlowRunRecord
{
    [SugarColumn(IsPrimaryKey = true)] public string Id { get; set; } = string.Empty;
    public string? ProjectId { get; set; }
    public string FlowId { get; set; } = string.Empty;
    public long FlowVersion { get; set; }
    public string DefinitionChecksum { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? StartedAt { get; set; }
    public string? EndedAt { get; set; }
    public string? ErrorSummary { get; set; }
    public string? CreatedAt { get; set; }
    public string? CancellationReason { get; set; }
    public string? Deadline { get; set; }
    public int TimeoutSeconds { get; set; }
    public int MaxSteps { get; set; }
    public int MaxNodeVisits { get; set; }
    public string? ProjectInputsJson { get; set; }
    public string? ConcurrencyMode { get; set; }
    public string? ExclusivityKey { get; set; }
    public bool IsListenerRun { get; set; }
    public string? QueuedAt { get; set; }
    public string ExecutionKind { get; set; } = "Production";
    public string? DebugSessionId { get; set; }
}

[SugarTable("FlowDebugSessions")]
public sealed class FlowDebugSessionRecord
{
    [SugarColumn(IsPrimaryKey = true)] public string Id { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string FlowId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string BreakpointsJson { get; set; } = "[]";
    public string? CurrentNodeId { get; set; }
    public string? ActiveInvocationId { get; set; }
    public string? ActiveFlipflopNodeId { get; set; }
    public int QueuedTriggerCount { get; set; }
    public long LastCommandSequence { get; set; }
    public long StateRevision { get; set; }
    public string? PauseStateJson { get; set; }
    public string? LastNodeResultJson { get; set; }
    public string? FailureMessage { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
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

[SugarTable("FlowRunOutputs")]
public sealed class FlowRunOutputRecord
{
    [SugarColumn(IsPrimaryKey = true)] public string RunId { get; set; } = string.Empty;
    [SugarColumn(IsPrimaryKey = true)] public long Sequence { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public string? Branch { get; set; }
    public string OutputsJson { get; set; } = "{}";
    public string InputsJson { get; set; } = "{}";
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
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
    public string? DllSha256 { get; set; }
    public string UploadedAt { get; set; } = string.Empty;
    public string PackagePath { get; set; } = string.Empty;
    public string NodeCatalogJson { get; set; } = "[]";
    public string? FamilyId { get; set; }
    public string? SemanticVersion { get; set; }
    public string? CompatibilityManifestJson { get; set; }
    public int CatalogSchemaVersion { get; set; }
    public string Status { get; set; } = "Available";
    public string? ArchivedAt { get; set; }
}

[SugarTable("LibraryFamilies")]
public sealed class LibraryFamilyRecord
{
    [SugarColumn(IsPrimaryKey = true)] public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LatestArtifactId { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
}

[SugarTable("FlowLibraryBindings")]
public sealed class FlowLibraryBindingRecord
{
    [SugarColumn(IsPrimaryKey = true)] public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string FlowId { get; set; } = string.Empty;
    public long FlowVersion { get; set; }
    public string LibraryArtifactId { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}

[SugarTable("RunLibraryBindings")]
public sealed class RunLibraryBindingRecord
{
    [SugarColumn(IsPrimaryKey = true)] public string Id { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public string LibraryArtifactId { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}

[SugarTable("LibraryUpgradePlans")]
public sealed class LibraryUpgradePlanRecord
{
    [SugarColumn(IsPrimaryKey = true)] public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string SourceArtifactId { get; set; } = string.Empty;
    public string TargetArtifactId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string AnalysisJson { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string? AppliedAt { get; set; }
    public string? FailureMessage { get; set; }
}

[SugarTable("ProjectLibraryReferences")]
public sealed class ProjectLibraryReferenceRecord
{
    [SugarColumn(IsPrimaryKey = true)] public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string LibraryId { get; set; } = string.Empty;
    public string ReferencedAt { get; set; } = string.Empty;
}

[SugarTable("RunEnvironmentSettings")]
public sealed class RunEnvironmentSettingsRecord
{
    [SugarColumn(IsPrimaryKey = true)] public string Id { get; set; } = "default";
    public int QueueCapacity { get; set; }
    public int MaxConcurrentRuns { get; set; }
    public int MaxConcurrentListenerRuns { get; set; }
    public int MaxConcurrentRunsPerProject { get; set; }
    public int QueueWaitTimeoutSeconds { get; set; }
    public int ShutdownGracePeriodSeconds { get; set; }
    public int SynchronousInvocationTimeoutSeconds { get; set; }
    public long MaxLibraryUploadBytes { get; set; }
    public string UpdatedAt { get; set; } = string.Empty;
}

[SugarTable("McpApiKeys")]
public sealed class McpApiKeyRecord
{
    [SugarColumn(IsPrimaryKey = true)] public string Id { get; set; } = string.Empty;
    public string? ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public string SecretHash { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public string PermissionsJson { get; set; } = "[]";
    public string CreatedAt { get; set; } = string.Empty;
    public string? ExpiresAt { get; set; }
    public string? RevokedAt { get; set; }
    public string? LastUsedAt { get; set; }
    public bool IsAdministrator { get; set; }
}

[SugarTable("McpMutationPreviews")]
public sealed class McpPreviewRecord
{
    [SugarColumn(IsPrimaryKey = true)] public string Id { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string PrincipalId { get; set; } = string.Empty;
    public string? ProjectId { get; set; }
    public string? FlowId { get; set; }
    public long? ExpectedVersion { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public string PreviewFingerprint { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string ExpiresAt { get; set; } = string.Empty;
    public string? AppliedAt { get; set; }
}

[SugarTable("McpAuditEntries")]
public sealed class McpAuditRecord
{
    [SugarColumn(IsPrimaryKey = true)] public string Id { get; set; } = string.Empty;
    public string PrincipalId { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string? ProjectId { get; set; }
    public string? FlowId { get; set; }
    public string? Track { get; set; }
    public long? FlowVersion { get; set; }
    public string? PreviewId { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string? RequestHash { get; set; }
    public string? Summary { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public long DurationMilliseconds { get; set; }
    public long InputBytes { get; set; }
    public long OutputBytes { get; set; }
}

[SugarTable("McpIdempotencyRecords")]
public sealed class McpIdempotencyRecord
{
    [SugarColumn(IsPrimaryKey = true)] public string Id { get; set; } = string.Empty;
    public string PrincipalId { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string ResponseJson { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}

[SugarTable("FlowInterfaces")]
public sealed class FlowInterfaceRecord
{
    [SugarColumn(IsPrimaryKey = true)] public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string FlowId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string InvocationMode { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
}
