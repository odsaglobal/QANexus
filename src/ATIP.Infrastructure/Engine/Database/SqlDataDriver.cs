using System.Data.Common;
using System.Text;
using System.Text.Json;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Engine.Contracts;
using ATIP.Application.Engine.Model;
using ATIP.Application.Features.Explorer.Agent;
using ATIP.Domain.Enums;
using ATIP.Infrastructure.Exploration;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Npgsql;

namespace ATIP.Infrastructure.Engine.Database;

/// <summary>
/// Runs SQL against a project's registered databases so a scenario can seed fixtures, clean up
/// after itself, and — most importantly — verify that what the UI claimed actually persisted.
/// </summary>
/// <remarks>
/// A UI-only test can only assert on what the application chose to render, which means it passes
/// happily when the confirmation screen is right and the write silently failed. Checking the row
/// closes that gap, which is why this driver exists at all.
/// </remarks>
public sealed class SqlDataDriver : ITestDriver
{
    private static readonly HashSet<TestActionKind> Supported =
    [
        TestActionKind.SqlQuery, TestActionKind.SqlExecute, TestActionKind.Assert, TestActionKind.Capture
    ];

    private readonly IApplicationDbContext _db;
    private readonly ISecretProtector _protector;
    private readonly ILogger<SqlDataDriver> _logger;

    /// <summary>Rows returned by the most recent query, kept so a following Assert can read them.</summary>
    private List<Dictionary<string, string?>> _lastRows = [];
    private string? _lastSummary;
    private int _lastAffected;

    public SqlDataDriver(IApplicationDbContext db, ISecretProtector protector, ILogger<SqlDataDriver> logger)
    {
        _db = db;
        _protector = protector;
        _logger = logger;
    }

    public TestPlatform Platform => TestPlatform.Database;

    public bool Supports(TestActionKind kind) => Supported.Contains(kind);

    public Task OpenAsync(RunContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task<ActionResult> ExecuteAsync(TestAction action, RunContext context, CancellationToken cancellationToken)
    {
        if (!Supports(action.Kind))
        {
            return ActionResult.NotSupported($"The database driver cannot perform '{action.Kind}'.");
        }

        try
        {
            return action.Kind switch
            {
                TestActionKind.SqlQuery => await QueryAsync(action, context, cancellationToken),
                TestActionKind.SqlExecute => await ExecuteNonQueryAsync(action, context, cancellationToken),
                TestActionKind.Assert => Assert(action),
                TestActionKind.Capture => Capture(action),
                _ => ActionResult.NotSupported($"The database driver cannot perform '{action.Kind}'.")
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbException ex)
        {
            // A SQL error is a test failure with a useful message, not an engine crash.
            _logger.LogWarning(ex, "Database action {Kind} failed.", action.Kind);
            return ActionResult.Fail($"SQL error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Database action {Kind} failed.", action.Kind);
            return ActionResult.Fail($"{action.Kind} failed: {ex.Message}");
        }
    }

    public Task<EvidenceCapture?> CaptureEvidenceAsync(CancellationToken cancellationToken) =>
        Task.FromResult<EvidenceCapture?>(_lastSummary is null
            ? null
            : new EvidenceCapture { Title = _lastSummary, Text = Render(_lastRows) });

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ── Execution ───────────────────────────────────────────────────────────────────────────

    private async Task<ActionResult> QueryAsync(TestAction action, RunContext context, CancellationToken ct)
    {
        var resolved = await OpenConnectionAsync(action, context, ct);
        if (resolved.Error is not null)
        {
            return ActionResult.Fail(resolved.Error);
        }

        await using var connection = resolved.Connection!;
        await using var command = CreateCommand(connection, action, context, resolved.TimeoutSeconds);

        _lastRows = [];

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i, ct)
                    ? null
                    : Convert.ToString(reader.GetValue(i), System.Globalization.CultureInfo.InvariantCulture);
            }

            _lastRows.Add(row);

            // Result sets are evidence, not data processing. Cap them so a missing WHERE clause
            // costs a truncated log instead of the run's memory.
            if (_lastRows.Count >= 500)
            {
                break;
            }
        }

