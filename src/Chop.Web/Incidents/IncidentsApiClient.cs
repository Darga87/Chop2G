using System.Net.Http.Headers;
using System.Net.Http.Json;
using Chop.Shared.Contracts.Alerts;
using Chop.Shared.Contracts.Auth;
using Chop.Shared.Contracts.Common;
using Chop.Shared.Contracts.Incidents;
using Chop.Web.Auth;

namespace Chop.Web.Incidents;

public sealed class IncidentsApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly WebAuthSession _session;

    public IncidentsApiClient(IHttpClientFactory httpClientFactory, WebAuthSession session)
    {
        _httpClientFactory = httpClientFactory;
        _session = session;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("Api");
        using var response = await client.PostAsJsonAsync("/api/auth/login", request, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, "/api/auth/login", cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<LoginResponseDto>(cancellationToken);
        return payload ?? throw new InvalidOperationException("Пустой ответ входа.");
    }

    public async Task<PagedResult<IncidentListItemDto>> GetIncidentsAsync(
        string? status,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}",
        };

        if (!string.IsNullOrWhiteSpace(status))
        {
            query.Add($"status={Uri.EscapeDataString(status)}");
        }

        if (from.HasValue)
        {
            query.Add($"from={Uri.EscapeDataString(from.Value.ToString("O"))}");
        }

        if (to.HasValue)
        {
            query.Add($"to={Uri.EscapeDataString(to.Value.ToString("O"))}");
        }

        var uri = "/api/operator/incidents?" + string.Join("&", query);
        using var request = CreateAuthedRequest(HttpMethod.Get, uri);
        var client = _httpClientFactory.CreateClient("Api");
        using var response = await client.SendAsync(request, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, uri, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<PagedResult<IncidentListItemDto>>(cancellationToken);
        return payload ?? throw new InvalidOperationException("Пустой ответ списка инцидентов.");
    }

    public async Task<IncidentDetailsDto?> GetIncidentByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var uri = $"/api/operator/incidents/{id}";
        using var request = CreateAuthedRequest(HttpMethod.Get, uri);
        var client = _httpClientFactory.CreateClient("Api");
        using var response = await client.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessOrThrowAsync(response, uri, cancellationToken);
        return await response.Content.ReadFromJsonAsync<IncidentDetailsDto>(cancellationToken);
    }

    public async Task<IncidentDto> ChangeStatusAsync(Guid id, ChangeIncidentStatusDto request, CancellationToken cancellationToken)
    {
        var uri = $"/api/operator/incidents/{id}/status";
        using var message = CreateAuthedRequest(HttpMethod.Post, uri);
        message.Content = JsonContent.Create(request);
        var client = _httpClientFactory.CreateClient("Api");
        using var response = await client.SendAsync(message, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, uri, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<IncidentDto>(cancellationToken);
        return payload ?? throw new InvalidOperationException("Пустой ответ смены статуса.");
    }

    public async Task<DispatchDto> CreateDispatchAsync(Guid incidentId, CreateDispatchDto request, CancellationToken cancellationToken)
    {
        var uri = $"/api/operator/incidents/{incidentId}/dispatch";
        using var message = CreateAuthedRequest(HttpMethod.Post, uri);
        message.Content = JsonContent.Create(request);
        var client = _httpClientFactory.CreateClient("Api");
        using var response = await client.SendAsync(message, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, uri, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<DispatchDto>(cancellationToken);
        return payload ?? throw new InvalidOperationException("Пустой ответ диспетчеризации.");
    }

    public async Task<IReadOnlyCollection<AlertListItemDto>> GetIncidentAlertsAsync(Guid incidentId, bool includeResolved, CancellationToken cancellationToken)
    {
        var uri = $"/api/operator/incidents/{incidentId}/alerts?includeResolved={includeResolved.ToString().ToLowerInvariant()}";
        using var request = CreateAuthedRequest(HttpMethod.Get, uri);
        var client = _httpClientFactory.CreateClient("Api");
        using var response = await client.SendAsync(request, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, uri, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<AlertListItemDto>>(cancellationToken);
        return payload ?? Array.Empty<AlertListItemDto>();
    }

    public async Task AckAlertAsync(Guid alertId, string? comment, CancellationToken cancellationToken)
    {
        var uri = $"/api/operator/alerts/{alertId}/ack";
        using var message = CreateAuthedRequest(HttpMethod.Post, uri);
        message.Content = JsonContent.Create(new AckAlertRequestDto { Comment = comment });
        var client = _httpClientFactory.CreateClient("Api");
        using var response = await client.SendAsync(message, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, uri, cancellationToken);
    }

    public async Task ResolveAlertAsync(Guid alertId, string? comment, CancellationToken cancellationToken)
    {
        var uri = $"/api/operator/alerts/{alertId}/resolve";
        using var message = CreateAuthedRequest(HttpMethod.Post, uri);
        message.Content = JsonContent.Create(new ResolveAlertRequestDto { Comment = comment });
        var client = _httpClientFactory.CreateClient("Api");
        using var response = await client.SendAsync(message, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, uri, cancellationToken);
    }

    public async Task AssignAlertAsync(Guid alertId, string? assigneeUserId, string? comment, CancellationToken cancellationToken)
    {
        var uri = $"/api/operator/alerts/{alertId}/assign";
        using var message = CreateAuthedRequest(HttpMethod.Post, uri);
        message.Content = JsonContent.Create(new AssignAlertRequestDto
        {
            AssigneeUserId = assigneeUserId,
            Comment = comment,
        });
        var client = _httpClientFactory.CreateClient("Api");
        using var response = await client.SendAsync(message, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, uri, cancellationToken);
    }

    public async Task OverrideAlertAsync(Guid alertId, string comment, CancellationToken cancellationToken)
    {
        var uri = $"/api/operator/alerts/{alertId}/override";
        using var message = CreateAuthedRequest(HttpMethod.Post, uri);
        message.Content = JsonContent.Create(new OverrideAlertRequestDto { Comment = comment });
        var client = _httpClientFactory.CreateClient("Api");
        using var response = await client.SendAsync(message, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, uri, cancellationToken);
    }

    private HttpRequestMessage CreateAuthedRequest(HttpMethod method, string uri)
    {
        var request = new HttpRequestMessage(method, uri);
        if (!string.IsNullOrWhiteSpace(_session.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.AccessToken);
        }

        return request;
    }

    private static async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response, string uri, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            ApiErrorFormatter.Build(response.StatusCode, uri, body),
            null,
            response.StatusCode);
    }
}
