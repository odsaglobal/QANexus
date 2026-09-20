using ATIP.Api.Services;
using ATIP.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Api.Hubs;

/// <summary>
/// Real-time hub that streams live browser screencast frames and progress for an
/// exploration session. Membership is strictly tenant-scoped: a connection may only
/// join a session that belongs to the caller's own organization, so users from
/// different organizations explore in complete isolation.
/// </summary>
[Authorize]
public sealed class ExplorationHub : Hub
{
    private readonly IApplicationDbContext _db;
    private readonly IExplorationLiveStream _live;

    public ExplorationHub(IApplicationDbContext db, IExplorationLiveStream live)
    {
        _db = db;
        _live = live;
    }

    /// <summary>Group name that isolates frames by tenant and session.</summary>
    public static string GroupName(Guid tenantId, Guid sessionId) => $"tenant:{tenantId:N}:session:{sessionId:N}";

    /// <summary>
    /// Subscribes the caller's connection to a session's live stream after verifying the
    /// session belongs to the caller's tenant.
    /// </summary>
    public async Task JoinSession(Guid sessionId)
    {
        var tenantId = GetTenantId()
            ?? throw new HubException("Missing tenant identity.");

        var belongsToTenant = await _db.ExplorationSessions
            .IgnoreQueryFilters()
            .AnyAsync(s => s.Id == sessionId && s.TenantId == tenantId);

        if (!belongsToTenant)
        {
            throw new HubException("Exploration session not found for your organization.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(tenantId, sessionId));

        // Replay recent activity so a client joining mid-run immediately sees prior log/step lines.
        await _live.ReplayRecentAsync(sessionId, Context.ConnectionId);
    }

    /// <summary>Removes the caller's connection from a session's live stream.</summary>
    public async Task LeaveSession(Guid sessionId)
    {
        var tenantId = GetTenantId();
        if (tenantId is null)
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(tenantId.Value, sessionId));
    }

    private Guid? GetTenantId()
    {
        var raw = Context.User?.FindFirst(Auth0ClaimsTransformer.TenantIdClaim)?.Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
