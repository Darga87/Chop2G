using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Chop.Shared.Contracts.Auth;
using Chop.Shared.Contracts.Incidents;

namespace Chop.App.Mobile.Services;

public sealed class MobileApiClient
{
    private readonly HttpClient _httpClient;
    private readonly MobileSessionState _session;

    public MobileApiClient(HttpClient httpClient, MobileSessionState session)
    {
        _httpClient = httpClient;
        _session = session;
    }

    public async Task<LoginResponseDto> LoginAsync(string login, string password, CancellationToken cancellationToken)
    {
        var response = await SendAsync(
            HttpMethod.Post,
            "/api/auth/login",
            new LoginRequestDto { Login = login, Password = password },
            authenticated: false,
            cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<LoginResponseDto>(cancellationToken: cancellationToken);
        if (payload is null)
        {
            throw new MobileApiException(HttpStatusCode.InternalServerError, "Пустой ответ от API при логине.");
        }

        return payload;
    }

    public async Task<CreateIncidentResponseDto> CreateIncidentAsync(CreateIncidentDto request, CancellationToken cancellationToken)
    {
        var message = await SendAsync(HttpMethod.Post, "/api/incidents", request, authenticated: true, cancellationToken);
        var payload = await message.Content.ReadFromJsonAsync<CreateIncidentResponseDto>(cancellationToken: cancellationToken);
        if (payload is null)
        {
            throw new MobileApiException(HttpStatusCode.InternalServerError, "Пустой ответ от API при создании тревоги.");
        }

        return payload;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string relativePath,
        object? body,
        bool authenticated,
        CancellationToken cancellationToken)
    {
        var baseUrl = _session.ApiBaseUrl.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new MobileApiException(HttpStatusCode.BadRequest, "Не заполнен API Base URL.");
        }

        using var request = new HttpRequestMessage(method, $"{baseUrl}{relativePath}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (authenticated)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.AccessToken);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new MobileApiException(
                HttpStatusCode.ServiceUnavailable,
                $"Connection failure. Проверь API URL и доступность сервера: {request.RequestUri}. {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            throw new MobileApiException(
                HttpStatusCode.RequestTimeout,
                $"Request timeout. Проверь API URL и сеть: {request.RequestUri}. {ex.Message}");
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = string.IsNullOrWhiteSpace(responseText)
            ? $"{(int)response.StatusCode} {response.StatusCode}"
            : $"{(int)response.StatusCode} {response.StatusCode}. {responseText}";
        throw new MobileApiException(response.StatusCode, message);
    }
}

public sealed class MobileApiException : Exception
{
    public MobileApiException(HttpStatusCode statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
