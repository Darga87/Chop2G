using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Chop.Shared.Contracts.Auth;
using Chop.Shared.Contracts.Common;
using Chop.Shared.Contracts.Incidents;

namespace Chop.Api.Tests;

public sealed class IncidentsAuthTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public IncidentsAuthTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OperatorEndpoint_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/operator/incidents");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OperatorEndpoint_WithClientToken_Returns403()
    {
        var clientToken = await LoginAndGetAccessTokenAsync("client", "client-pass");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/operator/incidents");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OperatorEndpoint_ForgedHeadersWithoutToken_DoNotGrantAccess()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/operator/incidents");
        request.Headers.Add("X-User-Id", "attacker");
        request.Headers.Add("X-User-Roles", "OPERATOR,SUPERADMIN");

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostIncidentAndList_WithValidJwt_Works()
    {
        var clientToken = await LoginAndGetAccessTokenAsync("client", "client-pass");
        var operatorToken = await LoginAndGetAccessTokenAsync("operator", "operator-pass");

        var createRequest = new CreateIncidentDto
        {
            ClientLocation = new IncidentLocationDto { Lat = 55.7522, Lon = 37.6156, AccuracyM = 10 },
            DeviceTimeUtc = DateTime.UtcNow,
            AddressText = "JWT create test",
        };

        using var createMessage = new HttpRequestMessage(HttpMethod.Post, "/api/incidents")
        {
            Content = JsonContent.Create(createRequest),
        };
        createMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

        var createResponse = await _client.SendAsync(createMessage);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var listMessage = new HttpRequestMessage(HttpMethod.Get, "/api/operator/incidents?page=1&pageSize=10");
        listMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);

        var listResponse = await _client.SendAsync(listMessage);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var payload = await listResponse.Content.ReadFromJsonAsync<PagedResult<IncidentListItemDto>>();
        Assert.NotNull(payload);
        Assert.True(payload!.TotalCount >= 1);
    }

    [Fact]
    public async Task GetIncidentDetails_ReturnsExpectedShape()
    {
        await _factory.SeedClientProfileAsync(
            userId: TestUsers.ClientDetailsShape.UserId,
            fullName: "Иванов Иван Иванович",
            phones:
            [
                ("+79990001122", "PRIMARY", true),
                ("+79990003344", "EMERGENCY", false),
            ],
            addresses:
            [
                ("Дом", "Москва, Тверская 1", 55.7558, 37.6176, true),
                ("Офис", "Москва, Арбат 10", null, null, false),
            ]);

        var clientToken = await LoginAndGetAccessTokenAsync("client-details-shape", "client-details-shape-pass");
        var operatorToken = await LoginAndGetAccessTokenAsync("operator", "operator-pass");

        var createRequest = new CreateIncidentDto
        {
            ClientLocation = new IncidentLocationDto { Lat = 55.75, Lon = 37.61 },
            AddressText = "Shape test",
            DeviceTimeUtc = DateTime.UtcNow,
        };

        using var createMessage = new HttpRequestMessage(HttpMethod.Post, "/api/incidents")
        {
            Content = JsonContent.Create(createRequest),
        };
        createMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);
        var createResponse = await _client.SendAsync(createMessage);
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<CreateIncidentResponseDto>();
        Assert.NotNull(created);

        using var detailsMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/operator/incidents/{created!.IncidentId}");
        detailsMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
        var detailsResponse = await _client.SendAsync(detailsMessage);
        detailsResponse.EnsureSuccessStatusCode();

        var payload = await detailsResponse.Content.ReadFromJsonAsync<IncidentDetailsDto>();
        Assert.NotNull(payload);
        Assert.Equal("Иванов Иван Иванович", payload!.Client.FullName);
        Assert.True(payload.Client.Phones.Count >= 2);
        Assert.True(payload.Addresses.Count >= 2);
        Assert.NotEmpty(payload.History);
        Assert.False(string.IsNullOrWhiteSpace(payload.Status));
        Assert.True(payload.CreatedAt <= payload.LastUpdatedAt);
        Assert.Contains(payload.Client.Phones, p => p.IsPrimary && p.Type == "PRIMARY");
        Assert.Contains(payload.Addresses, a => a.IsPrimary && a.Label == "Дом" && a.Location is not null);
        Assert.Contains(payload.Addresses, a => !a.IsPrimary && a.Label == "Офис" && a.Location is null);
        Assert.Contains(payload.History, h => h.ToStatus == "NEW");
        Assert.Contains(payload.History, h => h.ToStatus == "ACKED");
    }

    [Fact]
    public async Task GetIncidentDetails_AccessAllowedForOperatorAndAdmin()
    {
        var clientToken = await LoginAndGetAccessTokenAsync("client", "client-pass");
        var operatorToken = await LoginAndGetAccessTokenAsync("operator", "operator-pass");
        var adminToken = await LoginAndGetAccessTokenAsync("admin", "admin-pass");
        var superAdminToken = await LoginAndGetAccessTokenAsync("superadmin", "superadmin-pass");

        using var createMessage = new HttpRequestMessage(HttpMethod.Post, "/api/incidents")
        {
            Content = JsonContent.Create(new CreateIncidentDto { AddressText = "Access test" }),
        };
        createMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);
        var createResponse = await _client.SendAsync(createMessage);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreateIncidentResponseDto>();
        Assert.NotNull(created);

        using var operatorReq = new HttpRequestMessage(HttpMethod.Get, $"/api/operator/incidents/{created!.IncidentId}");
        operatorReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
        var operatorRes = await _client.SendAsync(operatorReq);
        Assert.Equal(HttpStatusCode.OK, operatorRes.StatusCode);

        using var adminReq = new HttpRequestMessage(HttpMethod.Get, $"/api/operator/incidents/{created.IncidentId}");
        adminReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var adminRes = await _client.SendAsync(adminReq);
        Assert.Equal(HttpStatusCode.OK, adminRes.StatusCode);

        using var superAdminReq = new HttpRequestMessage(HttpMethod.Get, $"/api/operator/incidents/{created.IncidentId}");
        superAdminReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", superAdminToken);
        var superAdminRes = await _client.SendAsync(superAdminReq);
        Assert.Equal(HttpStatusCode.OK, superAdminRes.StatusCode);
    }

    [Fact]
    public async Task GetIncidentDetails_WithoutToken_Returns401()
    {
        var clientToken = await LoginAndGetAccessTokenAsync("client", "client-pass");
        using var createMessage = new HttpRequestMessage(HttpMethod.Post, "/api/incidents")
        {
            Content = JsonContent.Create(new CreateIncidentDto { AddressText = "Details auth no-token" }),
        };
        createMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);
        var createResponse = await _client.SendAsync(createMessage);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreateIncidentResponseDto>();
        Assert.NotNull(created);

        var detailsResponse = await _client.GetAsync($"/api/operator/incidents/{created!.IncidentId}");
        Assert.Equal(HttpStatusCode.Unauthorized, detailsResponse.StatusCode);
    }

    [Fact]
    public async Task GetIncidentDetails_WithClientToken_Returns403()
    {
        var clientToken = await LoginAndGetAccessTokenAsync("client", "client-pass");
        using var createMessage = new HttpRequestMessage(HttpMethod.Post, "/api/incidents")
        {
            Content = JsonContent.Create(new CreateIncidentDto { AddressText = "Details auth client-role" }),
        };
        createMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);
        var createResponse = await _client.SendAsync(createMessage);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreateIncidentResponseDto>();
        Assert.NotNull(created);

        using var detailsReq = new HttpRequestMessage(HttpMethod.Get, $"/api/operator/incidents/{created!.IncidentId}");
        detailsReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);
        var detailsResponse = await _client.SendAsync(detailsReq);
        Assert.Equal(HttpStatusCode.Forbidden, detailsResponse.StatusCode);
    }

    [Fact]
    public async Task PostIncidents_WithSameIdempotencyKey_ReturnsSameIncident()
    {
        var clientToken = await LoginAndGetAccessTokenAsync("client-dedup-key", "client-dedup-key-pass");
        var operatorToken = await LoginAndGetAccessTokenAsync("operator", "operator-pass");
        var beforeCount = await GetTotalCountAsync(operatorToken);

        const string key = "idem-123";
        var req = new CreateIncidentDto
        {
            AddressText = "idempotency-test",
            ClientLocation = new IncidentLocationDto { Lat = 10.1, Lon = 20.2 },
        };

        var first = await CreateIncidentAsync(clientToken, req, key);
        var second = await CreateIncidentAsync(clientToken, req, key);

        Assert.Equal(first.IncidentId, second.IncidentId);

        var afterCount = await GetTotalCountAsync(operatorToken);
        Assert.Equal(beforeCount + 1, afterCount);
    }

    [Fact]
    public async Task PostIncidents_WithSameIdempotencyKeyAndDifferentPayload_Returns409()
    {
        var clientToken = await LoginAndGetAccessTokenAsync("client-idem-conflict", "client-idem-conflict-pass");

        const string key = "idem-conflict-1";
        var first = new CreateIncidentDto
        {
            AddressText = "first-payload",
            ClientLocation = new IncidentLocationDto { Lat = 1.0, Lon = 2.0 },
        };
        var second = new CreateIncidentDto
        {
            AddressText = "second-payload",
            ClientLocation = new IncidentLocationDto { Lat = 1.0, Lon = 2.0 },
        };

        var firstResponse = await CreateIncidentHttpAsync(clientToken, first, key);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var secondResponse = await CreateIncidentHttpAsync(clientToken, second, key);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task PostIncidents_WithoutKey_WithinDedupWindow_ReturnsSameActiveIncident()
    {
        var clientToken = await LoginAndGetAccessTokenAsync("client-dedup-window", "client-dedup-window-pass");
        var operatorToken = await LoginAndGetAccessTokenAsync("operator", "operator-pass");
        var beforeCount = await GetTotalCountAsync(operatorToken);

        var req = new CreateIncidentDto
        {
            AddressText = "dedup-window-test",
            ClientLocation = new IncidentLocationDto { Lat = 50.0, Lon = 30.0 },
        };

        var first = await CreateIncidentAsync(clientToken, req, null);
        var second = await CreateIncidentAsync(clientToken, req, null);

        Assert.Equal(first.IncidentId, second.IncidentId);

        var afterCount = await GetTotalCountAsync(operatorToken);
        Assert.Equal(beforeCount + 1, afterCount);
    }

    [Fact]
    public async Task PostIncidents_WithoutKey_ConcurrentRequests_ReturnSingleIncident()
    {
        var clientToken = await LoginAndGetAccessTokenAsync("client-concurrent-no-key", "client-concurrent-no-key-pass");
        var operatorToken = await LoginAndGetAccessTokenAsync("operator", "operator-pass");
        var beforeCount = await GetTotalCountAsync(operatorToken);

        var req = new CreateIncidentDto
        {
            AddressText = "concurrent-no-key-test",
            ClientLocation = new IncidentLocationDto { Lat = 44.1, Lon = 38.2 },
        };

        var tasks = Enumerable.Range(0, 6)
            .Select(_ => CreateIncidentAsync(clientToken, req, null));
        var responses = await Task.WhenAll(tasks);

        var distinctIncidentIds = responses.Select(x => x.IncidentId).Distinct().ToArray();
        Assert.Single(distinctIncidentIds);

        var afterCount = await GetTotalCountAsync(operatorToken);
        Assert.Equal(beforeCount + 1, afterCount);
    }

    [Fact]
    public async Task CreateDispatch_FromAckedIncident_ChangesStatusAndReturnsDispatch()
    {
        var operatorToken = await LoginAndGetAccessTokenAsync("operator", "operator-pass");
        var incidentId = await _factory.SeedIncidentAsync(TestUsers.Client.UserId, Chop.Domain.Incidents.IncidentStatus.Acked, "dispatch-happy-path");

        var dispatchRequest = new CreateDispatchDto
        {
            Method = "APP",
            Comment = "Dispatching nearest guard",
            Recipients =
            [
                new DispatchRecipientInputDto
                {
                    Type = "GUARD",
                    Id = TestUsers.Guard.UserId,
                    DistanceMeters = 420,
                },
            ],
        };

        using var dispatchMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/operator/incidents/{incidentId}/dispatch")
        {
            Content = JsonContent.Create(dispatchRequest),
        };
        dispatchMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
        var dispatchResponse = await _client.SendAsync(dispatchMessage);
        dispatchResponse.EnsureSuccessStatusCode();

        var dispatch = await dispatchResponse.Content.ReadFromJsonAsync<DispatchDto>();
        Assert.NotNull(dispatch);
        Assert.Equal(incidentId, dispatch!.IncidentId);
        Assert.Equal("APP", dispatch.Method);
        Assert.Single(dispatch.Recipients);
        Assert.Equal("GUARD", dispatch.Recipients.First().Type);
        Assert.Equal(TestUsers.Guard.UserId, dispatch.Recipients.First().RecipientId);

        using var detailsMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/operator/incidents/{incidentId}");
        detailsMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
        var detailsResponse = await _client.SendAsync(detailsMessage);
        detailsResponse.EnsureSuccessStatusCode();
        var details = await detailsResponse.Content.ReadFromJsonAsync<IncidentDetailsDto>();
        Assert.NotNull(details);
        Assert.Equal("DISPATCHED", details!.Status);
        Assert.NotEmpty(details.Dispatches);
    }

    [Fact]
    public async Task CreateDispatch_WithoutRecipients_Returns400()
    {
        var operatorToken = await LoginAndGetAccessTokenAsync("operator", "operator-pass");
        var incidentId = await _factory.SeedIncidentAsync(TestUsers.Client.UserId, Chop.Domain.Incidents.IncidentStatus.Acked, "dispatch-no-recipients");

        using var dispatchMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/operator/incidents/{incidentId}/dispatch")
        {
            Content = JsonContent.Create(new CreateDispatchDto
            {
                Method = "APP",
                Recipients = [],
            }),
        };
        dispatchMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
        var dispatchResponse = await _client.SendAsync(dispatchMessage);

        Assert.Equal(HttpStatusCode.BadRequest, dispatchResponse.StatusCode);
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

    private async Task<CreateIncidentResponseDto> CreateIncidentAsync(string clientToken, CreateIncidentDto dto, string? idempotencyKey)
    {
        var response = await CreateIncidentHttpAsync(clientToken, dto, idempotencyKey);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CreateIncidentResponseDto>();
        Assert.NotNull(payload);
        return payload!;
    }

    private async Task<HttpResponseMessage> CreateIncidentHttpAsync(string clientToken, CreateIncidentDto dto, string? idempotencyKey)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/incidents")
        {
            Content = JsonContent.Create(dto),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            message.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return await _client.SendAsync(message);
    }

    private async Task<int> GetTotalCountAsync(string operatorToken)
    {
        using var listMessage = new HttpRequestMessage(HttpMethod.Get, "/api/operator/incidents?page=1&pageSize=500");
        listMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);

        var listResponse = await _client.SendAsync(listMessage);
        listResponse.EnsureSuccessStatusCode();
        var payload = await listResponse.Content.ReadFromJsonAsync<PagedResult<IncidentListItemDto>>();
        Assert.NotNull(payload);
        return payload!.TotalCount;
    }
}
