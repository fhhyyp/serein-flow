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
    Flipflop,
    Script,
    Condition,
    FlowCall,
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
    TimedOut
}
