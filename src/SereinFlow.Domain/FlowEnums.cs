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
    Action,
    FlowCall,
    GlobalData,
    Flipflop,
    Script,
    ExpOp,
    ExpCondition,
    Condition,
    Value,
    Expression,
    Trigger
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
    Error,
    Upstream
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
    TimedOut
}
