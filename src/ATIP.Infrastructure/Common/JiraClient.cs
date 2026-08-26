using System.Text;
using System.Text.Json;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Common.Models;
using ATIP.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ATIP.Infrastructure.Common;

public sealed class JiraClient : IJiraClient
{
    private const string HttpClientName = "jira";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JiraOptions _options;
    private readonly ILogger<JiraClient> _logger;

    public JiraClient(
        IHttpClientFactory httpClientFactory,
        IOptions<JiraOptions> options,
        ILogger<JiraClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<JiraIssue> GetIssueAsync(string issueKey, CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException("Jira is not configured. Set Jira:BaseUrl, Jira:Email and Jira:ApiToken.");
        }

        var key = issueKey.Trim();
        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var response = await client.GetAsync(
            $"rest/api/3/issue/{Uri.EscapeDataString(key)}?fields=summary,description,status,issuetype,priority",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Jira issue {Key} fetch failed: {Status} {Body}", key, (int)response.StatusCode, body);
            throw new InvalidOperationException(
                response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? $"Jira issue '{key}' was not found."
                    : $"Jira request failed ({(int)response.StatusCode}).");
        }

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 256 });
        var root = doc.RootElement;
        var fields = root.TryGetProperty("fields", out var f) ? f : default;

        var siteBase = _options.BaseUrl.TrimEnd('/');

        return new JiraIssue
        {
            Key = root.TryGetProperty("key", out var k) ? k.GetString() ?? key : key,
            Summary = GetString(fields, "summary") ?? key,
            Description = ExtractAdfText(fields, "description"),
            Status = GetNestedName(fields, "status"),
            IssueType = GetNestedName(fields, "issuetype"),
            Priority = GetNestedName(fields, "priority"),
            Url = $"{siteBase}/browse/{key}",
        };
    }

    private static string? GetString(JsonElement fields, string name) =>
        fields.ValueKind == JsonValueKind.Object
        && fields.TryGetProperty(name, out var v)
        && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static string? GetNestedName(JsonElement fields, string name) =>
        fields.ValueKind == JsonValueKind.Object
        && fields.TryGetProperty(name, out var v)
        && v.ValueKind == JsonValueKind.Object
        && v.TryGetProperty("name", out var n)
            ? n.GetString()
            : null;

    /// <summary>Jira Cloud descriptions are Atlassian Document Format (ADF) JSON — flatten to plain text.</summary>
    private static string? ExtractAdfText(JsonElement fields, string name)
    {
        if (fields.ValueKind != JsonValueKind.Object
            || !fields.TryGetProperty(name, out var node)
            || node.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var sb = new StringBuilder();
        WalkAdf(node, sb);
        var text = sb.ToString().Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static void WalkAdf(JsonElement node, StringBuilder sb)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            if (node.TryGetProperty("type", out var type) && type.GetString() == "text"
                && node.TryGetProperty("text", out var text))
            {
                sb.Append(text.GetString());
            }

            var nodeType = node.TryGetProperty("type", out var t) ? t.GetString() : null;
            if (node.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in content.EnumerateArray())
                {
                    WalkAdf(child, sb);
                }
            }

            // Block-level nodes get a trailing newline for readability.
            if (nodeType is "paragraph" or "heading" or "listItem" or "blockquote")
            {
                sb.Append('\n');
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in node.EnumerateArray())
            {
                WalkAdf(child, sb);
            }
        }
    }
}
