using Chop.Domain.Incidents;

namespace Chop.Application.Incidents;

internal static class IncidentStateMachine
{
    public static void ValidateOperatorChange(IncidentStatus from, IncidentStatus to, string actorRole, string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            throw IncidentStatusTransitionException.BadRequest(
                "COMMENT_REQUIRED",
                "Comment is required for manual status change.");
        }

        if (from == IncidentStatus.Resolved)
        {
            throw IncidentStatusTransitionException.Conflict(
                "RESOLVED_IS_FINAL",
                "Resolved incident cannot be changed.");
        }

        if (from == to)
        {
            throw IncidentStatusTransitionException.BadRequest(
                "NO_OP_TRANSITION",
                "Target status must differ from current status.");
        }

        if (to == IncidentStatus.Dispatched)
        {
            throw IncidentStatusTransitionException.Conflict(
                "DISPATCH_REQUIRED",
                "DISPATCHED status can only be set by dispatch workflow.");
        }

        if (to == IncidentStatus.Accepted)
        {
            throw IncidentStatusTransitionException.Conflict(
                "GUARD_ACCEPT_REQUIRED",
                "ACCEPTED status can only be set by guard accept workflow.");
        }

        if (string.Equals(actorRole, "OPERATOR", StringComparison.OrdinalIgnoreCase))
        {
            if (to is not (IncidentStatus.Canceled or IncidentStatus.FalseAlarm))
            {
                throw IncidentStatusTransitionException.Conflict(
                    "OPERATOR_TRANSITION_FORBIDDEN",
                    "Operator can only set CANCELED or FALSE_ALARM.");
            }

            return;
        }

        if (string.Equals(actorRole, "ADMIN", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actorRole, "SUPERADMIN", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw IncidentStatusTransitionException.BadRequest(
            "ROLE_NOT_ALLOWED",
            "Role is not allowed to change incident status.");
    }

    public static void ValidateGuardAccept(IncidentStatus from)
    {
        if (from != IncidentStatus.Dispatched)
        {
            throw IncidentStatusTransitionException.Conflict(
                "INVALID_GUARD_ACCEPT_TRANSITION",
                "Guard can accept only DISPATCHED incidents.");
        }
    }

    public static void ValidateGuardProgress(IncidentStatus from, IncidentStatus to, string? comment)
    {
        var expected = from switch
        {
            IncidentStatus.Accepted => IncidentStatus.EnRoute,
            IncidentStatus.EnRoute => IncidentStatus.OnScene,
            IncidentStatus.OnScene => IncidentStatus.Resolved,
            _ => (IncidentStatus?)null,
        };

        if (expected is null || expected.Value != to)
        {
            throw IncidentStatusTransitionException.Conflict(
                "INVALID_GUARD_PROGRESS_TRANSITION",
                "Invalid guard progress transition.");
        }

        if (to == IncidentStatus.Resolved && string.IsNullOrWhiteSpace(comment))
        {
            throw IncidentStatusTransitionException.BadRequest(
                "COMMENT_REQUIRED_FOR_RESOLVED",
                "Comment is required when resolving incident.");
        }
    }
}
