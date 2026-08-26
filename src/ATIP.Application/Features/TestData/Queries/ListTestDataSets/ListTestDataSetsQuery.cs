using ATIP.Application.Features.TestData.Dtos;
using MediatR;

namespace ATIP.Application.Features.TestData.Queries.ListTestDataSets;

/// <summary>Lists all test data sets belonging to a specific environment.</summary>
public sealed record ListTestDataSetsQuery(Guid ProjectId, Guid EnvironmentId)
    : IRequest<IReadOnlyList<TestDataSetDto>>;
