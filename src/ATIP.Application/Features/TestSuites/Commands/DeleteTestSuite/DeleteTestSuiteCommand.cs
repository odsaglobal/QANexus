using MediatR;

namespace ATIP.Application.Features.TestSuites.Commands.DeleteTestSuite;

/// <summary>Soft-deletes a test suite.</summary>
public sealed record DeleteTestSuiteCommand(Guid ProjectId, Guid Id) : IRequest;
