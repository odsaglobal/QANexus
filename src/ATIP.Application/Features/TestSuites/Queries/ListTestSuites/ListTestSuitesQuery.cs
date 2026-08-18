using ATIP.Application.Features.TestSuites.Dtos;
using MediatR;

namespace ATIP.Application.Features.TestSuites.Queries.ListTestSuites;

public sealed record ListTestSuitesQuery(Guid ProjectId) : IRequest<IReadOnlyList<TestSuiteDto>>;
