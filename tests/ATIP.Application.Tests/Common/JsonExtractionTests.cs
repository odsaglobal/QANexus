using ATIP.Application.Common.Utilities;
using FluentAssertions;
using Xunit;

namespace ATIP.Application.Tests.Common;

public class JsonExtractionTests
{
    [Fact]
    public void Extracts_object_from_plain_json()
    {
        JsonExtraction.ExtractJsonObject("{\"a\":1}").Should().Be("{\"a\":1}");
    }

    [Fact]
    public void Strips_markdown_code_fences()
    {
        var input = "```json\n{\"a\":1}\n```";
        JsonExtraction.ExtractJsonObject(input).Should().Be("{\"a\":1}");
    }

    [Fact]
    public void Strips_surrounding_prose()
    {
        var input = "Here is the result:\n{\"a\":1}\nHope that helps!";
        JsonExtraction.ExtractJsonObject(input).Should().Be("{\"a\":1}");
    }

    [Fact]
    public void Handles_arrays()
    {
        JsonExtraction.ExtractJsonObject("prefix [1,2,3] suffix").Should().Be("[1,2,3]");
    }

    [Fact]
    public void Returns_empty_object_for_blank_input()
    {
        JsonExtraction.ExtractJsonObject("   ").Should().Be("{}");
    }
}
