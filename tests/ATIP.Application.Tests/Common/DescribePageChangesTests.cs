using ATIP.Infrastructure.Exploration;
using FluentAssertions;
using Xunit;

namespace ATIP.Application.Tests.Common;

public class DescribePageChangesTests
{
    [Fact]
    public void Lists_lines_that_appeared_after_expanding_a_section()
    {
        var before = "Filters\nBrand\nProcessor\nCore i5";
        var after = "Filters\nBrand\nHP\nLenovo\nProcessor\nCore i5";

        var changes = ExplorerAgent.DescribePageChanges(before, after, "https://x/s?q=a", "https://x/s?q=a");

        changes.Should().Contain("Appeared: HP | Lenovo").And.Contain("Disappeared: nothing").And.NotContain("URL changed");
    }

    [Fact]
    public void Reports_url_change_and_no_text_change()
    {
        var changes = ExplorerAgent.DescribePageChanges("Same", "Same", "https://x/a", "https://x/b");

        changes.Should().Contain("URL changed from https://x/a to https://x/b").And.Contain("No visible text changed.");
    }
}
