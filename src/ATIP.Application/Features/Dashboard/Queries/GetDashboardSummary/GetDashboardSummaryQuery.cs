using ATIP.Application.Features.Dashboard.Dtos;
using MediatR;

namespace ATIP.Application.Features.Dashboard.Queries.GetDashboardSummary;

/// <summary>Returns real, tenant-scoped counts for the dashboard overview.</summary>
public sealed record GetDashboardSummaryQuery : IRequest<DashboardSummaryDto>;
