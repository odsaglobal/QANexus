using System.Globalization;
using System.Text.RegularExpressions;
using ATIP.Application.Features.Explorer.Agent;

namespace ATIP.Infrastructure.Exploration;

/// <summary>
/// Evaluates compiled step assertions against the live page by reading values and comparing them.
/// <para>
/// This is the half of verification that must never involve a language model: once an expectation has
/// been compiled into <c>{ Source, Selector, Operator, Expected }</c>, deciding whether it holds is
/// arithmetic and string comparison. A run using compiled assertions is therefore reproducible — the
/// same page always yields the same verdict — and costs nothing.
/// </para>
/// </summary>
public static class AssertionEvaluator
{
    /// <summary>Reads the page and decides one assertion. Never throws: a broken assertion fails its check.</summary>
    public static async Task<StepCheckResult> EvaluateAsync(
        PlaywrightBrowserService browser,
        StepAssertion assertion,
        CancellationToken ct = default)
    {
        var label = string.IsNullOrWhiteSpace(assertion.Label) ? DescribeExpectation(assertion) : assertion.Label.Trim();
        var source = (assertion.Source ?? "text").Trim().ToLowerInvariant();
        var op = (assertion.Operator ?? "contains").Trim().ToLowerInvariant();

        IReadOnlyList<string> values;

        switch (source)
        {
            case "url":
                values = new[] { browser.CurrentUrl ?? string.Empty };
                break;

            case "title":
                values = new[] { browser.CurrentTitle ?? string.Empty };
                break;

            default:
                var read = await browser.ReadValuesAsync(assertion.Selector ?? string.Empty, source, ct: ct);
                if (read is null)
                {
                    // The selector is malformed or the page could not be queried at all. That is a
                    // defect in the test rather than in the application, so it is reported distinctly.
                    return new StepCheckResult
                    {
                        Label = label,
                        Passed = false,
                        Unresolved = true,
                        Expected = DescribeExpectation(assertion),
                        Actual = $"could not read '{assertion.Selector}' from the page",
                    };
                }

                values = read;
                break;
        }

        // Existence is decided by the match count alone, before any value is inspected.
        if (op is "exists" or "not_exists")
        {
            var present = values.Count > 0;
            return new StepCheckResult
            {
                Label = label,
                Passed = op == "exists" ? present : !present,
                Expected = DescribeExpectation(assertion),
                Actual = present ? $"found {values.Count} match(es): {Summarise(values)}" : "not present on the page",
            };
        }

        if (op == "count_equals")
        {
            var wanted = TryParseNumber(assertion.Expected, out var n) ? n : double.NaN;
            return new StepCheckResult
            {
                Label = label,
                Passed = !double.IsNaN(wanted) && Math.Abs(values.Count - wanted) < 0.5,
                Expected = DescribeExpectation(assertion),
                Actual = $"{values.Count} match(es)",
            };
        }

        if (values.Count == 0)
        {
            return new StepCheckResult
            {
                Label = label,
                Passed = op == "not_contains" || op == "not_equals",
                Unresolved = op is not ("not_contains" or "not_equals"),
                Expected = DescribeExpectation(assertion),
                Actual = $"nothing on the page matched '{assertion.Selector}'",
            };
        }

        var (passed, actual, unresolved) = Compare(op, values, assertion.Expected);

        return new StepCheckResult
        {
            Label = label,
            Passed = passed,
            Unresolved = unresolved,
            Expected = DescribeExpectation(assertion),
            Actual = actual,
        };
    }

