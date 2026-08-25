using SereinFlow.Core.Api;

namespace SereinFlow.TestLibrary;

[FlowLibrary("SereinFlow test library")]
public sealed class MathNodes
{
    [FlowNode(AnotherName = "Add numbers", Desc = "Adds two integers.")]
    public int Add(
        [NodeParam(Name = "left")] int left,
        [NodeParam(Name = "right")] int right)
        => left + right;

    [FlowNode(AnotherName = "Format text", Desc = "Formats a number with a prefix.")]
    public string Format(
        [NodeParam(Name = "value")] double value,
        [NodeParam(Name = "prefix")] string prefix)
        => $"{prefix}{value:0.###}";

    [FlowNode(NodeType = NodeType.Flipflop, AnotherName = "Is positive", Desc = "Returns whether the value is positive.")]
    public Task<bool> IsPositive([NodeParam(Name = "value")] int value)
        => Task.FromResult(value > 0);

    [FlowNode(Desc = "Emits a message and has no data output.")]
    public void Emit([NodeParam(Name = "message")] string message)
    {
        _ = message;
    }

    [FlowNode(AnotherName = "Join labels", Desc = "Joins two labels with a separator.")]
    public string Join(
        [NodeParam(Name = "first")] string first,
        [NodeParam(Name = "second")] string second,
        [NodeParam(Name = "separator")] string separator = " ")
        => string.Join(separator, first, second);
}
