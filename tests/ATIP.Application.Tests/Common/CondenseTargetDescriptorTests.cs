using ATIP.Infrastructure.Exploration;
using FluentAssertions;

namespace ATIP.Application.Tests.Common;

/// <summary>
/// The descriptor produced while exploring is stored as the REPLAY target, so these cases are about
/// correctness rather than tidiness: anything volatile that survives into the descriptor guarantees a
/// mismatch on the next run. The long inputs below are verbatim captures from a real Flipkart
/// exploration, which is where the problem was first observed.
/// </summary>
public class CondenseTargetDescriptorTests
{
    [Fact]
    public void Keeps_the_product_name_and_drops_the_volatile_card_text()
    {
        const string accessibleName =
            "Lenovo 100e Chromebook Gen 4 MediaTek Kompanio 520 - (4 GB/32 GB EMMC Storage/Chrome OS) "
            + "100e Chromebo... Add to Compare Lenovo 100e Chromebook Gen 4 MediaTek Kompanio 520 - "
            + "(4 GB/32 GB EMMC Storage/Chrome OS) 100e Chromebo... 4 3,479 Ratings & 330 Reviews "
            + "\u20b920,990 \u20b925,100 16% off Only 1 left Bank Offer";

        var condensed = ExplorerAgent.CondenseTargetDescriptor(accessibleName);

        condensed.Should().NotBeNull();
        condensed!.Length.Should().BeLessThanOrEqualTo(80);
        condensed.Should().StartWith("Lenovo 100e Chromebook Gen 4");

        // Every one of these changes between runs; keeping any of them makes the step unreplayable.
        condensed.Should().NotContain("20,990");
        condensed.Should().NotContain("Ratings");
        condensed.Should().NotContain("16% off");
        condensed.Should().NotContain("Only 1 left");
    }

    [Fact]
    public void Folds_a_name_that_repeats_itself()
    {
        ExplorerAgent.CondenseTargetDescriptor("Cart Cart").Should().Be("Cart");
        ExplorerAgent.CondenseTargetDescriptor("Add to Cart Add to Cart").Should().Be("Add to Cart");
    }

    [Fact]
    public void Leaves_an_already_clean_label_untouched()
    {
        ExplorerAgent.CondenseTargetDescriptor("Search for Products, Brands and More")
            .Should().Be("Search for Products, Brands and More");
        ExplorerAgent.CondenseTargetDescriptor("Add to Cart").Should().Be("Add to Cart");
    }

    [Fact]
    public void Does_not_fold_a_phrase_that_merely_repeats_a_word()
    {
        // "Cart" appears twice but the halves differ, so this is a real label, not a doubled one.
        ExplorerAgent.CondenseTargetDescriptor("Cart View Cart Now").Should().Be("Cart View Cart Now");
    }

    [Fact]
    public void Collapses_whitespace_runs()
    {
        ExplorerAgent.CondenseTargetDescriptor("  Place   \n Order  ").Should().Be("Place Order");
    }

    [Fact]
    public void Cuts_a_single_long_token_that_has_no_word_boundary()
    {
        var condensed = ExplorerAgent.CondenseTargetDescriptor(new string('x', 200));

        condensed.Should().HaveLength(80);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Passes_through_empty_input(string? input)
    {
        ExplorerAgent.CondenseTargetDescriptor(input).Should().Be(input);
    }
}
