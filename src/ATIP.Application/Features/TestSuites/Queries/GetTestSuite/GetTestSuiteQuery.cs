using ATIP.Application.Features.TestSuites.Dtos;
using MediatR;

namespace ATIP.Application.Features.TestSuites.Queries.GetTestSuite;

public sealed record GetTestSuiteQuery(Guid ProjectId, Guid Id) : IRequest<TestSuiteDto>;
