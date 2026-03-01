using Chop.Shared.Contracts.Incidents;

namespace Chop.Shared.Contracts.Realtime;

public static class IncidentRealtimeGroups
{
    public const string OpsLegacy = "ops:*";

    public const string OperatorRole = "role:OPERATOR";

    public const string AdminRole = "role:ADMIN";

    public const string SuperAdminRole = "role:SUPERADMIN";

    public static string IncidentScope(Guid incidentId) => $"scope:incident:{incidentId:D}";

    public static string ClientScope(string clientUserId) => $"scope:client:{clientUserId}";

    public static string RegionScope(string regionCode) => $"scope:region:{regionCode}";

    public static string ShiftScope(string shiftKey) => $"scope:shift:{shiftKey}";
}

public sealed class RealtimeScopeDto
{
    public Guid? IncidentId { get; set; }

    public string? ClientUserId { get; set; }

    public string? RegionCode { get; set; }

    public string? ShiftKey { get; set; }
}

public sealed class IncidentCreatedEvent
{
    public Guid EventId { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public string Type { get; set; } = "IncidentCreated";

    public IncidentDto Incident { get; set; } = new();

    public RealtimeScopeDto Scope { get; set; } = new();
}

public sealed class IncidentStatusChangedEvent
{
    public Guid EventId { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public string Type { get; set; } = "IncidentStatusChanged";

    public Guid IncidentId { get; set; }

    public string FromStatus { get; set; } = string.Empty;

    public string ToStatus { get; set; } = string.Empty;

    public string ActorUserId { get; set; } = string.Empty;

    public string ActorRole { get; set; } = string.Empty;

    public string? Comment { get; set; }

    public IncidentDto Incident { get; set; } = new();

    public RealtimeScopeDto Scope { get; set; } = new();
}

public sealed class DispatchCreatedEvent
{
    public Guid EventId { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public string Type { get; set; } = "DispatchCreated";

    public Guid IncidentId { get; set; }

    public RealtimeScopeDto Scope { get; set; } = new();
}

public sealed class DispatchAcceptedEvent
{
    public Guid EventId { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public string Type { get; set; } = "DispatchAccepted";

    public Guid IncidentId { get; set; }

    public string GuardUserId { get; set; } = string.Empty;

    public string? Comment { get; set; }

    public RealtimeScopeDto Scope { get; set; } = new();
}

public sealed class GuardLocationUpdatedEvent
{
    public Guid EventId { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public string Type { get; set; } = "GuardLocationUpdated";

    public Guid? IncidentId { get; set; }

    public string GuardUserId { get; set; } = string.Empty;

    public IncidentLocationDto Location { get; set; } = new();

    public RealtimeScopeDto Scope { get; set; } = new();
}
