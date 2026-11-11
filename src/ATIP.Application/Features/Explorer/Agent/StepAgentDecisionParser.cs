using System.Text.Json;
using System.Text.RegularExpressions;
using ATIP.Application.Common.Utilities;

namespace ATIP.Application.Features.Explorer.Agent;

/// <summary>
/// Turns whatever an LLM replied into a valid <see cref="StepAgentDecision"/>, or explains precisely
/// why it can't. Models differ wildly in how they phrase the same decision — snake_case keys,
/// "fill" instead of "type", <c>"[ref=e12]"</c> instead of <c>"e12"</c>, booleans as strings, a
/// wrapper object — so the agent loop must never depend on one model's habits.
/// </summary>
public static partial class StepAgentDecisionParser
{
    /// <summary>Actions that operate on one element of the snapshot.</summary>
    public static readonly IReadOnlySet<string> ElementActions = new HashSet<string>(StringComparer.Ordinal)
    {
        "click", "double_click", "right_click", "hover", "type", "clear", "select", "check", "uncheck", "drag", "upload",
    };

    public static readonly IReadOnlyList<string> CanonicalActions =
    [
        "navigate", "click", "double_click", "right_click", "hover", "type", "clear", "select",
        "check", "uncheck", "drag", "upload", "press", "scroll", "wait", "wait_for", "back", "reload",
        "switch_tab", "accept_dialog", "dismiss_dialog", "ask", "finish",
    ];

    private static readonly Dictionary<string, string> ActionSynonyms = new(StringComparer.Ordinal)
    {
        ["navigate"] = "navigate", ["goto"] = "navigate", ["go"] = "navigate", ["open"] = "navigate",
        ["visit"] = "navigate", ["load"] = "navigate", ["navigateto"] = "navigate", ["openurl"] = "navigate",
        ["click"] = "click", ["tap"] = "click", ["clickelement"] = "click", ["leftclick"] = "click", ["submit"] = "click",
        ["doubleclick"] = "double_click", ["dblclick"] = "double_click",
        ["rightclick"] = "right_click", ["contextclick"] = "right_click",
        ["hover"] = "hover", ["mouseover"] = "hover", ["hoverover"] = "hover", ["mousemove"] = "hover",
        ["type"] = "type", ["fill"] = "type", ["input"] = "type", ["enter"] = "type", ["entertext"] = "type",
        ["typetext"] = "type", ["write"] = "type", ["setvalue"] = "type", ["sendkeys"] = "type", ["filltext"] = "type",
        ["clear"] = "clear", ["cleartext"] = "clear", ["clearfield"] = "clear",
        ["select"] = "select", ["selectoption"] = "select", ["choose"] = "select", ["pick"] = "select", ["dropdown"] = "select",
        ["check"] = "check", ["tick"] = "check", ["uncheck"] = "uncheck", ["untick"] = "uncheck",
        ["drag"] = "drag", ["draganddrop"] = "drag", ["dragdrop"] = "drag", ["dragto"] = "drag", ["drop"] = "drag",
        ["upload"] = "upload", ["uploadfile"] = "upload", ["attach"] = "upload", ["attachfile"] = "upload", ["setinputfiles"] = "upload",
        ["press"] = "press", ["key"] = "press", ["keypress"] = "press", ["presskey"] = "press", ["hotkey"] = "press", ["keyboard"] = "press",
        ["scroll"] = "scroll", ["scrolldown"] = "scroll", ["scrollup"] = "scroll", ["scrollpage"] = "scroll",
        ["wait"] = "wait", ["sleep"] = "wait", ["pause"] = "wait",
        ["waitfor"] = "wait_for", ["waitfortext"] = "wait_for", ["waituntil"] = "wait_for",
        ["back"] = "back", ["goback"] = "back", ["navigateback"] = "back",
        ["reload"] = "reload", ["refresh"] = "reload",
        ["switchtab"] = "switch_tab", ["selecttab"] = "switch_tab", ["tab"] = "switch_tab",
        ["acceptdialog"] = "accept_dialog", ["accept"] = "accept_dialog", ["acceptalert"] = "accept_dialog", ["confirm"] = "accept_dialog",
        ["dismissdialog"] = "dismiss_dialog", ["dismiss"] = "dismiss_dialog", ["dismissalert"] = "dismiss_dialog",
        ["ask"] = "ask", ["askuser"] = "ask", ["question"] = "ask", ["clarify"] = "ask", ["askhuman"] = "ask",
        ["finish"] = "finish", ["done"] = "finish", ["complete"] = "finish", ["completed"] = "finish",
        ["stop"] = "finish", ["end"] = "finish", ["success"] = "finish",
        ["fail"] = "finish", ["failed"] = "finish", ["giveup"] = "finish", ["abort"] = "finish",
    };

