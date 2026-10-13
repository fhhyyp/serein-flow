using SereinFlow.Library;

namespace SereinFlow.TestLibrary;

[FlowLibrary("Run variable test library")]
public sealed class VariableNodes(LibraryVariableService variables)
{
    [FlowNode(Id = "test.variables.share", AnotherName = "Verify shared run variables")]
    public string ShareVariables(IFlowContext context)
    {
        if (!ReferenceEquals(variables, context.Variables))
            throw new InvalidOperationException("Constructor and context must share one variable table.");

        var count = context.Variables.Get<int>("test.variables.count") + 1;
        variables.Set("test.variables.count", count);
        return context.Variables.Get<int>("test.variables.count").ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
