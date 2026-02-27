using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Chop.Shared.Contracts.Alerts;
using Chop.Shared.Contracts.Auth;
using Chop.Shared.Contracts.Guards;
using Chop.Shared.Contracts.Incidents;

namespace Chop.Api.Tests;

public sealed class AlertsApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public AlertsApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Operator_CanAckAndResolve_IncidentAlert()
    {
        // Arrange: seed profile with a primary address missing geo.
        await _factory.SeedClientProfileAsync(
            TestUsers.Client.UserId,
            "Client Alerts",
            phones: [],
            addresses:
            [
                (Label: "Home", Address: "NoGeo", Lat: (double?)null, Lon: (double?)null, IsPrimary: true),
            ]);

        var operatorToken = await LoginAndGetAccessTokenAsync("operator", "operator-pass");
        var clientToken = await LoginAndGetAccessTokenAsync("client", "client-pass");

        using var createMessage = new HttpRequestMessage(HttpMethod.Post, "/api/incidents")
        {
            Content = JsonContent.Create(new CreateIncidentDto
            {
                ClientLocation = null,
                AddressText = "alerts-test",
            }),
        };
        createMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);
        var createResponse = await _client.SendAsync(createMessage);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreateIncidentResponseDto>();
        Assert.NotNull(created);

        // List open alerts.
        using var listMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/operator/incidents/{created!.IncidentId}/alerts?includeResolved=false");
        listMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
        var listResponse = await _client.SendAsync(listMessage);
        listResponse.EnsureSuccessStatusCode();
        var items = await listResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<AlertListItemDto>>();
        Assert.NotNull(items);
        Assert.NotEmpty(items);

        var first = items!.First();
        Assert.Equal("OPEN", first.Status);

        // Ack.
        using var ackMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/operator/alerts/{first.Id}/ack")
        {
            Content = JsonContent.Create(new AckAlertRequestDto { Comment = "ack" }),
        };
        ackMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
        var ackResponse = await _client.SendAsync(ackMessage);
        Assert.Equal(HttpStatusCode.NoContent, ackResponse.StatusCode);

        // Resolve.
        using var resolveMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/operator/alerts/{first.Id}/resolve")
        {
            Content = JsonContent.Create(new ResolveAlertRequestDto { Comment = "resolved" }),
        };
        resolveMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
        var resolveResponse = await _client.SendAsync(resolveMessage);
        Assert.Equal(HttpStatusCode.NoContent, resolveResponse.StatusCode);

        // Ensure it's not returned anymore when includeResolved=false.
        using var list2Message = new HttpRequestMessage(HttpMethod.Get, $"/api/operator/incidents/{created.IncidentId}/alerts?includeResolved=false");
        list2Message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
        var list2Response = await _client.SendAsync(list2Message);
        list2Response.EnsureSuccessStatusCode();
        var items2 = await list2Response.Content.ReadFromJsonAsync<IReadOnlyCollection<AlertListItemDto>>();
        Assert.NotNull(items2);
        Assert.DoesNotContain(items2!, x => x.Id == first.Id);
    }

    [Fact]
    public async Task Operator_CanAssignAndOverride_IncidentAlert()
    {
        await _factory.SeedClientProfileAsync(
            TestUsers.Client.UserId,
            "Client Alerts Assign",
            phones: [],
            addresses:
            [
                (Label: "Home", Address: "NoGeo", Lat: (double?)null, Lon: (double?)null, IsPrimary: true),
            ]);

        var operatorToken = await LoginAndGetAccessTokenAsync("operator", "operator-pass");
        var clientToken = await LoginAndGetAccessTokenAsync("client", "client-pass");

        using var createMessage = new HttpRequestMessage(HttpMethod.Post, "/api/incidents")
        {
            Content = JsonContent.Create(new CreateIncidentDto
            {
                ClientLocation = null,
                AddressText = "alerts-assign-test",
            }),
        };
        createMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);
        var createResponse = await _client.SendAsync(createMessage);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreateIncidentResponseDto>();
        Assert.NotNull(created);

        var alerts = await ListIncidentAlertsAsync(created!.IncidentId, operatorToken);
        var first = alerts.First();

        using var assignMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/operator/alerts/{first.Id}/assign")
        {
            Content = JsonContent.Create(new AssignAlertRequestDto
            {
                AssigneeUserId = TestUsers.Operator.UserId,
                Comment = "take ownership",
            }),
        };
        assignMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
        var assignResponse = await _client.SendAsync(assignMessage);
        Assert.Equal(HttpStatusCode.NoContent, assignResponse.StatusCode);

        var afterAssign = await ListIncidentAlertsAsync(created.IncidentId, operatorToken);
        var assigned = afterAssign.Single(x => x.Id == first.Id);
        Assert.Equal(TestUsers.Operator.UserId, assigned.AssigneeUserId);
        Assert.True(assigned.AssignedAtUtc.HasValue);

        using var overrideMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/operator/alerts/{first.Id}/override")
        {
            Content = JsonContent.Create(new OverrideAlertRequestDto
            {
                Comment = "manual override",
            }),
        };
        overrideMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
        var overrideResponse = await _client.SendAsync(overrideMessage);
        Assert.Equal(HttpStatusCode.NoContent, overrideResponse.StatusCode);

        var afterOverride = await ListIncidentAlertsAsync(created.IncidentId, operatorToken);
        Assert.DoesNotContain(afterOverride, x => x.Id == first.Id);
    }

    [Fact]
    public async Task PointConflictAlert_Appears_WhenAlertPointFarFromHome()
    {
        await _factory.SeedClientProfileAsync(
            TestUsers.ClientDetailsShape.UserId,
            "Client Point Conflict",
            phones: [],
            addresses:
            [
                (Label: "Home", Address: "Center", Lat: 55.751244, Lon: 37.618423, IsPrimary: true),
            ]);

        var operatorToken = await LoginAndGetAccessTokenAsync("operator", "operator-pass");
        var clientToken = await LoginAndGetAccessTokenAsync("client-details-shape", "client-details-shape-pass");

        using var createMessage = new HttpRequestMessage(HttpMethod.Post, "/api/incidents")
        {
            Content = JsonContent.Create(new CreateIncidentDto
            {
                ClientLocation = new IncidentLocationDto
                {
                    Lat = 59.9342802, // Saint Petersburg
                    Lon = 30.3350986,
                    AccuracyM = 25,
                },
                AddressText = "point-conflict-test",
            }),
        };
        createMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);
        var createResponse = await _client.SendAsync(createMessage);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreateIncidentResponseDto>();
        Assert.NotNull(created);

        var alerts = await ListIncidentAlertsAsync(created!.IncidentId, operatorToken);
        Assert.Contains(alerts, x => x.RuleCode == "INCIDENT_POINT_CONFLICT");
    }

    [Fact]
    public async Task SecondGroupMissingAlert_AppearsAfterFirstDispatch_AndResolvesAfterSecond()
    {
        var operatorToken = await LoginAndGetAccessTokenAsync("operator", "operator-pass");
        var incidentId = await _factory.SeedIncidentAsync(TestUsers.Client.UserId, Chop.Domain.Incidents.IncidentStatus.Acked, "alerts-second-group-test");

        // First dispatch -> should create SECOND_GROUP_MISSING alert.
        using var dispatch1 = new HttpRequestMessage(HttpMethod.Post, $"/api/operator/incidents/{incidentId}/dispatch")
        {
            Content = JsonContent.Create(new CreateDispatchDto
            {
                Method = "APP",
                Recipients =
                [
                    new DispatchRecipientInputDto { Type = "GUARD", Id = TestUsers.Guard.UserId },
                ],
            }),
        };
        dispatch1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
        var dispatch1Resp = await _client.SendAsync(dispatch1);
        dispatch1Resp.EnsureSuccessStatusCode();

        var after1 = await ListIncidentAlertsAsync(incidentId, operatorToken);
        Assert.Contains(after1, x => x.RuleCode == "INCIDENT_SECOND_GROUP_MISSING" && x.Status == "OPEN");

        // Second dispatch -> should resolve SECOND_GROUP_MISSING alert (not returned when includeResolved=false).
        using var dispatch2 = new HttpRequestMessage(HttpMethod.Post, $"/api/operator/incidents/{incidentId}/dispatch")
        {
            Content = JsonContent.Create(new CreateDispatchDto
            {
                Method = "APP",
                Recipients =
                [
                    new DispatchRecipientInputDto { Type = "GUARD", Id = TestUsers.Guard.UserId },
                ],
            }),
        };
        dispatch2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
        var dispatch2Resp = await _client.SendAsync(dispatch2);
        dispatch2Resp.EnsureSuccessStatusCode();

        var after2 = await ListIncidentAlertsAsync(incidentId, operatorToken);
        Assert.DoesNotContain(after2, x => x.RuleCode == "INCIDENT_SECOND_GROUP_MISSING");
    }

    [Fact]
    public async Task NoAccept_ThenGuardNoPing_Alerts_Workflow()
    {
        var operatorToken = await LoginAndGetAccessTokenAsync("operator", "operator-pass");
        var guardToken = await LoginAndGetAccessTokenAsync("guard", "guard-pass");
        var incidentId = await _factory.SeedIncidentAsync(TestUsers.Client.UserId, Chop.Domain.Incidents.IncidentStatus.Acked, "alerts-no-accept-no-ping");

        // Create dispatch as operator.
        using var dispatch = new HttpRequestMessage(HttpMethod.Post, $"/api/operator/incidents/{incidentId}/dispatch")
        {
            Content = JsonContent.Create(new CreateDispatchDto
            {
                Method = "APP",
                Recipients =
                [
                    new DispatchRecipientInputDto { Type = "GUARD", Id = TestUsers.Guard.UserId },
                ],
            }),
        };
        dispatch.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
        var dispatchResp = await _client.SendAsync(dispatch);
        dispatchResp.EnsureSuccessStatusCode();

        var afterDispatch = await ListIncidentAlertsAsync(incidentId, operatorToken);
        Assert.Contains(afterDispatch, x => x.RuleCode == "INCIDENT_NO_ACCEPT");

        // Guard accepts: resolves NO_ACCEPT and creates GUARD_NO_PING (until first ping).
        using var accept = new HttpRequestMessage(HttpMethod.Post, $"/api/guard/incidents/{incidentId}/accept")
        {
            Content = JsonContent.Create(new GuardAcceptIncidentDto { Comment = "ok" }),
        };
        accept.Headers.Authorization = new AuthenticationHeaderValue("Bearer", guardToken);
        var acceptResp = await _client.SendAsync(accept);
        acceptResp.EnsureSuccessStatusCode();

        var afterAccept = await ListIncidentAlertsAsync(incidentId, operatorToken);
        Assert.DoesNotContain(afterAccept, x => x.RuleCode == "INCIDENT_NO_ACCEPT");
        Assert.Contains(afterAccept, x => x.RuleCode == "INCIDENT_GUARD_NO_PING");

        // First ping: resolves GUARD_NO_PING.
        using var ping = new HttpRequestMessage(HttpMethod.Post, "/api/guard/location/ping")
        {
            Content = JsonContent.Create(new GuardLocationPingDto
            {
                IncidentId = incidentId,
                Lat = 55.751244,
                Lon = 37.618423,
                AccuracyM = 10,
            }),
        };
        ping.Headers.Authorization = new AuthenticationHeaderValue("Bearer", guardToken);
        var pingResp = await _client.SendAsync(ping);
        Assert.Equal(HttpStatusCode.NoContent, pingResp.StatusCode);

        var afterPing = await ListIncidentAlertsAsync(incidentId, operatorToken);
        Assert.DoesNotContain(afterPing, x => x.RuleCode == "INCIDENT_GUARD_NO_PING");
    }

    [Fact]
    public async Task SlaAlerts_NoAcceptStuck_And_GuardOffline_AppearByTime()
    {
        var operatorToken = await LoginAndGetAccessTokenAsync("operator", "operator-pass");
        var guardToken = await LoginAndGetAccessTokenAsync("guard", "guard-pass");
        var incidentId = await _factory.SeedIncidentAsync(TestUsers.Client.UserId, Chop.Domain.Incidents.IncidentStatus.Acked, "alerts-sla-time-based");

        using var dispatch = new HttpRequestMessage(HttpMethod.Post, $"/api/operator/incidents/{incidentId}/dispatch")
        {
            Content = JsonContent.Create(new CreateDispatchDto
            {
                Method = "APP",
                Recipients =
                [
                    new DispatchRecipientInputDto { Type = "GUARD", Id = TestUsers.Guard.UserId },
                ],
            }),
        };
        dispatch.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
        var dispatchResp = await _client.SendAsync(dispatch);
        dispatchResp.EnsureSuccessStatusCode();

        await WaitUntilAsync(
            async () =>
            {
                var alerts = await ListIncidentAlertsAsync(incidentId, operatorToken);
                return alerts.Any(x => x.RuleCode == "INCIDENT_NO_ACCEPT_STUCK");
            },
            TimeSpan.FromSeconds(5));

        using var accept = new HttpRequestMessage(HttpMethod.Post, $"/api/guard/incidents/{incidentId}/accept")
        {
            Content = JsonContent.Create(new GuardAcceptIncidentDto { Comment = "accept" }),
        };
        accept.Headers.Authorization = new AuthenticationHeaderValue("Bearer", guardToken);
        var acceptResp = await _client.SendAsync(accept);
        acceptResp.EnsureSuccessStatusCode();

        using var ping = new HttpRequestMessage(HttpMethod.Post, "/api/guard/location/ping")
        {
            Content = JsonContent.Create(new GuardLocationPingDto
            {
                IncidentId = incidentId,
                Lat = 55.751,
                Lon = 37.618,
            }),
        };
        ping.Headers.Authorization = new AuthenticationHeaderValue("Bearer", guardToken);
        var pingResp = await _client.SendAsync(ping);
        Assert.Equal(HttpStatusCode.NoContent, pingResp.StatusCode);

        await WaitUntilAsync(
            async () =>
            {
                var alerts = await ListIncidentAlertsAsync(incidentId, operatorToken);
                return alerts.Any(x => x.RuleCode == "INCIDENT_GUARD_OFFLINE");
            },
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SlaAlerts_StuckInStatus_AppearsForStaleEnRoute()
    {
        var operatorToken = await LoginAndGetAccessTokenAsync("operator", "operator-pass");
        var staleUpdatedAt = DateTime.UtcNow.AddSeconds(-5);
        var incidentId = await _factory.SeedIncidentAsync(
            TestUsers.Client.UserId,
            Chop.Domain.Incidents.IncidentStatus.EnRoute,
            "alerts-sla-stuck-in-status",
            lastUpdatedAtUtc: staleUpdatedAt);

        await WaitUntilAsync(
            async () =>
            {
                var alerts = await ListIncidentAlertsAsync(incidentId, operatorToken);
                return alerts.Any(x => x.RuleCode == "INCIDENT_STUCK_IN_STATUS");
            },
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Client_CannotAccess_OperatorAlerts()
    {
        var clientToken = await LoginAndGetAccessTokenAsync("client", "client-pass");
        var incidentId = await _factory.SeedIncidentAsync(TestUsers.Client.UserId, Chop.Domain.Incidents.IncidentStatus.Acked, "client-forbidden-alerts");

        using var listMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/operator/incidents/{incidentId}/alerts");
        listMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);
        var response = await _client.SendAsync(listMessage);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<IReadOnlyCollection<AlertListItemDto>> ListIncidentAlertsAsync(Guid incidentId, string operatorToken)
    {
        using var list = new HttpRequestMessage(HttpMethod.Get, $"/api/operator/incidents/{incidentId}/alerts?includeResolved=false");
        list.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
        var resp = await _client.SendAsync(list);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<IReadOnlyCollection<AlertListItemDto>>()) ?? Array.Empty<AlertListItemDto>();
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> predicate, TimeSpan timeout)
    {
        var started = DateTime.UtcNow;
        while (DateTime.UtcNow - started < timeout)
        {
            if (await predicate())
            {
                return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("Condition not reached within timeout.");
    }

    private async Task<string> LoginAndGetAccessTokenAsync(string login, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Login = login,
            Password = password,
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(payload);
        return payload!.AccessToken;
    }
}
