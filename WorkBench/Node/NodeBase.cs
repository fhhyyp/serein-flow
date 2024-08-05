namespace DySerin;






/*public class ConditionNode : NodeBase, ICondition
{
    private Func<bool> ConditionFunc { get; set; }

    public ConditionNode(Func<bool> conditionFunc)
    {
        ConditionFunc = conditionFunc;
    }

    public override void Execute()
    {
        if (ConditionFunc())
        {
            EnterTrueBranch();
        }
        else
        {
            EnterFalseBranch();
        }
    }

    // 实现条件接口的方法
    public bool Evaluate(DynamicContext context)
    {
        Context = context; // 更新节点的上下文
        return ConditionFunc();
    }
}



public class ActionNode : NodeBase, IAction
{
    private Action ActionMethod { get; set; }

    public ActionNode(Action actionMethod)
    {
        ActionMethod = actionMethod;
    }

    public override void Execute()
    {
        try
        {
            ActionMethod();
            EnterTrueBranch(); // 动作成功，进入真分支
        }
        catch (Exception ex)
        {
            // 可添加异常处理逻辑
            Return(); // 动作失败，终止节点
        }
    }

    // 实现动作接口的方法
    public void Execute(DynamicContext context)
    {
        Context = context; // 更新节点的上下文
        Execute();
    }
}





*/







/*
/// <summary>
/// 根节点
/// </summary>
public abstract class NodeControl : UserControl
{
    public string Id { get; set; }
    public string Name { get; set; }

    public abstract void Execute(NodeContext context);
}

/// <summary>
/// 条件节点
/// </summary>
public class ConditionNodeControl : NodeControl
{
    public Func<NodeContext, bool> Condition { get; set; }
    public NodeControl TrueNode { get; set; }
    public NodeControl FalseNode { get; set; }

    public ConditionNodeControl()
    {
        this.Content = new TextBlock { Text = "条件节点" };
        this.Background = Brushes.LightBlue;
    }

    public override void Execute(NodeContext context)
    {
        if (Condition(context))
        {
            TrueNode?.Execute(context);
        }
        else
        {
            FalseNode?.Execute(context);
        }
    }
}

/// <summary>
/// 动作节点
/// </summary>
public class ActionNodeControl : NodeControl
{
    public Action<NodeContext> Action { get; set; }

    public ActionNodeControl()
    {
        this.Content = new TextBlock { Text = "动作节点" };
        this.Background = Brushes.LightGreen;
    }

    public override void Execute(NodeContext context)
    {
        Action?.Invoke(context);
    }
}


/// <summary>
/// 状态节点
/// </summary>
public class StateNodeControl : NodeControl
{
    public Func<NodeContext, object> StateFunction { get; set; }

    public StateNodeControl()
    {
        this.Content = new TextBlock { Text = "状态节点" };
        this.Background = Brushes.LightYellow;
    }

    public override void Execute(NodeContext context)
    {
        var result = StateFunction(context);
        context.Set("StateResult", result);
    }
}

/// <summary>
/// 节点上下文
/// </summary>
public class NodeContext
{
    private readonly Dictionary<string, object> _data = new Dictionary<string, object>();

    public void Set<T>(string key, T value)
    {
        _data[key] = value;
    }

    public T Get<T>(string key)
    {
        return _data.ContainsKey(key) ? (T)_data[key] : default;
    }
}
*/

/*
public class Context
{
    public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
    public void Set<T>(string key, T value)
    {
        Data[key] = value;
    }

    public T Get<T>(string key)
    {
        return Data.ContainsKey(key) ? (T)Data[key] : default;
    }
}

public interface INode
{
    Context Enter(Context context);
    Context Exit(Context context);
}



public partial class ConditionNode : INode
{
    public Func<Context, bool> Condition { get; set; }
    public INode TrueBranch { get; set; }
    public INode FalseBranch { get; set; }

    public Context Enter(Context context)
    {
        if (Condition(context))
        {
            return TrueBranch?.Enter(context) ?? context;
        }
        else
        {
            return FalseBranch?.Enter(context) ?? context;
        }
    }

    public Context Exit(Context context)
    {
        return context;
    }


   
}

public partial class ActionNode : INode
{
    public Action<Context> Action { get; set; }
    public bool IsTask { get; set; }

    public Context Enter(Context context)
    {
        if (IsTask)
        {
            Task.Run(() => Action(context));
        }
        else
        {
            Action(context);
        }
        return context;
    }

    public Context Exit(Context context)
    {
        return context;
    }
}

public partial class StateNode : INode
{
    public Context Enter(Context context)
    {
        return context;
    }

    public Context Exit(Context context)
    {
        return context;
    }
}



*/
