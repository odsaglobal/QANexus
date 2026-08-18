using System.Text;
using ClosedXML.Excel;
using ATIP.Application.Common.Models;

namespace ATIP.Application.Features.Scenarios.Import;

internal static class ScenarioImportParser
{
    public static IReadOnlyList<ImportedTestScenario> Parse(string fileName, byte[] content)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".csv" => ParseCsv(content),
            ".xlsx" => ParseXlsx(content),
            _ => throw new InvalidOperationException("Only .csv and .xlsx files are supported."),
        };
    }

    private static IReadOnlyList<ImportedTestScenario> ParseCsv(byte[] content)
    {
        var text = Encoding.UTF8.GetString(content);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd('\r'))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        if (lines.Count < 2)
        {
            return [];
        }

        var headers = SplitCsvLine(lines[0]);
        var rows = new List<Dictionary<string, string>>(capacity: Math.Max(0, lines.Count - 1));
        for (var i = 1; i < lines.Count; i++)
        {
            var cols = SplitCsvLine(lines[i]);
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < headers.Count; c++)
            {
                var key = NormalizeHeader(headers[c]);
                var value = c < cols.Count ? cols[c].Trim() : string.Empty;
                row[key] = value;
            }

            rows.Add(row);
        }

        return BuildScenarios(rows);
    }

    private static IReadOnlyList<ImportedTestScenario> ParseXlsx(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.First();

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        if (lastRow < 2 || lastCol < 1)
        {
            return [];
        }

        var headers = new List<string>();
        for (var c = 1; c <= lastCol; c++)
        {
            headers.Add(NormalizeHeader(sheet.Cell(1, c).GetString()));
        }

        var rows = new List<Dictionary<string, string>>();
        for (var r = 2; r <= lastRow; r++)
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var c = 1; c <= lastCol; c++)
            {
                row[headers[c - 1]] = sheet.Cell(r, c).GetString().Trim();
            }

            if (row.Values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            rows.Add(row);
        }

        return BuildScenarios(rows);
    }

    private static IReadOnlyList<ImportedTestScenario> BuildScenarios(IReadOnlyList<Dictionary<string, string>> rows)
    {
        var grouped = rows.GroupBy(r =>
        {
            var title = Pick(r, "title", "name", "testcase", "test_case", "test_case_title", "scenario", "summary");
            var reference = Pick(r, "case_id", "id", "reference", "test_case_id", "tc_id", "key");
            return string.IsNullOrWhiteSpace(reference) ? title : reference;
        }, StringComparer.OrdinalIgnoreCase);

        var scenarios = new List<ImportedTestScenario>();
        foreach (var group in grouped)
        {
            var first = group.First();
            var title = Pick(first, "title", "name", "testcase", "test_case", "test_case_title", "scenario", "summary");
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var preconditions = Pick(first, "preconditions", "precondition", "setup", "pre_conditions");
            var expectedResult = Pick(first, "expected_result", "expected", "expected_results", "case_expected", "overall_expected");
            var reference = Pick(first, "case_id", "id", "reference", "test_case_id", "tc_id", "key");

            var steps = group
                .Select((row, index) =>
                {
                    var action = Pick(row, "step", "step_action", "action", "instruction", "test_step", "step_description", "steps");
                    var stepExpected = Pick(row, "step_expected", "expected_step", "expected_result_step", "step_expected_result", "expected");
                    if (string.IsNullOrWhiteSpace(action))
                    {
                        return null;
                    }

                    var orderRaw = Pick(row, "step_order", "order", "step_no", "step_number", "sno", "s_no");
                    var order = int.TryParse(orderRaw, out var parsed) ? parsed : index + 1;
                    return new ImportedTestStep(order, action, stepExpected);
                })
                .Where(s => s is not null)
                .Cast<ImportedTestStep>()
                .OrderBy(s => s.Order)
                .ToList();

            if (steps.Count == 0)
            {
                continue;
            }

            scenarios.Add(new ImportedTestScenario(
                Title: title,
                Preconditions: NullIfEmpty(preconditions),
                ExpectedResult: NullIfEmpty(expectedResult),
                Steps: steps,
                ExternalReference: NullIfEmpty(reference)));
        }

        return scenarios;
    }

    private static string NormalizeHeader(string header) =>
        (header ?? string.Empty).Trim().ToLowerInvariant().Replace(" ", "_");

    private static string? Pick(IReadOnlyDictionary<string, string> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (row.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<string> SplitCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        values.Add(current.ToString());
        return values;
    }
}
