using ClosedXML.Excel;

namespace ATIP.Application.Features.Scenarios.Import;

/// <summary>
/// Builds the downloadable .xlsx import template. The header row uses the primary column names
/// understood by <see cref="ScenarioImportParser"/>, and the sheet is pre-filled with worked
/// examples so a user can replace the rows rather than guess the shape.
/// </summary>
public static class ScenarioImportTemplate
{
    public const string FileName = "atip-test-case-import-template.xlsx";

    public const string ContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly string[] Headers =
    [
        "case_id",
        "title",
        "preconditions",
        "step_order",
        "step",
        "step_expected",
        "expected_result",
    ];

    /// <summary>One row per step; rows sharing a case_id are grouped into a single test case.</summary>
    private static readonly string[][] SampleRows =
    [
        ["TC-001", "Standard user can log in with valid credentials", "The store is reachable and the standard_user account is active", "1", "Open the store home page", "The login form is displayed", "The user reaches the inventory page and sees the product list"],
        ["TC-001", "", "", "2", "Enter username \"standard_user\"", "The username field contains standard_user", ""],
        ["TC-001", "", "", "3", "Enter password \"secret_sauce\"", "The password is masked", ""],
        ["TC-001", "", "", "4", "Click Login", "The inventory page is shown", ""],
        ["TC-002", "Locked-out user is refused with a clear message", "The locked_out_user account exists and is locked", "1", "Open the store home page", "The login form is displayed", "Login is refused and an explanatory error is shown"],
        ["TC-002", "", "", "2", "Enter username \"locked_out_user\" and password \"secret_sauce\"", "The credentials are accepted into the form", ""],
        ["TC-002", "", "", "3", "Click Login", "An error states the user has been locked out", ""],
        ["TC-003", "Adding a product updates the cart badge", "A user is signed in on the inventory page", "1", "Click Add to cart on the first product", "The button changes to Remove", "The cart badge shows 1 and the item appears in the cart"],
        ["TC-003", "", "", "2", "Read the cart badge in the header", "The badge shows 1", ""],
        ["TC-003", "", "", "3", "Open the cart", "The added product is listed with quantity 1", ""],
    ];

    public static byte[] Build()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Test cases");

        for (var c = 0; c < Headers.Length; c++)
        {
            var cell = sheet.Cell(1, c + 1);
            cell.Value = Headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#EDE9FE");
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        for (var r = 0; r < SampleRows.Length; r++)
        {
            for (var c = 0; c < SampleRows[r].Length; c++)
            {
                sheet.Cell(r + 2, c + 1).Value = SampleRows[r][c];
            }
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Column(2).Width = 46;
        sheet.Column(3).Width = 40;
        sheet.Column(5).Width = 46;
        sheet.Column(6).Width = 40;
        sheet.Column(7).Width = 46;
        sheet.Columns().AdjustToContents(1, 1);
        sheet.Range(1, 1, SampleRows.Length + 1, Headers.Length).Style.Alignment.WrapText = true;

        AddInstructionsSheet(workbook);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void AddInstructionsSheet(XLWorkbook workbook)
    {
        var sheet = workbook.Worksheets.Add("How to use");
        var lines = new (string Label, string Text)[]
        {
            ("Layout", "One row per STEP. Rows that share the same case_id form a single test case, in step_order sequence."),
            ("case_id", "Optional. Your own reference (e.g. TC-001 or a Jira key). Rows are grouped by it, and it is kept on the scenario as an \"external:\" tag. Without it, rows are grouped by title instead."),
            ("title", "Required on the first row of each case. Leave blank on that case's remaining rows."),
            ("preconditions", "Optional. Set-up that must be true before step 1."),
            ("step_order", "Optional. Defaults to the row order within the case."),
            ("step", "Required. The action to perform. A row with no step is ignored."),
            ("step_expected", "Optional. What should be true right after this step."),
            ("expected_result", "Optional. The overall outcome for the whole case."),
            (string.Empty, string.Empty),
            ("Accepted names", "Headers are case-insensitive and spaces become underscores. Alternatives are accepted: title/name/summary/scenario, step/action/test_step, step_expected/expected_step, case_id/id/key/reference."),
            ("File types", ".xlsx and .csv are both supported. Only the first worksheet is read."),
            ("Suites", "During import you can add every imported case to a new suite, append them to an existing suite, or import them with no suite at all."),
            ("Note", "Importing ADDS test cases; it never removes or overwrites the ones already in the project."),
        };

        sheet.Cell(1, 1).Value = "Test case import template";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;

        for (var i = 0; i < lines.Length; i++)
        {
            sheet.Cell(i + 3, 1).Value = lines[i].Label;
            sheet.Cell(i + 3, 1).Style.Font.Bold = true;
            sheet.Cell(i + 3, 2).Value = lines[i].Text;
            sheet.Cell(i + 3, 2).Style.Alignment.WrapText = true;
        }

        sheet.Column(1).Width = 18;
        sheet.Column(2).Width = 110;
    }
}