    /// <summary>
    /// The pure comparison half, kept free of any browser dependency so the operator semantics can be
    /// reasoned about and tested on their own.
    /// <para>
    /// <c>Unresolved</c> separates "the assertion is broken" from "the expectation was not met". Only the
    /// latter is a finding about the application; the former means the compiled test is out of date and
    /// must be recompiled, not reported as a defect.
    /// </para>
    /// </summary>
    internal static (bool Passed, string Actual, bool Unresolved) Compare(string op, IReadOnlyList<string> values, string? expected)
    {
        var wanted = (expected ?? string.Empty).Trim();

        switch (op)
        {
            case "equals":
                return (values.Any(v => string.Equals(v.Trim(), wanted, StringComparison.OrdinalIgnoreCase)), Summarise(values), false);

            case "not_equals":
                return (!values.Any(v => string.Equals(v.Trim(), wanted, StringComparison.OrdinalIgnoreCase)), Summarise(values), false);

            case "contains":
                return (values.Any(v => v.Contains(wanted, StringComparison.OrdinalIgnoreCase)), Summarise(values), false);

            case "not_contains":
                return (!values.Any(v => v.Contains(wanted, StringComparison.OrdinalIgnoreCase)), Summarise(values), false);

            case "matches":
                try
                {
                    var rx = new Regex(wanted, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
                    return (values.Any(rx.IsMatch), Summarise(values), false);
                }
                catch (ArgumentException)
                {
                    return (false, $"the pattern '{wanted}' is not a valid expression", true);
                }

            case "sorted_asc":
            case "sorted_desc":
            case "first_is_max":
            case "first_is_min":
                {
                    var (ordered, orderingActual) = CompareOrdering(op, values);
                    return (ordered, orderingActual, false);
                }

            case "gte":
            case "lte":
                {
                    if (!TryParseNumber(wanted, out var target))
                    {
                        return (false, $"'{wanted}' is not a number to compare against", true);
                    }

                    if (!TryParseNumber(values[0], out var observed))
                    {
                        // The selector resolved, but to something that is not a measurement. On sites
                        // whose class names are build-hashed, a stale selector routinely matches a
                        // DIFFERENT element rather than none at all — a product title where a result
                        // count used to be. Calling that an unmet expectation blames the application
                        // for a test that is merely out of date, so mark it unresolved and let the
                        // recompile path rebuild the assertion against the live page.
                        return (false, $"'{Truncate(values[0])}' is not a number, so there is nothing to compare", true);
                    }

                    var ok = op == "gte" ? observed >= target : observed <= target;
                    return (ok, Format(observed), false);
                }

            default:
                // An operator we do not implement must not quietly pass — that is how a green suite
                // stops meaning anything.
                return (false, $"unsupported assertion operator '{op}'", true);
        }
    }

    private static (bool Passed, string Actual) CompareOrdering(string op, IReadOnlyList<string> values)
    {
        if (values.Count < 2)
        {
            return (false, $"only {values.Count} value(s) found — not enough to judge ordering");
        }

        var numbers = new List<double>(values.Count);
        foreach (var value in values)
        {
            if (!TryParseNumber(value, out var number))
            {
                // Fall back to text ordering, which is still deterministic and is the right answer for
                // names; mixing the two would not be.
                return CompareTextOrdering(op, values);
            }

            numbers.Add(number);
        }

        var actual = Summarise(values);

        // Report the values as the page rendered them ("₹14,989"), not as the numbers they parsed to.
        // The reader is checking a screen, and "14989" makes them do the conversion themselves.
        var highest = values[numbers.IndexOf(numbers.Max())];
        var lowest = values[numbers.IndexOf(numbers.Min())];

        return op switch
        {
            "sorted_asc" => (IsSorted(numbers, ascending: true), actual),
            "sorted_desc" => (IsSorted(numbers, ascending: false), actual),
            "first_is_max" => (numbers[0] >= numbers.Max(), $"first is {Truncate(values[0])}, highest on the page is {Truncate(highest)}"),
            "first_is_min" => (numbers[0] <= numbers.Min(), $"first is {Truncate(values[0])}, lowest on the page is {Truncate(lowest)}"),
            _ => (false, actual),
        };
    }

    private static (bool Passed, string Actual) CompareTextOrdering(string op, IReadOnlyList<string> values)
    {
        var text = values.Select(v => v.Trim()).ToList();
        var actual = Summarise(values);

        return op switch
        {
            "sorted_asc" => (IsSortedText(text, ascending: true), actual),
            "sorted_desc" => (IsSortedText(text, ascending: false), actual),
            "first_is_max" => (text[0] == text.OrderByDescending(v => v, StringComparer.OrdinalIgnoreCase).First(), actual),
            "first_is_min" => (text[0] == text.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).First(), actual),
            _ => (false, actual),
        };
    }

