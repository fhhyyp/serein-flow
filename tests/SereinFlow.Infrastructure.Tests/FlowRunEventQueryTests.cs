using System.Diagnostics;
using SereinFlow.Domain;
using SereinFlow.Infrastructure.Persistence;
using SqlSugar;
using Xunit.Abstractions;

namespace SereinFlow.Infrastructure.Tests;

public sealed class FlowRunEventQueryTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(1_000)]
    [InlineData(10_000)]
    public async Task LastSequenceUsesScalarAggregateAndExistingIndex(int count)
    {
        var operationResult11 = Path.GetTempPath();
        var operationResult12 = $"event-query-{Guid.NewGuid():N}.db";
        var path = Path.Combine(operationResult11, operationResult12);
        try
        {
            var operationResult1 = new SqliteDatabaseOptions(path);
            using var database = new SqliteDatabase(operationResult1);
            database.Initialize();
            var store = new SqlSugarFlowRunEventStore(database);
            var runId = Guid.NewGuid();
            var runKey = runId.ToString("D");
            var operationResult2 = await store.GetLastSequenceAsync(runId);
            Assert.Equal(0, operationResult2);
            database.Execute("INSERT INTO Projects (Id, Name, Version, Status, CreatedAt, UpdatedAt) VALUES ('p', 'query', 1, 'Ready', '', '')");
            database.Execute("INSERT INTO FlowDefinitions (Id, ProjectId, Version, DefinitionJson, Checksum) VALUES ('f', 'p', 1, '{}', '')");
            var operationResult3 = new SugarParameter("@run", runKey);
            database.Execute("INSERT INTO FlowRuns (Id, FlowId, FlowVersion, Status) VALUES (@run, 'f', 1, 'Running')", operationResult3);
            var operationResult4 = new SugarParameter("@count", count);
            var operationResult5 = new SugarParameter("@run", runKey);
            // Seed in descending order and include a sizeable payload to expose accidental full-row reads.
            database.Execute("""
                        WITH RECURSIVE seq(n) AS (SELECT @count UNION ALL SELECT n - 1 FROM seq WHERE n > 1)
                        INSERT INTO FlowRunEvents (RunId, Sequence, Timestamp, Type, PayloadJson)
                        SELECT @run, n, '2026-10-20T00:00:00Z', 'log', hex(zeroblob(512)) FROM seq
                        """, operationResult4, operationResult5);
            string? sql = null;
            database.Client.Aop.OnLogExecuting = (statement, _) => sql = statement;
            var watch = Stopwatch.StartNew();
            var operationResult6 = await store.GetLastSequenceAsync(runId);
            Assert.Equal(count, operationResult6);
            watch.Stop();
            output.WriteLine($"{count} events: {watch.Elapsed.TotalMilliseconds:F3} ms; SQL: {sql}");
            Assert.Contains("MAX(", sql!, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PayloadJson", sql!, StringComparison.OrdinalIgnoreCase);
            var operationResult7 = new SugarParameter("@run", runKey);
            var plan = database.Query<QueryPlan>("EXPLAIN QUERY PLAN SELECT MAX(Sequence) FROM FlowRunEvents WHERE RunId = @run", operationResult7);
            Assert.Contains(plan, row =>
            {
                var operationResult1 = row.Detail.Contains("COVERING INDEX", StringComparison.OrdinalIgnoreCase);
                return operationResult1;
            });
            var missingRunId = Guid.NewGuid();
            var operationResult8 = await store.GetLastSequenceAsync(missingRunId);
            Assert.Equal(0, operationResult8);
            FlowRunEvent[] operationResult9 = [new FlowRunEvent(runId, count, DateTimeOffset.UtcNow, "duplicate", null, "{}")];
            await store.AppendAsync(operationResult9);
            var operationResult10 = await store.GetLastSequenceAsync(runId);
            Assert.Equal(count, operationResult10);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            {
                var operationResult2 = store.GetLastSequenceAsync(runId, cancellation.Token);
                return operationResult2;
            });
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var file in new[]
            {
                path,
                path + "-wal",
                path + "-shm"
            }

            )
            {
                try
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                }
                catch (IOException)
                {
                // Another parallel SQLite test may still be releasing a pooled handle.
                }
            }
        }
    }

    private sealed class QueryPlan
    {
        public string Detail { get; set; } = string.Empty;
    }
}
