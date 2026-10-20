using SereinFlow.Contracts;

namespace SereinFlow.Infrastructure.Persistence;
/// <summary>
/// Extracts library artifact references from immutable DTOs without resolving
/// catalog metadata. It deliberately records only the persisted binding, not
/// an inferred "latest" version.
/// 从不可变 DTO 提取类库工件引用，不解析目录元数据。它只记录持久化绑定，
/// 不会推断“最新”版本。
/// </summary>
internal static class LibraryBindingIndex
{
    public static IReadOnlyList<string> Extract(FlowDefinitionDto definition)
    {
        var selectManyResult = definition.Canvases.SelectMany(static canvas => canvas.Nodes);
        var whereResult = selectManyResult.Where(static node => node.Type is NodeTypeDto.Action or NodeTypeDto.Flipflop);
        var selectResult = whereResult.Select(static node => node.Ui?.LibraryId);
        var whereResult1 = selectResult.Where(static id =>
        {
            var evaluatedValue = !string.IsNullOrWhiteSpace(id);
            return evaluatedValue;
        });
        var selectResult2 = whereResult1.Select(static id =>
        {
            var trimResult = id!.Trim();
            return trimResult;
        });
        var distinctResult = selectResult2.Distinct(StringComparer.OrdinalIgnoreCase);
        var orderResult = distinctResult.Order(StringComparer.Ordinal);
        var toArrayResult = orderResult.ToArray();
        return toArrayResult;
    }
}
