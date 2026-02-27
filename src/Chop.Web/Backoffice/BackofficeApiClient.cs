using System.Net.Http.Headers;
using System.Net.Http.Json;
using Chop.Shared.Contracts.Auth;
using Chop.Shared.Contracts.Backoffice;
using Chop.Web.Auth;
using Microsoft.JSInterop;

namespace Chop.Web.Backoffice;

public sealed class BackofficeApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly WebAuthSession _session;
    private readonly IJSRuntime _js;

    public BackofficeApiClient(IHttpClientFactory httpClientFactory, WebAuthSession session, IJSRuntime js)
    {
        _httpClientFactory = httpClientFactory;
        _session = session;
        _js = js;
    }

    public async Task<IReadOnlyCollection<GuardItemDto>> GetGuardsAsync(string? search, string? status, bool onShiftOnly, CancellationToken cancellationToken)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search)}");
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query.Add($"status={Uri.EscapeDataString(status)}");
        }

        query.Add($"onShiftOnly={onShiftOnly.ToString().ToLowerInvariant()}");

        var uri = "/api/hr/guards?" + string.Join("&", query);
        using var response = await SendAuthedAsync(HttpMethod.Get, uri, null, cancellationToken);
        return await ReadAsync<GuardItemDto>(response, cancellationToken);
    }

    public async Task ToggleGuardActiveAsync(Guid guardId, CancellationToken cancellationToken)
    {
        using var response = await SendAuthedAsync(HttpMethod.Post, $"/api/hr/guards/{guardId}/toggle-active", JsonContent.Create(new { }), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<GuardItemDto> CreateGuardAsync(CreateGuardRequestDto request, CancellationToken cancellationToken)
    {
        using var response = await SendAuthedAsync(HttpMethod.Post, "/api/hr/guards", JsonContent.Create(request), cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<GuardItemDto>(cancellationToken);
        return payload ?? throw new InvalidOperationException("Не удалось прочитать ответ по охраннику.");
    }

    public async Task<GuardItemDto> UpdateGuardAsync(Guid guardId, UpdateGuardRequestDto request, CancellationToken cancellationToken)
    {
        using var response = await SendAuthedAsync(HttpMethod.Put, $"/api/hr/guards/{guardId:D}", JsonContent.Create(request), cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<GuardItemDto>(cancellationToken);
        return payload ?? throw new InvalidOperationException("Не удалось прочитать ответ по обновлению охранника.");
    }

    public async Task<IReadOnlyCollection<GuardGroupItemDto>> GetGuardGroupsAsync(CancellationToken cancellationToken)
    {
        using var response = await SendAuthedAsync(HttpMethod.Get, "/api/hr/groups", null, cancellationToken);
        return await ReadAsync<GuardGroupItemDto>(response, cancellationToken);
    }

    public async Task<GuardGroupItemDto> CreateGuardGroupAsync(string name, CancellationToken cancellationToken)
    {
        var request = new CreateGuardGroupRequestDto { Name = name };
        using var response = await SendAuthedAsync(HttpMethod.Post, "/api/hr/groups", JsonContent.Create(request), cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<GuardGroupItemDto>(cancellationToken);
        return payload ?? throw new InvalidOperationException("Не удалось прочитать ответ по группе охраны.");
    }

    public async Task AddGuardToGroupAsync(Guid groupId, string guardUserId, bool isCommander, CancellationToken cancellationToken)
    {
        var request = new AddGuardToGroupRequestDto
        {
            GuardUserId = guardUserId,
            IsCommander = isCommander,
        };

        using var response = await SendAuthedAsync(HttpMethod.Post, $"/api/hr/groups/{groupId:D}/members", JsonContent.Create(request), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveGuardFromGroupAsync(Guid groupId, string guardUserId, CancellationToken cancellationToken)
    {
        using var response = await SendAuthedAsync(HttpMethod.Delete, $"/api/hr/groups/{groupId:D}/members/{Uri.EscapeDataString(guardUserId)}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyCollection<GuardShiftItemDto>> GetActiveShiftsAsync(CancellationToken cancellationToken)
    {
        using var response = await SendAuthedAsync(HttpMethod.Get, "/api/hr/shifts/active", null, cancellationToken);
        return await ReadAsync<GuardShiftItemDto>(response, cancellationToken);
    }

    public async Task<GuardShiftItemDto> StartShiftAsync(StartGuardShiftRequestDto request, CancellationToken cancellationToken)
    {
        using var response = await SendAuthedAsync(HttpMethod.Post, "/api/hr/shifts/start", JsonContent.Create(request), cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<GuardShiftItemDto>(cancellationToken);
        return payload ?? throw new InvalidOperationException("Не удалось прочитать ответ по началу смены.");
    }

    public async Task EndShiftAsync(string guardUserId, CancellationToken cancellationToken)
    {
        var request = new EndGuardShiftRequestDto { GuardUserId = guardUserId };
        using var response = await SendAuthedAsync(HttpMethod.Post, "/api/hr/shifts/end", JsonContent.Create(request), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyCollection<AdminClientItemDto>> GetClientsAsync(string? search, string? billing, bool debtOnly, CancellationToken cancellationToken)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search)}");
        }

        if (!string.IsNullOrWhiteSpace(billing))
        {
            query.Add($"billing={Uri.EscapeDataString(billing)}");
        }

        query.Add($"debtOnly={debtOnly.ToString().ToLowerInvariant()}");

        var uri = "/api/admin/clients?" + string.Join("&", query);
        using var response = await SendAuthedAsync(HttpMethod.Get, uri, null, cancellationToken);
        return await ReadAsync<AdminClientItemDto>(response, cancellationToken);
    }

    public async Task<AdminClientDetailsDto> GetClientByIdAsync(Guid clientId, CancellationToken cancellationToken)
    {
        using var response = await SendAuthedAsync(HttpMethod.Get, $"/api/admin/clients/{clientId:D}", null, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<AdminClientDetailsDto>(cancellationToken);
        return payload ?? throw new InvalidOperationException("Не удалось прочитать ответ по клиенту.");
    }

    public async Task<IReadOnlyCollection<BillingTariffItemDto>> GetTariffsAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var uri = $"/api/admin/tariffs?includeInactive={includeInactive.ToString().ToLowerInvariant()}";
        using var response = await SendAuthedAsync(HttpMethod.Get, uri, null, cancellationToken);
        return await ReadAsync<BillingTariffItemDto>(response, cancellationToken);
    }

    public async Task<CreateAdminClientResponseDto> CreateClientAsync(CreateAdminClientRequestDto request, CancellationToken cancellationToken)
    {
        using var response = await SendAuthedAsync(HttpMethod.Post, "/api/admin/clients", JsonContent.Create(request), cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<CreateAdminClientResponseDto>(cancellationToken);
        return payload ?? throw new InvalidOperationException("Не удалось прочитать ответ по созданию клиента.");
    }

    public async Task<AdminClientItemDto> UpdateClientAsync(Guid clientId, UpdateAdminClientRequestDto request, CancellationToken cancellationToken)
    {
        using var response = await SendAuthedAsync(HttpMethod.Put, $"/api/admin/clients/{clientId:D}", JsonContent.Create(request), cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<AdminClientItemDto>(cancellationToken);
        return payload ?? throw new InvalidOperationException("Не удалось прочитать ответ по обновлению клиента.");
    }

    public async Task<IReadOnlyCollection<OperatorForceItemDto>> GetForcesAsync(string? search, string? availability, bool onlineOnly, CancellationToken cancellationToken)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search)}");
        }

        if (!string.IsNullOrWhiteSpace(availability))
        {
            query.Add($"availability={Uri.EscapeDataString(availability)}");
        }

        query.Add($"onlineOnly={onlineOnly.ToString().ToLowerInvariant()}");

        var uri = "/api/operator/forces?" + string.Join("&", query);
        using var response = await SendAuthedAsync(HttpMethod.Get, uri, null, cancellationToken);
        return await ReadAsync<OperatorForceItemDto>(response, cancellationToken);
    }

    public async Task<IReadOnlyCollection<OperatorPointItemDto>> GetPointsAsync(string? search, string? type, bool includeInactive, CancellationToken cancellationToken)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search)}");
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query.Add($"type={Uri.EscapeDataString(type)}");
        }

        query.Add($"includeInactive={includeInactive.ToString().ToLowerInvariant()}");
        var uri = "/api/operator/points?" + string.Join("&", query);
        using var response = await SendAuthedAsync(HttpMethod.Get, uri, null, cancellationToken);
        return await ReadAsync<OperatorPointItemDto>(response, cancellationToken);
    }

    public async Task<OperatorPointItemDto> CreatePointAsync(CreateSecurityPointRequestDto request, CancellationToken cancellationToken)
    {
        using var response = await SendAuthedAsync(HttpMethod.Post, "/api/operator/points", JsonContent.Create(request), cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<OperatorPointItemDto>(cancellationToken);
        return payload ?? throw new InvalidOperationException("Не удалось прочитать ответ по точке.");
    }

    public async Task<OperatorPointItemDto> UpdatePointAsync(Guid pointId, UpdateSecurityPointRequestDto request, CancellationToken cancellationToken)
    {
        using var response = await SendAuthedAsync(HttpMethod.Put, $"/api/operator/points/{pointId:D}", JsonContent.Create(request), cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<OperatorPointItemDto>(cancellationToken);
        return payload ?? throw new InvalidOperationException("Не удалось прочитать ответ по обновлению точки.");
    }

    public async Task TogglePointActiveAsync(Guid pointId, CancellationToken cancellationToken)
    {
        using var response = await SendAuthedAsync(HttpMethod.Post, $"/api/operator/points/{pointId:D}/toggle-active", JsonContent.Create(new { }), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyCollection<SuperAdminSettingItemDto>> GetSuperAdminSettingsAsync(string? scope, CancellationToken cancellationToken)
    {
        var uri = string.IsNullOrWhiteSpace(scope)
            ? "/api/superadmin/settings"
            : $"/api/superadmin/settings?scope={Uri.EscapeDataString(scope)}";
        using var response = await SendAuthedAsync(HttpMethod.Get, uri, null, cancellationToken);
        return await ReadAsync<SuperAdminSettingItemDto>(response, cancellationToken);
    }

    public async Task<BillingTariffItemDto> CreateTariffAsync(UpsertBillingTariffRequestDto request, CancellationToken cancellationToken)
    {
        using var response = await SendAuthedAsync(HttpMethod.Post, "/api/superadmin/tariffs", JsonContent.Create(request), cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<BillingTariffItemDto>(cancellationToken);
        return payload ?? throw new InvalidOperationException("Не удалось прочитать ответ по созданию тарифа.");
    }

    public async Task<BillingTariffItemDto> UpdateTariffAsync(string code, UpsertBillingTariffRequestDto request, CancellationToken cancellationToken)
    {
        using var response = await SendAuthedAsync(HttpMethod.Put, $"/api/superadmin/tariffs/{Uri.EscapeDataString(code)}", JsonContent.Create(request), cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<BillingTariffItemDto>(cancellationToken);
        return payload ?? throw new InvalidOperationException("Не удалось прочитать ответ по обновлению тарифа.");
    }

    public async Task DeleteTariffAsync(string code, CancellationToken cancellationToken)
    {
        using var response = await SendAuthedAsync(HttpMethod.Delete, $"/api/superadmin/tariffs/{Uri.EscapeDataString(code)}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyCollection<SuperAdminAuditItemDto>> GetSuperAdminAuditAsync(string? search, CancellationToken cancellationToken)
    {
        var uri = string.IsNullOrWhiteSpace(search)
            ? "/api/superadmin/audit"
            : $"/api/superadmin/audit?search={Uri.EscapeDataString(search)}";
        using var response = await SendAuthedAsync(HttpMethod.Get, uri, null, cancellationToken);
        return await ReadAsync<SuperAdminAuditItemDto>(response, cancellationToken);
    }

    public async Task<IReadOnlyCollection<BackofficeUserItemDto>> GetSuperAdminUsersAsync(
        string? search,
        string? role,
        bool? active,
        CancellationToken cancellationToken)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search)}");
        }

        if (!string.IsNullOrWhiteSpace(role) && !string.Equals(role, "all", StringComparison.OrdinalIgnoreCase))
        {
            query.Add($"role={Uri.EscapeDataString(role)}");
        }

        if (active.HasValue)
        {
            query.Add($"active={active.Value.ToString().ToLowerInvariant()}");
        }

        var uri = query.Count == 0
            ? "/api/superadmin/users"
            : "/api/superadmin/users?" + string.Join("&", query);

        using var response = await SendAuthedAsync(HttpMethod.Get, uri, null, cancellationToken);
        return await ReadAsync<BackofficeUserItemDto>(response, cancellationToken);
    }

    public async Task<BackofficeUserItemDto> CreateSuperAdminUserAsync(CreateBackofficeUserRequestDto request, CancellationToken cancellationToken)
    {
        using var response = await SendAuthedAsync(HttpMethod.Post, "/api/superadmin/users", JsonContent.Create(request), cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<BackofficeUserItemDto>(cancellationToken);
        return payload ?? throw new InvalidOperationException("Не удалось прочитать данные созданного пользователя.");
    }

    public async Task AddSuperAdminUserRoleAsync(Guid userId, string role, CancellationToken cancellationToken)
    {
        var request = new ChangeBackofficeUserRoleRequestDto { Role = role };
        using var response = await SendAuthedAsync(HttpMethod.Post, $"/api/superadmin/users/{userId:D}/roles/add", JsonContent.Create(request), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveSuperAdminUserRoleAsync(Guid userId, string role, CancellationToken cancellationToken)
    {
        var request = new ChangeBackofficeUserRoleRequestDto { Role = role };
        using var response = await SendAuthedAsync(HttpMethod.Post, $"/api/superadmin/users/{userId:D}/roles/remove", JsonContent.Create(request), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ToggleSuperAdminUserActiveAsync(Guid userId, CancellationToken cancellationToken)
    {
        using var response = await SendAuthedAsync(HttpMethod.Post, $"/api/superadmin/users/{userId:D}/toggle-active", JsonContent.Create(new { }), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyCollection<PaymentImportItemDto>> GetImportsAsync(CancellationToken cancellationToken)
    {
        using var response = await SendAuthedAsync(HttpMethod.Get, "/api/admin/payments/imports", null, cancellationToken);
        return await ReadAsync<PaymentImportItemDto>(response, cancellationToken);
    }

    public async Task<PaymentImportItemDto> CreateImportDraftAsync(string fileName, string content, CancellationToken cancellationToken)
    {
        using var body = new MultipartFormDataContent();
        var fileContent = new StringContent(content);
        body.Add(fileContent, "file", fileName);
        using var response = await SendAuthedAsync(HttpMethod.Post, "/api/admin/payments/import", body, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<PaymentImportItemDto>(cancellationToken);
        return payload ?? throw new InvalidOperationException("Не удалось прочитать ответ по импорту платежей.");
    }

    public async Task<IReadOnlyCollection<PaymentImportRowItemDto>> GetImportRowsAsync(Guid importId, CancellationToken cancellationToken)
    {
        using var response = await SendAuthedAsync(HttpMethod.Get, $"/api/admin/payments/imports/{importId}/rows", null, cancellationToken);
        return await ReadAsync<PaymentImportRowItemDto>(response, cancellationToken);
    }

    public async Task ApplyImportAsync(Guid importId, CancellationToken cancellationToken)
    {
        using var response = await SendAuthedAsync(HttpMethod.Post, $"/api/admin/payments/imports/{importId}/apply", JsonContent.Create(new { }), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ManualMatchAsync(Guid importId, Guid rowId, string clientDisplayName, CancellationToken cancellationToken)
    {
        var request = new ManualMatchRequestDto { ClientDisplayName = clientDisplayName };
        using var response = await SendAuthedAsync(HttpMethod.Post, $"/api/admin/payments/imports/{importId}/rows/{rowId}/match", JsonContent.Create(request), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<HttpResponseMessage> SendAuthedAsync(HttpMethod method, string uri, HttpContent? content, CancellationToken cancellationToken)
    {
        var bufferedContent = await BufferContentAsync(content, cancellationToken);
        var response = await SendAuthedOnceAsync(method, uri, bufferedContent, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            var refreshed = await TryRefreshSessionAsync(cancellationToken);
            if (refreshed)
            {
                response = await SendAuthedOnceAsync(method, uri, bufferedContent, cancellationToken);
            }
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await ClearSessionAsync();
            }

            response.Dispose();
            throw new HttpRequestException(
                ApiErrorFormatter.Build(response.StatusCode, uri, body),
                null,
                response.StatusCode);
        }

        return response;
    }

    private async Task<HttpResponseMessage> SendAuthedOnceAsync(HttpMethod method, string uri, BufferedContent? content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri)
        {
            Content = content?.ToHttpContent(),
        };

        if (!string.IsNullOrWhiteSpace(_session.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.AccessToken);
        }

        var client = _httpClientFactory.CreateClient("Api");
        return await client.SendAsync(request, cancellationToken);
    }

    private static async Task<BufferedContent?> BufferContentAsync(HttpContent? content, CancellationToken cancellationToken)
    {
        if (content is null)
        {
            return null;
        }

        var bytes = await content.ReadAsByteArrayAsync(cancellationToken);
        return new BufferedContent(bytes, content.Headers.ContentType?.ToString());
    }

    private async Task<bool> TryRefreshSessionAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_session.RefreshToken))
        {
            return false;
        }

        var client = _httpClientFactory.CreateClient("Api");
        using var refreshResponse = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshRequestDto { RefreshToken = _session.RefreshToken },
            cancellationToken);

        if (!refreshResponse.IsSuccessStatusCode)
        {
            await ClearSessionAsync();
            return false;
        }

        var payload = await refreshResponse.Content.ReadFromJsonAsync<RefreshResponseDto>(cancellationToken);
        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken) || string.IsNullOrWhiteSpace(payload.RefreshToken))
        {
            await ClearSessionAsync();
            return false;
        }

        _session.SignInFromToken(payload.AccessToken, payload.RefreshToken);
        await _js.InvokeVoidAsync("chopAuth.persistSession", payload.AccessToken, payload.RefreshToken);
        return true;
    }

    private async Task ClearSessionAsync()
    {
        _session.SignOut();
        try
        {
            await _js.InvokeVoidAsync("chopAuth.clearSession");
        }
        catch
        {
            // Ignore JS failures in server-side fallback path.
        }
    }

    private static async Task<IReadOnlyCollection<T>> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var payload = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<T>>(cancellationToken);
        return payload ?? [];
    }

    private sealed record BufferedContent(byte[] Bytes, string? ContentType)
    {
        public HttpContent ToHttpContent()
        {
            var payload = new ByteArrayContent(Bytes);
            if (!string.IsNullOrWhiteSpace(ContentType))
            {
                payload.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(ContentType);
            }

            return payload;
        }
    }
}



