using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

const int SuccessExitCode = 0;
const int ViolationExitCode = 1;
const int FirstPosition = 1;
const int EmptyPosition = 0;
var repositoryRoot = args.Length > EmptyPosition ? Path.GetFullPath(args[EmptyPosition]) : Directory.GetCurrentDirectory();
var selfTest = args.Contains("--self-test", StringComparer.Ordinal);
if (selfTest)
{
    var runResult = ConventionTests.Run();
    return runResult;
}

var processStart = new ProcessStartInfo("git")
{
    WorkingDirectory = repositoryRoot,
    RedirectStandardOutput = true,
    UseShellExecute = false
};
processStart.ArgumentList.Add("ls-files");
processStart.ArgumentList.Add("--cached");
processStart.ArgumentList.Add("--others");
processStart.ArgumentList.Add("--exclude-standard");
processStart.ArgumentList.Add("-z");
using var gitProcess = Process.Start(processStart)!;
var trackedOutput = gitProcess.StandardOutput.ReadToEnd();
gitProcess.WaitForExit();
if (gitProcess.ExitCode != SuccessExitCode)
{
    throw new InvalidOperationException("Unable to enumerate repository sources.");
}

var paths = trackedOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries);
var exceptionPath = Path.Combine(repositoryRoot, "eng", "code-conventions", "exceptions.json");
List<ConventionException> ReadExceptions()
{
    var manifestJson = File.ReadAllText(exceptionPath);
    var serializerOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };
    var registeredExceptions = JsonSerializer.Deserialize<List<ConventionException>>(manifestJson, serializerOptions) ?? throw new InvalidDataException("The convention exception manifest must be an array.");
    return registeredExceptions;
}

var exceptions = File.Exists(exceptionPath) ? ReadExceptions() : [];
var violationCount = EmptyPosition;
var fileCount = EmptyPosition;
foreach (var file in paths.Distinct(StringComparer.Ordinal))
{
    if (!ConventionSources.ShouldCheck(file))
    {
        continue;
    }

    var absolutePath = Path.Combine(repositoryRoot, file);
    if (!File.Exists(absolutePath))
    {
        continue;
    }

    fileCount += FirstPosition;
    var source = File.ReadAllText(absolutePath);
    var diagnostics = CSharpRules.Check(source, file, exceptions);
    foreach (var diagnostic in diagnostics)
    {
        Console.Error.WriteLine($"{file}({diagnostic.Line},{diagnostic.Column}): {diagnostic.Rule} {diagnostic.Message}");
        violationCount += FirstPosition;
    }
}

Console.WriteLine($"C# conventions: {fileCount} files, {violationCount} violations.");
return violationCount == EmptyPosition ? SuccessExitCode : ViolationExitCode;
internal sealed record ConventionException(string File, string Expression, string Reason);
internal sealed record ConventionDiagnostic(string Rule, int Line, int Column, string Message);
internal static class ConventionSources
{
    private static readonly string[] ExcludedDirectories =
    [
        "src/ThirdParty/",
        "src/SereinFlow.Mcp/",
        "src/SereinFlow.Mcp.Framework/",
        "src/SereinFlow.Mcp.Generator/",
        "tests/SereinFlow.Mcp.Tests/",
        "tests/SereinFlow.Mcp.Framework.Tests/",
        "tests/SereinFlow.Mcp.Generator.Tests/"
    ];

