using Chop.Application.Incidents;
using Chop.Domain.Incidents;

namespace Chop.Application.Tests;

public sealed class IncidentStateMachineTests
{
    [Fact]
    public void OperatorChange_WithoutComment_ThrowsBadRequest()
    {
        var ex = Assert.Throws<IncidentStatusTransitionException>(() =>
            IncidentStateMachine.ValidateOperatorChange(IncidentStatus.Acked, IncidentStatus.Canceled, "OPERATOR", ""));

        Assert.Equal(400, ex.HttpStatusCode);
        Assert.Equal("COMMENT_REQUIRED", ex.Code);
    }

    [Fact]
    public void OperatorChange_FromResolved_ThrowsConflict()
    {
        var ex = Assert.Throws<IncidentStatusTransitionException>(() =>
            IncidentStateMachine.ValidateOperatorChange(IncidentStatus.Resolved, IncidentStatus.Canceled, "OPERATOR", "x"));

        Assert.Equal(409, ex.HttpStatusCode);
        Assert.Equal("RESOLVED_IS_FINAL", ex.Code);
    }

    [Fact]
    public void OperatorChange_ToCanceled_WithComment_Succeeds()
    {
        IncidentStateMachine.ValidateOperatorChange(IncidentStatus.Acked, IncidentStatus.Canceled, "OPERATOR", "manual cancel");
    }

    [Fact]
    public void OperatorChange_ToEnRoute_ForOperator_ThrowsConflict()
    {
        var ex = Assert.Throws<IncidentStatusTransitionException>(() =>
            IncidentStateMachine.ValidateOperatorChange(IncidentStatus.Accepted, IncidentStatus.EnRoute, "OPERATOR", "x"));

        Assert.Equal(409, ex.HttpStatusCode);
        Assert.Equal("OPERATOR_TRANSITION_FORBIDDEN", ex.Code);
    }

    [Fact]
    public void AdminChange_ToFalseAlarm_WithComment_Succeeds()
    {
        IncidentStateMachine.ValidateOperatorChange(IncidentStatus.OnScene, IncidentStatus.FalseAlarm, "ADMIN", "admin override");
    }

    [Fact]
    public void GuardAccept_FromDispatched_Succeeds()
    {
        IncidentStateMachine.ValidateGuardAccept(IncidentStatus.Dispatched);
    }

    [Fact]
    public void GuardAccept_FromAcked_ThrowsConflict()
    {
        var ex = Assert.Throws<IncidentStatusTransitionException>(() =>
            IncidentStateMachine.ValidateGuardAccept(IncidentStatus.Acked));

        Assert.Equal(409, ex.HttpStatusCode);
        Assert.Equal("INVALID_GUARD_ACCEPT_TRANSITION", ex.Code);
    }

    [Fact]
    public void GuardProgress_AcceptedToEnRoute_Succeeds()
    {
        IncidentStateMachine.ValidateGuardProgress(IncidentStatus.Accepted, IncidentStatus.EnRoute, null);
    }

    [Fact]
    public void GuardProgress_OnSceneToResolved_WithoutComment_ThrowsBadRequest()
    {
        var ex = Assert.Throws<IncidentStatusTransitionException>(() =>
            IncidentStateMachine.ValidateGuardProgress(IncidentStatus.OnScene, IncidentStatus.Resolved, " "));

        Assert.Equal(400, ex.HttpStatusCode);
        Assert.Equal("COMMENT_REQUIRED_FOR_RESOLVED", ex.Code);
    }

    [Fact]
    public void GuardProgress_AcceptedToOnScene_ThrowsConflict()
    {
        var ex = Assert.Throws<IncidentStatusTransitionException>(() =>
            IncidentStateMachine.ValidateGuardProgress(IncidentStatus.Accepted, IncidentStatus.OnScene, "x"));

        Assert.Equal(409, ex.HttpStatusCode);
        Assert.Equal("INVALID_GUARD_PROGRESS_TRANSITION", ex.Code);
    }
}
