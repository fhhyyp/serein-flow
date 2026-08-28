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
        => definition.Canvases
            .SelectMany(static canvas => canvas.Nodes)
            .Where(static node => node.Type is NodeTypeDto.Action or NodeTypeDto.Flipflop)
            .Select(static node => node.Ui?.LibraryId)
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Select(static id => id!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToArray();
}
