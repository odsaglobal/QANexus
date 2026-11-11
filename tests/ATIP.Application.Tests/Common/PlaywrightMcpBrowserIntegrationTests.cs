using ATIP.Infrastructure.Exploration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ATIP.Application.Tests.Common;

/// <summary>Drives the real Playwright MCP server. Does nothing when tools/mcp is not installed.</summary>
[Trait("Category", "Integration")]
public sealed class PlaywrightMcpBrowserIntegrationTests
{
    private const string Page = """
        <input type=file aria-label="Resume" onchange="document.getElementById('o').textContent='got '+this.files[0].name">
        <p id=o>none</p>
        <div draggable=true role=button aria-label="Card A" ondragstart="event.dataTransfer.setData('t','A')">Card A</div>
        <div role=region aria-label="Done column" ondragover="event.preventDefault()" ondrop="this.textContent='dropped '+event.dataTransfer.getData('t')">Done column</div>
        <label><input type=checkbox> Agree</label>
        <iframe srcdoc="<button onclick=&quot;this.textContent='inner clicked'&quot;>Inner button</button>"></iframe>
        """;

    [Fact]
    public async Task Uploads_only_from_the_file_root_and_drives_drag_check_and_iframes()
    {
        var script = FindMcpScript();
        if (script is null)
        {
            return;
        }

        var root = Directory.CreateTempSubdirectory("patternsai-it-").FullName;
        var inside = Path.Combine(root, "resume.txt");
        var outside = Path.Combine(Path.GetTempPath(), $"outside-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(inside, "ok");
        await File.WriteAllTextAsync(outside, "secret");

        try
        {
            await using var mcp = new PlaywrightMcpBrowser(NullLogger.Instance, script, ["--headless", "--isolated", "--browser", "chromium"], root);
            await mcp.InitializeAsync();
            await mcp.NavigateAsync("data:text/html," + Uri.EscapeDataString(Page));
            await mcp.WaitAsync(0.5);

            var snapshot = await mcp.SnapshotAsync();
            string Ref(string name) => Application.Features.Explorer.Agent.StepAgentDecisionParser.FindRefByName(snapshot, name)!;

            var denied = await mcp.UploadFilesAsync("Resume", Ref("Resume"), [outside]);
            denied.Ok.Should().BeFalse("files outside the advertised root must be refused");

            var allowed = await mcp.UploadFilesAsync("Resume", Ref("Resume"), [inside]);
            allowed.Ok.Should().BeTrue(allowed.Detail);

            (await mcp.DragAsync("Card A", Ref("Card A"), "Done column", Ref("Done column"))).Should().BeTrue();
            (await mcp.SetCheckedAsync("Agree", Ref("Agree"), wanted: true)).Should().BeTrue();
            (await mcp.ClickAsync("Inner button", Ref("Inner button"))).Should().BeTrue();

            var after = await mcp.SnapshotAsync();
            after.Should().Contain("got resume.txt").And.Contain("dropped A").And.Contain("inner clicked");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            File.Delete(outside);
        }
    }

    [Fact]
    public async Task Detects_and_dismisses_a_login_wall_but_not_ordinary_fixed_layout()
    {
        var script = FindMcpScript();
        if (script is null)
        {
            return;
        }

        const string page = """
            <meta charset="utf-8">
            <div style="position:fixed;inset:0;z-index:1">app shell <input placeholder="Search"></div>
            <div id=wall style="position:fixed;inset:0;z-index:99999999;background:rgba(0,0,0,.5)"
                 onkeydown="">
              <div style="margin:100px auto;width:600px;height:400px;background:#fff">
                <span onclick="document.getElementById('wall').remove()">✕</span>
                Login. Log in for the best experience. Enter your phone number to continue
              </div>
            </div>
            """;

        await using var mcp = new PlaywrightMcpBrowser(NullLogger.Instance, script, ["--headless", "--isolated", "--browser", "chromium"]);
        await mcp.InitializeAsync();
        await mcp.NavigateAsync("data:text/html," + Uri.EscapeDataString(page));

        var popup = await mcp.DetectBlockingPopupAsync();
        popup.Should().Contain("Log in for the best experience");

        (await mcp.DismissBlockingPopupAsync()).Should().BeTrue();
        (await mcp.DetectBlockingPopupAsync()).Should().BeNull("the low z-index app shell is not a popup");
    }

    private static string? FindMcpScript()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "tools", "mcp", "playwright-mcp.sh");
            if (File.Exists(candidate) && Directory.Exists(Path.Combine(dir.FullName, "tools", "mcp", "node_modules")))
            {
                return candidate;
            }
        }

        return null;
    }
}
