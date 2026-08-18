using ATIP.Application.Features.Explorer.Agent;
using ATIP.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace ATIP.Application.Tests.Common;

public class ScenarioStepOutcomeResolverTests
{
    [Theory]
    [InlineData(true, 0, StepRunStatus.Passed)]
    [InlineData(true, 1, StepRunStatus.Healed)]
    [InlineData(false, 0, StepRunStatus.Failed)]
    public void Resolves_outcome_consistently_for_step_execution(bool resolved, int fallbackIndex, StepRunStatus expected)
    {
        var status = ScenarioStepOutcomeResolver.Resolve(resolved, fallbackIndex);

        status.Should().Be(expected);
    }
}
