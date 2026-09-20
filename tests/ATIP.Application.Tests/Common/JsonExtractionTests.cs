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

    [Fact]
    public void Strips_reasoning_model_think_block()
    {
        var input = "<think>\nLet me consider the {options} here...\n</think>\n{\"a\":1}";
        JsonExtraction.ExtractJsonObject(input).Should().Be("{\"a\":1}");
    }

    [Fact]
    public void Ignores_braces_in_prose_before_the_real_json_object()
    {
        var input = "I'll click the {Login} button next.\n{\"action\":\"click\",\"target\":\"Login\"}";
        JsonExtraction.ExtractJsonObject(input).Should().Be("{\"action\":\"click\",\"target\":\"Login\"}");
    }

    [Fact]
    public void Handles_nested_objects_and_arrays_with_string_containing_brackets()
    {
        var input = "prefix {\"items\":[{\"name\":\"a [special] item\"},{\"name\":\"b\"}]} suffix";
        JsonExtraction.ExtractJsonObject(input)
            .Should().Be("{\"items\":[{\"name\":\"a [special] item\"},{\"name\":\"b\"}]}");
    }

    [Fact]
    public void Falls_back_to_naive_slice_when_json_never_closes()
    {
        var input = "Here is the result: {\"a\":1, \"b\": \"truncated because the model ran out of tokens";
        JsonExtraction.ExtractJsonObject(input).Should().Contain("\"a\":1");
    }
}
