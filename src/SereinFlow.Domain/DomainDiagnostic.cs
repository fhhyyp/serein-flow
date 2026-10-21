namespace SereinFlow.Domain;
/// <summary>
/// Represents domain diagnostic data used by SereinFlow.
/// 表示 SereinFlow 使用的Domain诊断数据。
/// </summary>
public sealed record DomainDiagnostic(string Code, string Message, string? Path = null);
public sealed class DomainValidationException : Exception
{
    public DomainValidationException(IReadOnlyList<DomainDiagnostic> diagnostics) : base("The domain object is invalid. 领域对象无效。")
    {
        Diagnostics = diagnostics;
    }

    public IReadOnlyList<DomainDiagnostic> Diagnostics { get; }
}