    private static readonly string[] FailureFinishWords = ["fail", "failed", "giveup", "abort"];

    /// <summary>
    /// Parses and validates a reply. Returns the decision (possibly repaired, e.g. a ref recovered from
    /// the target's name) and a <paramref name="problem"/> describing what is still wrong, phrased so it
    /// can be sent back to the model verbatim. A non-null problem means the decision must not be acted on.
    /// </summary>
    public static StepAgentDecision? Parse(string? raw, string? snapshot, out string? problem)
    {
        problem = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            problem = "The reply was empty. Reply with one JSON object.";
            return null;
        }

        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(JsonExtraction.ExtractJsonObject(raw));
            root = Unwrap(doc.RootElement).Clone();
        }
        catch (JsonException)
        {
            problem = "The reply was not valid JSON. Reply with exactly one JSON object and nothing else.";
            return null;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            problem = "The reply must be a single JSON object, not a list or a bare value.";
            return null;
        }

        var fields = root.EnumerateObject()
            .GroupBy(p => NormalizeKey(p.Name))
            .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.Ordinal);

        var rawAction = ReadString(fields, "action", "actiontype", "command", "operation", "tool", "next", "nextaction", "type");
        var decision = new StepAgentDecision
        {
            Thought = ReadString(fields, "thought", "thinking", "reasoning", "reason", "rationale", "explanation"),
            Ref = NormalizeRef(ReadString(fields, "ref", "elementref", "reference", "targetref", "refid", "elementid")),
            Target = ReadString(fields, "target", "element", "selector", "name", "label", "url", "key", "locator"),
            Value = ReadString(fields, "value", "text", "input", "option", "content", "keys"),
            ExpectedResult = ReadString(fields, "expectedresult", "expected", "expectation", "assertion", "expectedoutcome"),
            StepComplete = ReadBool(fields, "stepcomplete", "missioncomplete", "complete", "done", "goalreached", "success", "finished"),
            StepFailed = ReadBool(fields, "stepfailed", "missionfailed", "failed", "impossible", "blocked", "failure"),
            DestinationRef = NormalizeRef(ReadString(fields, "destinationref", "toref", "dropref", "endref", "targetref2")),
            DestinationTarget = ReadString(fields, "destination", "to", "totarget", "droptarget", "endtarget", "dropzone"),
            Question = ReadString(fields, "question", "clarification", "askuser"),
            QuestionContext = ReadString(fields, "questioncontext", "options", "choices", "context"),
        };

        var actionKey = NormalizeKey(rawAction ?? string.Empty);
        if (string.IsNullOrEmpty(actionKey) && (decision.StepComplete || decision.StepFailed))
        {
            actionKey = "finish";
        }

        if (!ActionSynonyms.TryGetValue(actionKey, out var action))
        {
            problem = string.IsNullOrWhiteSpace(rawAction)
                ? $"The reply had no \"action\". Use one of: {string.Join(", ", CanonicalActions)}."
                : $"Unknown action \"{rawAction}\". Use one of: {string.Join(", ", CanonicalActions)}.";
            return decision;
        }

        decision.Action = action;
        if (action == "finish")
        {
            if (FailureFinishWords.Contains(actionKey))
            {
                decision.StepFailed = true;
            }
            else if (!decision.StepFailed)
            {
                decision.StepComplete = true;
            }
        }

        // Models often put the handle in "target" ("e12") and the name nowhere.
        if (decision.Ref is null && NormalizeRef(decision.Target) is { } refInTarget && LooksLikeRef(decision.Target))
        {
            decision.Ref = refInTarget;
            decision.Target = null;
        }

        problem = Validate(decision, snapshot);
        return decision;
    }

    /// <summary>All <c>[ref=eN]</c> handles present in a snapshot.</summary>
    public static HashSet<string> RefsIn(string? snapshot) =>
        string.IsNullOrEmpty(snapshot)
            ? []
            : RefPattern().Matches(snapshot).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

    private static string? Validate(StepAgentDecision d, string? snapshot)
    {
        var action = d.Action!;

        if (ElementActions.Contains(action))
        {
            var refs = RefsIn(snapshot);
            if (d.Ref is not null && refs.Count > 0 && !refs.Contains(d.Ref))
            {
                // A hallucinated or stale handle: recover it from the name when the name is unambiguous.
                d.Ref = FindRefByName(snapshot, d.Target);
                if (d.Ref is null && string.IsNullOrWhiteSpace(d.Target))
                {
                    return $"ref is not in the current snapshot. Pick a [ref=eN] that appears in the snapshot for the {action} target.";
                }
            }

            if (d.Ref is null && !string.IsNullOrWhiteSpace(d.Target))
            {
                d.Ref = FindRefByName(snapshot, d.Target);
            }

            if (d.Ref is null && string.IsNullOrWhiteSpace(d.Target))
            {
                return $"\"{action}\" needs \"ref\" (a [ref=eN] from the snapshot) and \"target\" (the element's visible name).";
            }

            // These tools cannot fall back to finding an element by name.
            if (d.Ref is null && action is "hover" or "select" or "double_click" or "right_click" or "check" or "uncheck" or "drag" or "upload")
            {
                return $"\"{action}\" needs a \"ref\" that exists in the snapshot; \"{d.Target}\" did not match any element by name.";
            }
        }

        if (action == "drag")
        {
            var refs = RefsIn(snapshot);
            if (d.DestinationRef is null || (refs.Count > 0 && !refs.Contains(d.DestinationRef)))
            {
                d.DestinationRef = FindRefByName(snapshot, d.DestinationTarget);
            }

            if (d.DestinationRef is null)
            {
                return "\"drag\" needs \"destinationRef\" (the [ref=eN] to drop onto) and \"destination\" (its visible name).";
            }
        }

        switch (action)
        {
            case "type" when d.Value is null:
                return "\"type\" needs \"value\" — the text to enter.";
            case "select" when string.IsNullOrWhiteSpace(d.Value):
                return "\"select\" needs \"value\" — the exact option label to choose.";
            case "upload" when string.IsNullOrWhiteSpace(d.Value):
                return "\"upload\" needs \"value\" — the test file name(s) to attach, comma-separated.";
            case "navigate" when string.IsNullOrWhiteSpace(d.Target):
                if (string.IsNullOrWhiteSpace(d.Value))
                {
                    return "\"navigate\" needs \"target\" — the URL or path to open.";
                }
                d.Target = d.Value;
                break;
            case "press" when string.IsNullOrWhiteSpace(d.Target):
                d.Target = string.IsNullOrWhiteSpace(d.Value) ? "Enter" : d.Value;
                break;
            case "wait_for" when string.IsNullOrWhiteSpace(d.Value) && string.IsNullOrWhiteSpace(d.Target):
                return "\"wait_for\" needs \"value\" — the text to wait for.";
            case "ask" when string.IsNullOrWhiteSpace(d.Question):
                return "\"ask\" needs \"question\" — one specific question for the user.";
        }

        return null;
    }

    /// <summary>Finds the unique snapshot ref whose accessible name matches, ignoring case and quotes.</summary>
    public static string? FindRefByName(string? snapshot, string? name)
    {
        if (string.IsNullOrEmpty(snapshot) || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var wanted = Squash(name);
        string? exact = null;
        var exactCount = 0;
        string? partial = null;
        var partialCount = 0;

        foreach (var line in snapshot.Split('\n'))
        {
            var refMatch = RefPattern().Match(line);
            if (!refMatch.Success)
            {
                continue;
            }

            // A line can carry both an accessible name and trailing text: - generic "tip" [ref=e4]: Menu
            var quoted = QuotedNamePattern().Match(line);
            var colon = line.LastIndexOf("]:", StringComparison.Ordinal);
            var names = new[]
            {
                quoted.Success ? quoted.Groups[1].Value : string.Empty,
                colon >= 0 ? line[(colon + 2)..] : string.Empty,
            };

            foreach (var candidate in names.Select(Squash).Where(c => c.Length > 0))
            {
                if (candidate == wanted)
                {
                    exact ??= refMatch.Groups[1].Value;
                    exactCount++;
                }
                else if (wanted.Length >= 3 && (candidate.Contains(wanted, StringComparison.Ordinal) || wanted.Contains(candidate, StringComparison.Ordinal)))
                {
                    partial ??= refMatch.Groups[1].Value;
                    partialCount++;
                }
            }
        }

        return exactCount >= 1 ? exact : partialCount == 1 ? partial : null;
    }

    private static JsonElement Unwrap(JsonElement element)
    {
        // [ {...} ] → {...}
        if (element.ValueKind == JsonValueKind.Array && element.GetArrayLength() > 0)
        {
            element = element[0];
        }

        // { "decision": { ... } } / { "response": { ... } } → inner object, when it holds the action.
        if (element.ValueKind == JsonValueKind.Object)
        {
            var props = element.EnumerateObject().ToList();
            if (props.Count == 1 && props[0].Value.ValueKind == JsonValueKind.Object)
            {
                return props[0].Value;
            }
        }

        return element;
    }

    private static string? ReadString(Dictionary<string, JsonElement> fields, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!fields.TryGetValue(key, out var v))
            {
                continue;
            }

            var s = v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null,
            };

            if (s is not null)
            {
                return s;
            }
        }

        return null;
    }

    private static bool ReadBool(Dictionary<string, JsonElement> fields, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!fields.TryGetValue(key, out var v))
            {
                continue;
            }

            switch (v.ValueKind)
            {
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.Number when v.TryGetInt32(out var n):
                    return n != 0;
                case JsonValueKind.String:
                    var s = v.GetString()?.Trim().ToLowerInvariant();
                    if (s is "true" or "yes" or "1")
                    {
                        return true;
                    }
                    break;
            }
        }

        return false;
    }

    private static string NormalizeKey(string key) =>
        new string(key.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static string? NormalizeRef(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var m = RefValuePattern().Match(value);
        if (!m.Success)
        {
            return null;
        }

        var token = m.Groups[1].Value.ToLowerInvariant();
        return char.IsDigit(token[0]) ? "e" + token : token;
    }

    private static bool LooksLikeRef(string? value) =>
        value is not null && WholeRefPattern().IsMatch(value.Trim());

    private static string Squash(string s) =>
        new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    // Main-document refs are eN; refs inside iframes are prefixed with the frame, e.g. f1e2.
    [GeneratedRegex(@"\[ref=((?:f\d+)?e\d+)\]")]
    private static partial Regex RefPattern();

    [GeneratedRegex(@"(?:ref\s*=\s*)?\b((?:f\d+)?e\d+|\d+)\b", RegexOptions.IgnoreCase)]
    private static partial Regex RefValuePattern();

    [GeneratedRegex(@"^\[?(?:ref\s*=\s*)?(?:f\d+)?e\d+\]?$", RegexOptions.IgnoreCase)]
    private static partial Regex WholeRefPattern();

    [GeneratedRegex(@"""([^""]{1,200})""")]
    private static partial Regex QuotedNamePattern();
}