    private static bool IsSorted(List<double> numbers, bool ascending)
    {
        for (var i = 1; i < numbers.Count; i++)
        {
            if (ascending ? numbers[i] < numbers[i - 1] : numbers[i] > numbers[i - 1])
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSortedText(List<string> values, bool ascending)
    {
        for (var i = 1; i < values.Count; i++)
        {
            var cmp = string.Compare(values[i], values[i - 1], StringComparison.OrdinalIgnoreCase);
            if (ascending ? cmp < 0 : cmp > 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Reads a number out of a rendered value such as "₹1,29,900" or "$24.99".
    /// <para>
    /// Deliberately strict: it accepts a value containing exactly ONE run of digits. "Sony WH-1000XM4"
    /// holds two runs and is rejected rather than being silently read as a price, because a comparison
    /// against a product code is worse than an honest failure to compare. Digit grouping is dropped
    /// without assuming a locale, so both "1,299" and the Indian "1,29,900" resolve correctly.
    /// </para>
    /// </summary>
    internal static bool TryParseNumber(string? value, out double number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var matches = NumericRunPattern.Matches(value);
        if (matches.Count != 1)
        {
            return false;
        }

        var raw = matches[0].Value.Replace(",", string.Empty);

        // A trailing separator was punctuation, not part of the number ("Total: 1,299.").
        raw = raw.TrimEnd('.');

        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }

    /// <summary>
    /// A single run of digits with optional grouping commas and decimals. Whitespace is deliberately NOT
    /// treated as a group separator: allowing it would silently weld "₹14,989" and a nearby "24 offers"
    /// into 1498924, and a confidently wrong number is worse than a value we decline to compare.
    /// </summary>
    private static readonly Regex NumericRunPattern = new(
        @"\d[\d,]*(?:\.\d+)?",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static string Format(double value) =>
        value == Math.Floor(value)
            ? value.ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Renders observed values compactly — a report line, not a data dump.</summary>
    private static string Summarise(IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return "nothing";
        }

        // Empty inputs and icon-only controls have no text to quote. "found 1 match(es): " reads like
        // a bug, so say what was actually established.
        if (values.All(string.IsNullOrWhiteSpace))
        {
            return values.Count == 1 ? "1 element, with no text of its own" : $"{values.Count} elements, none with text of their own";
        }

        var shown = values.Take(5).Select(Truncate);
        var text = string.Join(", ", shown);

        return values.Count > 5 ? $"{text} … ({values.Count} in total)" : text;
    }

    private static string Truncate(string value)
    {
        var text = value.Trim();
        return text.Length <= 60 ? text : text[..60] + "…";
    }

    /// <summary>Restates the assertion as the requirement it encodes, for the report's "expected" line.</summary>
    internal static string DescribeExpectation(StepAssertion assertion)
    {
        var op = (assertion.Operator ?? string.Empty).Trim().ToLowerInvariant();
        var expected = (assertion.Expected ?? string.Empty).Trim();

        return op switch
        {
            "equals" => $"exactly \"{expected}\"",
            "not_equals" => $"anything other than \"{expected}\"",
            "contains" => $"contains \"{expected}\"",
            "not_contains" => $"does not contain \"{expected}\"",
            "matches" => $"matches the pattern /{expected}/",
            "exists" => "present on the page",
            "not_exists" => "not present on the page",
            "sorted_asc" => "sorted low to high",
            "sorted_desc" => "sorted high to low",
            "first_is_max" => "the first value is the highest on the page",
            "first_is_min" => "the first value is the lowest on the page",
            "gte" => $"at least {expected}",
            "lte" => $"at most {expected}",
            "count_equals" => $"exactly {expected} match(es)",
            _ => expected.Length > 0 ? expected : op,
        };
    }
}
