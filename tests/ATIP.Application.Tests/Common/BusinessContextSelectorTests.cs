using ATIP.Application.Features.Explorer.Agent;
using FluentAssertions;
using Xunit;

namespace ATIP.Application.Tests.Common;

public class BusinessContextSelectorTests
{
    private static readonly string LongDoc = string.Join("\n\n",
        Enumerable.Range(1, 40).Select(i => $"## Section {i}\nGeneric filler text about topic number {i} that does not matter much at all."))
        + "\n\n## Checkout\nGuest checkout uses the account guest@example.com and always picks Standard delivery.";

    [Fact]
    public void Returns_short_context_unchanged()
    {
        BusinessContextSelector.Select("Small doc.", "anything", 1000).Should().Be("Small doc.");
    }

    [Fact]
    public void Keeps_the_section_relevant_to_the_step_even_beyond_the_head_budget()
    {
        LongDoc.Length.Should().BeGreaterThan(1500);

        var selected = BusinessContextSelector.Select(LongDoc, "Complete guest checkout with delivery", 600);

        selected.Should().Contain("guest@example.com");
        selected.Length.Should().BeLessThanOrEqualTo(700);
    }

    [Fact]
    public void Keeps_headings_with_their_paragraph()
    {
        var selected = BusinessContextSelector.Select(LongDoc, "checkout delivery", 300);

        selected.Should().Contain("## Checkout\nGuest checkout");
    }

    [Fact]
    public void Empty_context_yields_empty_string()
    {
        BusinessContextSelector.Select(null, "x", 100).Should().BeEmpty();
    }
}
