namespace SereinFlow.Domain;

public sealed record DomainDiagnostic(string Code, string Message, string? Path = null);

public sealed class DomainValidationException : Exception
{
    public DomainValidationException(IReadOnlyList<DomainDiagnostic> diagnostics)
        : base("The domain object is invalid.")
    {
        Diagnostics = diagnostics;
    }

    public IReadOnlyList<DomainDiagnostic> Diagnostics { get; }
}
