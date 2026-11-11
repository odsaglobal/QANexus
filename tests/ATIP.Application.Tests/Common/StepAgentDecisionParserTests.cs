using ATIP.Application.Features.Explorer.Agent;
using FluentAssertions;

namespace ATIP.Application.Tests.Common;

public class StepAgentDecisionParserTests
{
    private const string Snapshot = """
        - generic [active] [ref=e1]:
          - button "Add to cart" [ref=e2]
          - button "Buy now" [disabled] [ref=e3]
          - generic "tip" [ref=e4]: Menu
          - checkbox "Agree" [ref=e5]
          - textbox "Email" [ref=e6]
          - combobox "Sort" [ref=e7]
          - region "Done column" [ref=e8]
          - iframe [ref=e9]:
            - button "Inner button" [ref=f1e2]
        """;

    [Fact]
    public void Accepts_refs_inside_iframes()
    {
        var d = StepAgentDecisionParser.Parse("""{"action":"click","ref":"[ref=f1e2]","target":"Inner button"}""", Snapshot, out var problem);

        problem.Should().BeNull();
        d!.Ref.Should().Be("f1e2");
    }

    [Fact]
    public void Drag_recovers_the_destination_ref_from_its_name()
    {
        var d = StepAgentDecisionParser.Parse(
            """{"action":"drag_and_drop","ref":"e2","target":"Add to cart","destination":"Done column"}""", Snapshot, out var problem);

        problem.Should().BeNull();
        d!.Action.Should().Be("drag");
        d.DestinationRef.Should().Be("e8");
    }

    [Fact]
    public void Drag_without_a_destination_is_reported()
    {
        StepAgentDecisionParser.Parse("""{"action":"drag","ref":"e2"}""", Snapshot, out var problem);

        problem.Should().Contain("destinationRef");
    }

    [Fact]
    public void Upload_needs_file_names()
    {
        StepAgentDecisionParser.Parse("""{"action":"attach_file","ref":"e2"}""", Snapshot, out var problem);

        problem.Should().Contain("test file");
    }

    [Fact]
    public void Parses_canonical_reply()
    {
        var d = StepAgentDecisionParser.Parse(
            """{"thought":"t","action":"click","ref":"e2","target":"Add to cart","expectedResult":"x","stepComplete":false,"stepFailed":false}""",
            Snapshot, out var problem);

        problem.Should().BeNull();
        d!.Action.Should().Be("click");
        d.Ref.Should().Be("e2");
    }

    [Theory]
    [InlineData("fill", "type")]
    [InlineData("Enter_Text", "type")]
    [InlineData("TAP", "click")]
    [InlineData("dblclick", "double_click")]
    [InlineData("mouseover", "hover")]
    [InlineData("select_option", "select")]
    public void Maps_action_synonyms(string raw, string expected)
    {
        var d = StepAgentDecisionParser.Parse(
            $$"""{"action":"{{raw}}","ref":"e7","target":"Sort","value":"Price"}""", Snapshot, out _);

        d!.Action.Should().Be(expected);
    }

    [Fact]
    public void Accepts_snake_case_keys_string_booleans_and_wrapped_object()
    {
        var d = StepAgentDecisionParser.Parse(
            """Sure! ```json {"decision":{"action_type":"done","step_complete":"true","expected_result":"Cart shows 1"}} ```""",
            Snapshot, out var problem);

        problem.Should().BeNull();
        d!.Action.Should().Be("finish");
        d.StepComplete.Should().BeTrue();
        d.ExpectedResult.Should().Be("Cart shows 1");
    }

    [Theory]
    [InlineData("[ref=e6]")]
    [InlineData("ref=e6")]
    [InlineData("6")]
    public void Normalizes_ref_formats(string rawRef)
    {
        var d = StepAgentDecisionParser.Parse(
            $$"""{"action":"type","ref":"{{rawRef}}","target":"Email","value":"a@b.c"}""", Snapshot, out var problem);

        problem.Should().BeNull();
        d!.Ref.Should().Be("e6");
    }

    [Fact]
    public void Moves_a_ref_given_as_target()
    {
        var d = StepAgentDecisionParser.Parse("""{"action":"click","target":"e2"}""", Snapshot, out var problem);

        problem.Should().BeNull();
        d!.Ref.Should().Be("e2");
    }

    [Fact]
    public void Recovers_hallucinated_ref_from_the_name()
    {
        var d = StepAgentDecisionParser.Parse(
            """{"action":"click","ref":"e999","target":"add to CART"}""", Snapshot, out var problem);

        problem.Should().BeNull();
        d!.Ref.Should().Be("e2");
    }

    [Fact]
    public void Finds_names_after_bracketed_attributes_and_trailing_text()
    {
        StepAgentDecisionParser.FindRefByName(Snapshot, "Buy now").Should().Be("e3");
        StepAgentDecisionParser.FindRefByName(Snapshot, "Menu").Should().Be("e4");
    }

    [Fact]
    public void Reports_unknown_action_so_the_model_can_be_asked_again()
    {
        StepAgentDecisionParser.Parse("""{"action":"teleport"}""", Snapshot, out var problem);

        problem.Should().Contain("Unknown action \"teleport\"");
    }

    [Fact]
    public void Reports_type_without_value()
    {
        StepAgentDecisionParser.Parse("""{"action":"type","ref":"e6","target":"Email"}""", Snapshot, out var problem);

        problem.Should().Contain("value");
    }

    [Fact]
    public void Reports_hover_on_an_element_that_is_not_in_the_snapshot()
    {
        StepAgentDecisionParser.Parse("""{"action":"hover","target":"Nonexistent widget"}""", Snapshot, out var problem);

        problem.Should().Contain("ref");
    }

    [Fact]
    public void Reports_non_json()
    {
        StepAgentDecisionParser.Parse("I will click the button.", Snapshot, out var problem);

        problem.Should().Contain("JSON");
    }

    [Fact]
    public void Failure_words_mark_the_step_failed()
    {
        var d = StepAgentDecisionParser.Parse("""{"action":"give_up","thought":"no such control"}""", Snapshot, out var problem);

        problem.Should().BeNull();
        d!.StepFailed.Should().BeTrue();
        d.StepComplete.Should().BeFalse();
    }
}
