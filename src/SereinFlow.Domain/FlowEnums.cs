namespace SereinFlow.Domain;
/// <summary>
/// Defines the lifecycle state of a project.
/// 定义项目的生命周期状态。
/// </summary>
public enum ProjectStatus
{
    /// <summary>
    /// Indicates the draft value.
    /// 表示“草稿”这一取值。
    /// </summary>
    Draft,
    /// <summary>
    /// Indicates the ready value.
    /// 表示“就绪”这一取值。
    /// </summary>
    Ready,
    /// <summary>
    /// Indicates the script invalid value.
    /// 表示“脚本无效”这一取值。
    /// </summary>
    ScriptInvalid,
    /// <summary>
    /// Indicates the archived value.
    /// 表示“已归档”这一取值。
    /// </summary>
    Archived
}

/// <summary>
/// Defines the lifecycle stage of a flow canvas.
/// 定义流程画布的生命周期阶段。
/// </summary>
public enum CanvasLifecycle
{
    /// <summary>
    /// Indicates the main value.
    /// 表示“主画布”这一取值。
    /// </summary>
    Main,
    /// <summary>
    /// Indicates the init value.
    /// 表示“初始化”这一取值。
    /// </summary>
    Init,
    /// <summary>
    /// Indicates the loading value.
    /// 表示“加载”这一取值。
    /// </summary>
    Loading,
    /// <summary>
    /// Indicates the exit value.
    /// 表示“退出”这一取值。
    /// </summary>
    Exit,
    /// <summary>
    /// Indicates the custom value.
    /// 表示“自定义”这一取值。
    /// </summary>
    Custom
}

/// <summary>
/// Identifies the kind of flow node.
/// 标识流程节点的类型。
/// </summary>
public enum NodeType
{
    /// <summary>
    /// Indicates the action value.
    /// 表示“动作”这一取值。
    /// </summary>
    Action = 0,
    /// <summary>
    /// Indicates the flipflop value.
    /// 表示“触发器”这一取值。
    /// </summary>
    Flipflop = 1,
    /// <summary>
    /// Indicates the script value.
    /// 表示“脚本”这一取值。
    /// </summary>
    Script = 2,
    // Value 3 belonged to the removed Condition node. Do not reuse it:
    // persisted numeric payloads must remain invalid instead of becoming FlowCall.
    // 数值 3 曾属于已移除的 Condition 节点，不能复用，避免旧数据被误解释为 FlowCall。
    FlowCall = 4,
}

/// <summary>
/// Defines how a variadic parameter is represented.
/// 定义可变参数的表示方式。
/// </summary>
public enum VariadicParameterMode
{
    /// <summary>
    /// Indicates the expanded value.
    /// 表示“展开”这一取值。
    /// </summary>
    Expanded,
    /// <summary>
    /// Indicates the collection value.
    /// 表示“集合”这一取值。
    /// </summary>
    Collection
}

/// <summary>
/// Defines whether a node port accepts or emits data.
/// 定义节点端口用于接收还是输出数据。
/// </summary>
public enum PortDirection
{
    /// <summary>
    /// Indicates the input value.
    /// 表示“输入”这一取值。
    /// </summary>
    Input,
    /// <summary>
    /// Indicates the output value.
    /// 表示“输出”这一取值。
    /// </summary>
    Output
}

/// <summary>
/// Distinguishes execution links from data links.
/// 区分执行连线和数据连线。
/// </summary>
public enum ConnectionKind
{
    /// <summary>
    /// Indicates the execution value.
    /// 表示“执行”这一取值。
    /// </summary>
    Execution,
    /// <summary>
    /// Indicates the data value.
    /// 表示“数据”这一取值。
    /// </summary>
    Data
}

