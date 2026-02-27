using Chop.Shared.Contracts.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

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

        await Groups.AddToGroupAsync(Context.ConnectionId, IncidentRealtimeGroups.Ops);
        await base.OnConnectedAsync();
    }
}
