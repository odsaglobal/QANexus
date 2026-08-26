using MediatR;

namespace ATIP.Application.Features.TestData.Commands.DeleteTestDataSet;

/// <summary>Deletes a test data set.</summary>
public sealed record DeleteTestDataSetCommand(Guid Id) : IRequest;
