using ATIP.Application.Common.Utilities;
using FluentAssertions;
using Xunit;

namespace ATIP.Application.Tests.Common;

public class SlugGeneratorTests
{
    [Theory]
    [InlineData("Acme Corp", "acme-corp")]
    [InlineData("  Multiple   Spaces  ", "multiple-spaces")]
    [InlineData("Special@#Chars!!", "special-chars")]
    [InlineData("Café Déjà", "cafe-deja")]
    [InlineData("Checkout / Web", "checkout-web")]
    public void Create_produces_url_safe_slug(string input, string expected)
    {
        SlugGenerator.Create(input).Should().Be(expected);
    }

    [Fact]
    public void Create_returns_empty_for_blank_input()
    {
        SlugGenerator.Create("   ").Should().BeEmpty();
    }

    [Fact]
    public void Create_truncates_to_max_length_without_trailing_dash()
    {
        var result = SlugGenerator.Create(new string('a', 100), maxLength: 10);
        result.Length.Should().BeLessThanOrEqualTo(10);
        result.Should().NotEndWith("-");
    }
}
