namespace Chop.Domain.Incidents;

public enum IncidentStatus
{
    New = 0,
    Acked = 1,
    Dispatched = 2,
    Accepted = 3,
    EnRoute = 4,
    OnScene = 5,
    Resolved = 6,
    Canceled = 7,
    FalseAlarm = 8,
    Failed = 9,
}
