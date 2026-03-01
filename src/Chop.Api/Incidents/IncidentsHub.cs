using Chop.Shared.Contracts.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Chop.Api.Incidents;

[Authorize(Roles = "OPERATOR,ADMIN,SUPERADMIN")]
public sealed class IncidentsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (Context.User?.IsInRole("OPERATOR") == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, IncidentRealtimeGroups.OperatorRole);
        }

        if (Context.User?.IsInRole("ADMIN") == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, IncidentRealtimeGroups.AdminRole);
        }

        if (Context.User?.IsInRole("SUPERADMIN") == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, IncidentRealtimeGroups.SuperAdminRole);
        }

        if (Context.User?.IsInRole("ADMIN") == true || Context.User?.IsInRole("SUPERADMIN") == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, IncidentRealtimeGroups.OpsLegacy);
        }

        // Optional scoped routing by claims.
        var clientScopes = Context.User?.FindAll("ops_client_scope").Select(x => x.Value).Where(x => !string.IsNullOrWhiteSpace(x)) ?? [];
        foreach (var scope in clientScopes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, IncidentRealtimeGroups.ClientScope(scope));
        }

        var regionScopes = Context.User?.FindAll("ops_region_scope").Select(x => x.Value).Where(x => !string.IsNullOrWhiteSpace(x)) ?? [];
        foreach (var scope in regionScopes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, IncidentRealtimeGroups.RegionScope(scope));
        }

        var shiftScopes = Context.User?.FindAll("ops_shift_scope").Select(x => x.Value).Where(x => !string.IsNullOrWhiteSpace(x)) ?? [];
        foreach (var scope in shiftScopes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, IncidentRealtimeGroups.ShiftScope(scope));
        }

        await base.OnConnectedAsync();
    }

    public Task SubscribeIncident(Guid incidentId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, IncidentRealtimeGroups.IncidentScope(incidentId));

    public Task UnsubscribeIncident(Guid incidentId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, IncidentRealtimeGroups.IncidentScope(incidentId));
}
