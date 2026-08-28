namespace SereinFlow.Domain;

public enum ProjectStatus
{
    Draft,
    Ready,
    ScriptInvalid,
    Archived
}

public enum CanvasLifecycle
{
    Main,
    Init,
    Loading,
    Exit,
    Custom
}

public enum NodeType
{
    Action = 0,
    Flipflop = 1,
    Script = 2,
    // Value 3 belonged to the removed Condition node. Do not reuse it:
    // persisted numeric payloads must remain invalid instead of becoming FlowCall.
    // 数值 3 曾属于已移除的 Condition 节点，不能复用，避免旧数据被误解释为 FlowCall。
    FlowCall = 4,
}

public enum VariadicParameterMode
{
    Expanded,
    Collection
}

public enum PortDirection
{
    Input,
    Output
}

public enum ConnectionKind
{
    Execution,
    Data
}

public enum ExecutionBranch
{
    Success,
    Failure,
    Error
}

public enum DataSource
{
    Literal,
    PreviousNode,
    ProjectInput,
    Expression
}

public enum FlowRunStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Cancelled,
    TimedOut,
    Interrupted
}

/// <summary>
/// Identifies the source and interaction model of a run without introducing a
/// flow publishing lifecycle. A debug run still executes an immutable normal
/// flow snapshot.
/// 标识运行来源和交互模型，不引入流程发布生命周期。调试运行仍执行不可变的普通流程快照。
/// </summary>
public enum FlowRunExecutionKind
{
    Production,
    Debug
}

public enum FlowDebugSessionStatus
{
    Pending,
    Running,
    Paused,
    Completed,
    Cancelled,
    Failed
}

/// <summary>
/// Controls whether separate instances of one flow may be admitted together.
/// 控制同一流程的不同运行实例是否可以同时准入。
/// </summary>
public enum FlowConcurrencyMode
{
    Parallel,
    ExclusiveReject
}