    internal static bool ShouldCheck(string path)
    {
        var normalized = path.Replace('\\', '/');
        var isCSharp = normalized.EndsWith(".cs", StringComparison.Ordinal);
        var excluded = ExcludedDirectories.Any(directory =>
        {
            var matches = normalized.StartsWith(directory, StringComparison.Ordinal);
            return matches;
        });
        return isCSharp && !excluded;
    }
}
internal static class CSharpRules
{
    private const int PositionOffset = 1;
    private const int SingleVariable = 1;
    public static List<ConventionDiagnostic> Check(string source, string file, IReadOnlyList<ConventionException>? exceptions = null)
    {
        var options = new CSharpParseOptions(LanguageVersion.Preview);
        var tree = CSharpSyntaxTree.ParseText(source, options);
        var root = tree.GetRoot();
        var diagnostics = new List<ConventionDiagnostic>();
        var statementLines = new HashSet<int>();
        var syntaxDiagnostics = tree.GetDiagnostics();
        foreach (var syntaxDiagnostic in syntaxDiagnostics)
        {
            if (syntaxDiagnostic.Severity != DiagnosticSeverity.Error)
            {
                continue;
            }

            var location = tree.GetLineSpan(syntaxDiagnostic.Location.SourceSpan);
            var position = location.StartLinePosition;
            var message = syntaxDiagnostic.GetMessage();
            var diagnostic = new ConventionDiagnostic("PARSE001", position.Line + PositionOffset, position.Character + PositionOffset, message);
            diagnostics.Add(diagnostic);
        }

        if (diagnostics.Count > 0)
        {
            return diagnostics;
        }
        var exceptionExpressions = new HashSet<string>(StringComparer.Ordinal);
        if (exceptions is not null)
        {
            foreach (var exception in exceptions)
            {
                if (exception.File != file || string.IsNullOrWhiteSpace(exception.Reason))
                {
                    continue;
                }

                var expression = SyntaxFactory.ParseExpression(exception.Expression);
                var normalized = Normalize(expression);
                exceptionExpressions.Add(normalized);
            }
        }

        Visit(root);
        return diagnostics;
        void Report(string rule, SyntaxNode node, string message)
        {
            var position = tree.GetLineSpan(node.Span).StartLinePosition;
            var conventionDiagnostic = new ConventionDiagnostic(rule, position.Line + PositionOffset, position.Character + PositionOffset, message);
            diagnostics.Add(conventionDiagnostic);
        }

        void Visit(SyntaxNode node)
        {
            if (node is StatementSyntax and not (BlockSyntax or EmptyStatementSyntax))
            {
                var statementLine = tree.GetLineSpan(node.Span).StartLinePosition.Line;
                if (!statementLines.Add(statementLine))
                {
                    Report("LINE001", node, "Put each statement on a separate line.");
                }
            }
            if (node is AnonymousFunctionExpressionSyntax)
            {
                var normalized = Normalize(node);
                if (exceptionExpressions.Contains(normalized))
                {
                    return;
                }
            }

            if (node is VariableDeclarationSyntax declaration && declaration.Variables.Count > SingleVariable)
            {
                Report("DECL001", node, "Declare each variable separately.");
            }

            if (file.Replace('\\', '/').StartsWith("src/", StringComparison.Ordinal)
                && node is VariableDeclaratorSyntax { Parent.Parent: LocalDeclarationStatementSyntax } variable
                && Regex.IsMatch(variable.Identifier.ValueText, @"^(?:(?:argument|evaluatedValue|conditionalValue|optionalReceiver|optionalResult|result)\d+|\w+Result\d+)$", RegexOptions.CultureInvariant))
            {
                Report("NAME001", node, "Remove forwarding temporaries or name the variable for its purpose instead of numbering it.");
            }

            if (node is IfStatementSyntax condition)
            {
                if (condition.Statement is not BlockSyntax)
                {
                    Report("FLOW001", node, "Use braces for every conditional branch.");
                }

                if (condition.Else is { } alternative)
                {
                    if (alternative.Statement is not (BlockSyntax or IfStatementSyntax))
                    {
                        Report("FLOW001", alternative, "Use braces for every else branch.");
                    }

                    var previousLine = tree.GetLineSpan(condition.Statement.Span).EndLinePosition.Line;
                    var elseLine = tree.GetLineSpan(alternative.ElseKeyword.Span).StartLinePosition.Line;
                    if (previousLine == elseLine)
                    {
                        Report("FLOW002", alternative, "Place else on a separate line.");
                    }
                }
            }

            var statements = node switch
            {
                BlockSyntax block => block.Statements,
                SwitchSectionSyntax section => section.Statements,
                _ => default
            };
            CheckStatementLines(statements);
            if (node is CompilationUnitSyntax compilation)
            {
                var globalMembers = compilation.Members.OfType<GlobalStatementSyntax>();
                var globalStatements = globalMembers.Select(static member => member.Statement);
                CheckStatementLines(globalStatements);
            }

            foreach (var child in node.ChildNodes())
            {
                Visit(child);
            }
        }

        void CheckStatementLines(IEnumerable<StatementSyntax> statements)
        {
            var previousStartLine = -PositionOffset;
            var previousEndLine = -PositionOffset;
            foreach (var statement in statements)
            {
                var span = tree.GetLineSpan(statement.Span);
                var startLine = span.StartLinePosition.Line;
                if (startLine == previousEndLine && startLine != previousStartLine)
                {
                    Report("LINE001", statement, "Put each statement on a separate line.");
                }
                previousStartLine = startLine;
                previousEndLine = span.EndLinePosition.Line;
            }
        }

    }