        _lastSummary = $"{_lastRows.Count} row(s)";
        return ActionResult.Ok($"Query returned {_lastSummary}", Render(_lastRows));
    }

    private async Task<ActionResult> ExecuteNonQueryAsync(TestAction action, RunContext context, CancellationToken ct)
    {
        var resolved = await OpenConnectionAsync(action, context, ct);
        if (resolved.Error is not null)
        {
            return ActionResult.Fail(resolved.Error);
        }

        if (resolved.ReadOnly)
        {
            return ActionResult.Fail(
                $"Connection '{resolved.Name}' is registered read-only, so it cannot run a write statement.");
        }

        await using var connection = resolved.Connection!;
        await using var command = CreateCommand(connection, action, context, resolved.TimeoutSeconds);

        _lastAffected = await command.ExecuteNonQueryAsync(ct);
        _lastRows = [];
        _lastSummary = $"{_lastAffected} row(s) affected";

        return ActionResult.Ok(_lastSummary, _lastAffected.ToString());
    }

    private static DbCommand CreateCommand(DbConnection connection, TestAction action, RunContext context, int timeoutSeconds)
    {
        var command = connection.CreateCommand();
        command.CommandText = context.Resolve(action.Value) ?? string.Empty;
        command.CommandTimeout = timeoutSeconds;

        // Parameters are bound, never interpolated. Test SQL routinely embeds values that came from
        // the application under test, and string-building them into the statement would make the
        // test suite itself an injection vector.
        foreach (var (name, value) in ParseParameters(context.Resolve(action.Option(ActionOptionNames.SqlParameters))))
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = (object?)value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        return command;
    }

    private static IEnumerable<KeyValuePair<string, string?>> ParseParameters(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            yield break;
        }

        JsonElement root;
        try
        {
            root = JsonDocument.Parse(json).RootElement;
        }
        catch (JsonException)
        {
            yield break;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        foreach (var property in root.EnumerateObject())
        {
            yield return new KeyValuePair<string, string?>(
                property.Name,
                property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : property.Value.ToString());
        }
    }

    // ── Connections ─────────────────────────────────────────────────────────────────────────

    private readonly record struct ResolvedConnection(
        DbConnection? Connection,
        string? Name,
        bool ReadOnly,
        int TimeoutSeconds,
        string? Error);

    private async Task<ResolvedConnection> OpenConnectionAsync(TestAction action, RunContext context, CancellationToken ct)
    {
        var name = action.Option(ActionOptionNames.ConnectionName);
        if (string.IsNullOrWhiteSpace(name))
        {
            return new ResolvedConnection(null, null, false, 0, "The step does not say which database connection to use.");
        }

        if (context.EnvironmentId is not { } environmentId)
        {
            return new ResolvedConnection(null, name, false, 0, "Database steps need the run to be bound to an environment.");
        }

        var record = await _db.DataConnections
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                c => c.TenantId == context.TenantId
                    && c.EnvironmentId == environmentId
                    && c.Name == name
                    && !c.IsDeleted,
                ct);

        if (record is null)
        {
            return new ResolvedConnection(null, name, false, 0,
                $"No database connection named '{name}' is registered for this environment.");
        }

        var connectionString = _protector.Unprotect(
            new ProtectedSecret(record.EncryptedConnectionString, record.EncryptionNonce, record.KeyId));

        DbConnection connection = record.Provider switch
        {
            DataProviderKind.SqlServer => new SqlConnection(connectionString),
            DataProviderKind.MySql => new MySqlConnection(connectionString),
            _ => new NpgsqlConnection(connectionString)
        };

        await connection.OpenAsync(ct);
        return new ResolvedConnection(connection, name, record.ReadOnly, record.CommandTimeoutSeconds, null);
    }

    // ── Assertions and capture ──────────────────────────────────────────────────────────────

    private ActionResult Assert(TestAction action)
    {
        if (action.Assertion is null)
        {
            return ActionResult.Fail("Assert action has no compiled assertion.");
        }

        var assertion = action.Assertion;
        var label = string.IsNullOrWhiteSpace(assertion.Label) ? "Database assertion" : assertion.Label.Trim();
        var op = (assertion.Operator ?? "contains").Trim().ToLowerInvariant();

        // Selector names the column to read; empty means the whole row, rendered.
        var values = ReadColumn(assertion.Selector);

        if (op is "exists" or "not_exists")
        {
            var present = _lastRows.Count > 0;
            var existsCheck = new StepCheckResult
            {
                Label = label,
                Passed = op == "exists" ? present : !present,
                Expected = op == "exists" ? "at least one matching row" : "no matching rows",
                Actual = $"{_lastRows.Count} row(s)"
            };

            return ActionResult.Assertion(existsCheck.Passed, $"{label}: {existsCheck.Actual}", [existsCheck]);
        }

        if (op == "count_equals")
        {
            var wanted = int.TryParse(assertion.Expected, out var n) ? n : -1;
            var countCheck = new StepCheckResult
            {
                Label = label,
                Passed = wanted >= 0 && _lastRows.Count == wanted,
                Expected = $"{assertion.Expected} row(s)",
                Actual = $"{_lastRows.Count} row(s)"
            };

            return ActionResult.Assertion(countCheck.Passed, $"{label}: {countCheck.Actual}", [countCheck]);
        }

        var (passed, actual, unresolved) = AssertionEvaluator.Compare(op, values, assertion.Expected);
        var check = new StepCheckResult
        {
            Label = label,
            Passed = passed,
            Unresolved = unresolved,
            Expected = assertion.Expected ?? string.Empty,
            Actual = actual
        };

        return ActionResult.Assertion(passed, passed ? $"{label}: passed" : $"{label}: {actual}", [check]);
    }

    private ActionResult Capture(TestAction action)
    {
        var column = action.Option(ActionOptionNames.CaptureExpression) ?? action.Value;
        var values = ReadColumn(column);

        return values.Count == 0
            ? ActionResult.Fail($"No value to capture — the last query returned no '{column}'.")
            : ActionResult.Ok($"Captured '{values[0]}'", values[0]);
    }

    private IReadOnlyList<string> ReadColumn(string? column)
    {
        if (_lastRows.Count == 0)
        {
            return [];
        }

        if (string.IsNullOrWhiteSpace(column))
        {
            // No column named: fall back to the first column, which is what a scalar query means.
            return _lastRows.Select(r => r.Values.FirstOrDefault() ?? string.Empty).ToList();
        }

        return _lastRows
            .Where(r => r.ContainsKey(column))
            .Select(r => r[column] ?? string.Empty)
            .ToList();
    }

    private static string Render(List<Dictionary<string, string?>> rows)
    {
        if (rows.Count == 0)
        {
            return "(no rows)";
        }

        var builder = new StringBuilder();
        builder.AppendLine(string.Join(" | ", rows[0].Keys));

        foreach (var row in rows.Take(20))
        {
            builder.AppendLine(string.Join(" | ", row.Values.Select(v => v ?? "NULL")));
        }

        if (rows.Count > 20)
        {
            builder.AppendLine($"… {rows.Count - 20} more row(s)");
        }

        return builder.ToString();
    }
}