/// <summary>
/// Identifies the branch selected by a node result.
/// 标识节点结果所选择的分支。
/// </summary>
public enum ExecutionBranch
{
    /// <summary>
    /// Indicates the success value.
    /// 表示“成功”这一取值。
    /// </summary>
    Success,
    /// <summary>
    /// Indicates the failure value.
    /// 表示“失败”这一取值。
    /// </summary>
    Failure,
    /// <summary>
    /// Indicates the error value.
    /// 表示“错误”这一取值。
    /// </summary>
    Error
}

/// <summary>
/// Defines where a node parameter obtains its value.
/// 定义节点参数的取值来源。
/// </summary>
public enum DataSource
{
    /// <summary>
    /// Indicates the literal value.
    /// 表示“字面量”这一取值。
    /// </summary>
    Literal,
    /// <summary>
    /// Indicates the previous node value.
    /// 表示“上游节点”这一取值。
    /// </summary>
    PreviousNode,
    /// <summary>
    /// Indicates the project input value.
    /// 表示“项目输入”这一取值。
    /// </summary>
    ProjectInput,
    /// <summary>
    /// Indicates the expression value.
    /// 表示“表达式”这一取值。
    /// </summary>
    Expression
}

/// <summary>
/// Defines the lifecycle state of a flow run.
/// 定义流程运行的生命周期状态。
/// </summary>
public enum FlowRunStatus
{
    /// <summary>
    /// Indicates the pending value.
    /// 表示“等待中”这一取值。
    /// </summary>
    Pending,
    /// <summary>
    /// Indicates the running value.
    /// 表示“运行中”这一取值。
    /// </summary>
    Running,
    /// <summary>
    /// Indicates the succeeded value.
    /// 表示“成功”这一取值。
    /// </summary>
    Succeeded,
    /// <summary>
    /// Indicates the failed value.
    /// 表示“失败”这一取值。
    /// </summary>
    Failed,
    /// <summary>
    /// Indicates the cancelled value.
    /// 表示“已取消”这一取值。
    /// </summary>
    Cancelled,
    /// <summary>
    /// Indicates the timed out value.
    /// 表示“TimedOut”这一取值。
    /// </summary>
    TimedOut,
    /// <summary>
    /// Indicates the interrupted value.
    /// 表示“已中断”这一取值。
    /// </summary>
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
    /// <summary>
    /// Indicates the production value.
    /// 表示“生产”这一取值。
    /// </summary>
    Production,
    /// <summary>
    /// Indicates the debug value.
    /// 表示“调试”这一取值。
    /// </summary>
    Debug
}

/// <summary>
/// Defines the lifecycle state of a debug session.
/// 定义调试会话的生命周期状态。
/// </summary>
public enum FlowDebugSessionStatus
{
    /// <summary>
    /// Indicates the pending value.
    /// 表示“等待中”这一取值。
    /// </summary>
    Pending,
    /// <summary>
    /// Indicates the running value.
    /// 表示“运行中”这一取值。
    /// </summary>
    Running,
    /// <summary>
    /// Indicates the paused value.
    /// 表示“已暂停”这一取值。
    /// </summary>
    Paused,
    /// <summary>
    /// Indicates the completed value.
    /// 表示“Completed”这一取值。
    /// </summary>
    Completed,
    /// <summary>
    /// Indicates the cancelled value.
    /// 表示“已取消”这一取值。
    /// </summary>
    Cancelled,
    /// <summary>
    /// Indicates the failed value.
    /// 表示“失败”这一取值。
    /// </summary>
    Failed
}

/// <summary>
/// Controls whether separate instances of one flow may be admitted together.
/// 控制同一流程的不同运行实例是否可以同时准入。
/// </summary>
public enum FlowConcurrencyMode
{
    /// <summary>
    /// Indicates the parallel value.
    /// 表示“并行”这一取值。
    /// </summary>
    Parallel,
    /// <summary>
    /// Indicates the exclusive reject value.
    /// 表示“Exclusive拒绝”这一取值。
    /// </summary>
    ExclusiveReject
}
