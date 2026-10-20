using Microsoft.AspNetCore.Mvc;

namespace SereinFlow.Api;

/// <summary>
/// Shared problem fields for MVC and IResult responses. Explicit code/version
/// factories retain null values; callers decide whether an optional field exists.
/// MVC 与 IResult 共用错误字段；显式工厂保留 null，避免混淆空值与字段缺失。
/// </summary>
internal static class ApiProblemDetailsFactory
{
    internal static ProblemDetails Create(int statusCode, string? title, string? detail = null, IReadOnlyDictionary<string, object?>? extensions = null)
    {
        var problem = new ProblemDetails { Status = statusCode, Title = title, Detail = detail };
        if (extensions is not null)
        {
            foreach (var extension in extensions)
            {
                problem.Extensions[extension.Key] = extension.Value;
            }
        }

        return problem;
    }

    internal static ProblemDetails WithCode(int statusCode, string? title, string? code, long? currentVersion = null)
    {
        var problem = Create(statusCode, title);
        problem.Extensions["code"] = code;
        if (currentVersion is not null)
        {
            problem.Extensions["currentVersion"] = currentVersion;
        }

        return problem;
    }

    internal static ProblemDetails WithCurrentVersion(int statusCode, string? title, long? currentVersion)
    {
        var problem = Create(statusCode, title);
        problem.Extensions["currentVersion"] = currentVersion;
        return problem;
    }
}