    private static string Normalize(SyntaxNode node)
    {
        var tokens = node.DescendantTokens();
        var selectResult = tokens.Select(static token => token.Text);
        var text = string.Join(" ", selectResult);
        return text;
    }
}

internal static class ConventionTests
{
    private const int FailureExitCode = 1;
    private const int SuccessExitCode = 0;
    public static int Run()
    {
        var excludedSources = new[]
        {
            "src/SereinFlow.Mcp/Tools/Example.cs",
            "src/SereinFlow.Mcp.Framework/Hosting/Example.cs",
            "src/SereinFlow.Mcp.Generator/Models/Example.cs",
            "tests/SereinFlow.Mcp.Tests/Example.cs",
            "tests/SereinFlow.Mcp.Framework.Tests/Example.cs",
            "tests\\SereinFlow.Mcp.Generator.Tests\\Example.cs"
        };
        var checkedSources = new[]
        {
            "src/SereinFlow.Mcp.Other/Example.cs",
            "tests/SereinFlow.Mcp.Tests.Other/Example.cs",
            "src/SereinFlow.Application/Example.cs",
            "tests/SereinFlow.ArchitectureTests/Example.cs"
        };
        var excludedWereChecked = excludedSources.Any(ConventionSources.ShouldCheck);
        var includedWereSkipped = checkedSources.Any(path =>
        {
            var checkedSource = ConventionSources.ShouldCheck(path);
            return !checkedSource;
        });
        if (excludedWereChecked || includedWereSkipped)
        {
            Console.Error.WriteLine("MCP convention exclusions must match complete source and test directories.");
            return FailureExitCode;
        }

        var invalidCases = new Dictionary<string, string>
        {
            ["PARSE001"] = "class C { void M() { var value = ; } }",
            ["DECL001"] = "class C { void M() { int first = 1, second = 2; } }",
            ["FLOW001"] = "class C { void M() { if (enabled) Run(); } }",
            ["FLOW002"] = "class C { void M() { if (enabled) { Run(); } else { Stop(); } } }",
            ["LINE001"] = "class C { void M() { Run(); Stop(); } }"
        };
        foreach (var testCase in invalidCases)
        {
            var diagnostics = CSharpRules.Check(testCase.Value, "test.cs");
            var found = diagnostics.Exists(diagnostic => diagnostic.Rule == testCase.Key);
            if (!found)
            {
                Console.Error.WriteLine($"Missing expected diagnostic {testCase.Key}.");
                return FailureExitCode;
            }
        }

        const string validSource = """
            class C
            {
                object M()
                {
                    var value = Source();
                    var result = Next(value);
                    return result;
                }
                void Configure()
                {
                    Register(() =>
                    {
                        var result = Create();
                        return result;
                    });
                }
            }
            """;
        var validDiagnostics = CSharpRules.Check(validSource, "test.cs");
        if (validDiagnostics.Count != SuccessExitCode)
        {
            Console.Error.WriteLine("Valid code produced convention diagnostics.");
            return FailureExitCode;
        }

        const string expressionSource = "class C { void M() { Query(row => { if (row.Enabled) Run(); }); } }";
        const string queryExpression = "row => { if (row.Enabled) Run(); }";
        var unregisteredDiagnostics = CSharpRules.Check(expressionSource, "test.cs");
        var reportsUnbracedBranch = unregisteredDiagnostics.Exists(static diagnostic => diagnostic.Rule == "FLOW001");
        if (!reportsUnbracedBranch)
        {
            Console.Error.WriteLine("An unregistered callback with an unbraced branch escaped checking.");
            return FailureExitCode;
        }

        var exemption = new ConventionException("test.cs", queryExpression, "The query provider requires an expression tree.");
        ConventionException[] exemptions = [exemption];
        var exemptedDiagnostics = CSharpRules.Check(expressionSource, "test.cs", exemptions);
        var otherFileDiagnostics = CSharpRules.Check(expressionSource, "other.cs", exemptions);
        if (exemptedDiagnostics.Count != SuccessExitCode || otherFileDiagnostics.Count == SuccessExitCode)
        {
            Console.Error.WriteLine("Expression exceptions must apply only to the registered expression and file.");
            return FailureExitCode;
        }

        const string awaitedSource = "class C { async Task<object> M() { return await Read(); } }";
        const string constructorSource = "class C { void M() { var value = new Container(Create()); } }";
        var awaitedDiagnostics = CSharpRules.Check(awaitedSource, "test.cs");
        var constructorDiagnostics = CSharpRules.Check(constructorSource, "test.cs");
        var permitsAwaitedReturn = awaitedDiagnostics.Count == SuccessExitCode;
        var permitsConstructorNesting = constructorDiagnostics.Count == SuccessExitCode;
        if (!permitsAwaitedReturn || !permitsConstructorNesting)
        {
            Console.Error.WriteLine("Awaited returns and nested constructors must remain natural expressions.");
            return FailureExitCode;
        }

        const string optionalSource = "class C { void M() { var value = Read()?.Trim(); } }";
        const string deconstructionSource = "class C { void M() { (var first, var second) = Read(); } }";
        const string globalSource = "Read(); Write();";
        const string initializerSource = "class C { C() : this(Create()) { } }";
        const string nestedStatementSource = "class C { void M() { if (enabled) { Run(); } } }";
        const string sharedEndSource = "class C { void M() { var first = Read(\n value); var second = value; } }";
        var optionalDiagnostics = CSharpRules.Check(optionalSource, "test.cs");
        var deconstructionDiagnostics = CSharpRules.Check(deconstructionSource, "test.cs");
        var globalDiagnostics = CSharpRules.Check(globalSource, "test.cs");
        var initializerDiagnostics = CSharpRules.Check(initializerSource, "test.cs");
        var nestedStatementDiagnostics = CSharpRules.Check(nestedStatementSource, "test.cs");
        var sharedEndDiagnostics = CSharpRules.Check(sharedEndSource, "test.cs");
        var permitsOptionalChain = optionalDiagnostics.Count == SuccessExitCode;
        var permitsDeconstruction = deconstructionDiagnostics.Count == SuccessExitCode;
        var reportsGlobalStatements = globalDiagnostics.Exists(static diagnostic => diagnostic.Rule == "LINE001");
        var permitsInitializerNesting = initializerDiagnostics.Count == SuccessExitCode;
        var reportsNestedStatements = nestedStatementDiagnostics.Exists(static diagnostic => diagnostic.Rule == "LINE001");
        var reportsSharedEnd = sharedEndDiagnostics.Exists(static diagnostic => diagnostic.Rule == "LINE001");
        if (!permitsOptionalChain || !permitsDeconstruction || !reportsGlobalStatements || !permitsInitializerNesting || !reportsNestedStatements || !reportsSharedEnd)
        {
            Console.Error.WriteLine("Optional chains, deconstruction and top-level statements must be checked.");
            return FailureExitCode;
        }

        var expressionAndTupleCases = new (string Source, string? ExpectedRule)[]
        {
            ("object M() => Create();", null),
            ("async Task<object> M() => await Read();", null),
            ("object Value => Create();", null),
            ("void M()\n{\nvar (first, second) = Read();\n}", null),
            ("void M()\n{\n(int first, string second) = Read();\n}", null),
            ("void M()\n{\nvar (first, (second, _)) = Read();\n}", null),
            ("void M()\n{\nforeach (var (first, second) in items)\n{\nUse(first, second);\n}\n}", null),
            ("object M() => Outer(Inner());", null),
            ("void M()\n{\nvar (first, second) = Outer(Inner());\n}", null),
            ("void M()\n{\nint first = 1, second = 2;\n}", "DECL001")
        };
        foreach (var testCase in expressionAndTupleCases)
        {
            var source = "class C\n{\n" + testCase.Source + "\n}";
            var diagnostics = CSharpRules.Check(source, "test.cs");
            var expectedRule = testCase.ExpectedRule;
            var passed = expectedRule is null
                ? diagnostics.Count == SuccessExitCode
                : diagnostics.Exists(diagnostic => diagnostic.Rule == expectedRule);
            if (!passed)
            {
                Console.Error.WriteLine($"Expression body or tuple convention regression: {testCase.Source}");
                return FailureExitCode;
            }
        }

        var linqCases = new (string Source, string? ExpectedRule)[]
        {
            ("var result = items.Where(x => x > 0).OrderBy(x => x).Take(2).ToArray();", null),
            ("var result = items.Select(x => x).ToHashSet();", null),
            ("var result = items.AsQueryable().Where(x => x > 0).Select(x => x).ToArray();", null),
            ("var result = items.Select(x => Transform(x)).ToArray();", null),
            ("var result = items.Where(x => x > Limit()).ToArray();", null),
            ("var result = items.Take(Limit()).ToArray();", null),
            ("var result = items.Select(x => {\nOuter(Transform(x));\nreturn x;\n}).ToArray();", null),
            ("var result = Read().Where(x => x > 0).ToArray();", null),
            ("var result = new Custom().Where().ToArray();", null),
            ("var result = items.Where(x => x > 0).ToArray().Clone();", null),
            ("var result = (await ReadAsync()).OrderBy(x => x).ToArray();", null),
            ("var result = Enum.GetValues<DayOfWeek>().ToHashSet();", null),
            ("var result = element.EnumerateArray().Select(x => x).ToArray();", null),
            ("var result = Read()?.Where(x => x > 0).ToArray();", null),
            ("var result = Source().Next(Transform(1));", null),
            ("var result = items.Select(x => Source().Next()).ToArray();", null)
        };
        foreach (var testCase in linqCases)
        {
            var linqSource = "using System.Linq;\nclass C {\nvoid Outer(int x) { }\nint Limit() {\nreturn 1;\n}\nint Transform(int x) {\nreturn x;\n}\nint[] Read() {\nreturn [];\n}\nvoid M(int[] items) {\n" + testCase.Source + "\n}\n}\nclass Custom {\npublic Custom Where() {\nreturn this;\n}\npublic Custom ToArray() {\nreturn this;\n}\n}";
            var linqDiagnostics = CSharpRules.Check(linqSource, "test.cs");
            var expectedRule = testCase.ExpectedRule;
            var passed = expectedRule is null
                ? linqDiagnostics.Count == SuccessExitCode
                : linqDiagnostics.Exists(diagnostic => diagnostic.Rule == expectedRule);
            if (!passed)
            {
                Console.Error.WriteLine($"Chain convention regression: {testCase.Source}");
                return FailureExitCode;
            }
        }

        const string numberedTemporarySource = """
            class C
            {
                object M()
                {
                    var argument4 = Read();
                    return argument4;
                }
            }
            """;
        var numberedTemporaryDiagnostics = CSharpRules.Check(numberedTemporarySource, "src/Example.cs");
        if (!numberedTemporaryDiagnostics.Exists(static diagnostic => diagnostic.Rule == "NAME001"))
        {
            Console.Error.WriteLine("Numbered forwarding temporaries must be rejected in production sources.");
            return FailureExitCode;
        }

        const string meaningfulNumbersSource = """
            class C
            {
                void M()
                {
                    var sha256 = Hash();
                    var isV2 = IsCurrentVersion();
                    var packageBase64 = Encode();
                    Send(sha256, isV2, packageBase64);
                }
            }
            """;
        if (CSharpRules.Check(meaningfulNumbersSource, "src/Example.cs").Count != SuccessExitCode)
        {
            Console.Error.WriteLine("Algorithm, encoding and version numbers must remain valid names.");
            return FailureExitCode;
        }

        Console.WriteLine("C# convention self-tests passed.");
        return SuccessExitCode;
    }
}
