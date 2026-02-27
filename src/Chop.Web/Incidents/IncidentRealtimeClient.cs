using Chop.Shared.Contracts.Realtime;
using Chop.Web.Auth;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using System.Net;

namespace Chop.Web.Incidents;

public sealed class IncidentRealtimeClient : IAsyncDisposable
{
    private readonly IConfiguration _configuration;
    private readonly WebAuthSession _session;
    private readonly NavigationManager _navigationManager;
    private HubConnection? _connection;

    public event Action<IncidentCreatedEvent>? IncidentCreated;

    public event Action<IncidentStatusChangedEvent>? IncidentStatusChanged;

    public event Action<DispatchCreatedEvent>? DispatchCreated;

    public event Action<DispatchAcceptedEvent>? DispatchAccepted;

    public event Action<GuardLocationUpdatedEvent>? GuardLocationUpdated;

    public IncidentRealtimeClient(IConfiguration configuration, WebAuthSession session, NavigationManager navigationManager)
    {
        _configuration = configuration;
        _session = session;
        _navigationManager = navigationManager;
    }

    public async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (!_session.IsAuthenticated || !_session.IsInAnyRole(RoleConstants.OpsRoles))
        {
            return;
        }

        if (_connection is { State: HubConnectionState.Connected or HubConnectionState.Connecting or HubConnectionState.Reconnecting })
        {
            return;
        }

        var hubUri = ResolveHubUri();
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(_session.AccessToken)!;
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<IncidentCreatedEvent>("IncidentCreated", payload => IncidentCreated?.Invoke(payload));
        _connection.On<IncidentStatusChangedEvent>("IncidentStatusChanged", payload => IncidentStatusChanged?.Invoke(payload));
        _connection.On<DispatchCreatedEvent>("DispatchCreated", payload => DispatchCreated?.Invoke(payload));
        _connection.On<DispatchAcceptedEvent>("DispatchAccepted", payload => DispatchAccepted?.Invoke(payload));
        _connection.On<GuardLocationUpdatedEvent>("GuardLocationUpdated", payload => GuardLocationUpdated?.Invoke(payload));

        _connection.Closed += HandleConnectionClosedAsync;

        try
        {
            await _connection.StartAsync(cancellationToken);
        }
        catch (Exception ex) when (HandleAuthFailure(ex))
        {
            await SafeDisposeConnectionAsync();
        }
    }

    private Uri ResolveHubUri()
    {
        var configuredBaseUrl = _configuration["Api:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            return new Uri(new Uri(configuredBaseUrl), "/hubs/incidents");
        }

        return new Uri(_navigationManager.ToAbsoluteUri("/"), "hubs/incidents");
    }

    public async ValueTask DisposeAsync()
    {
        await SafeDisposeConnectionAsync();
    }

    private Task HandleConnectionClosedAsync(Exception? ex)
    {
        if (ex is null)
        {
            return Task.CompletedTask;
        }

        HandleAuthFailure(ex);
        return Task.CompletedTask;
    }

    private bool HandleAuthFailure(Exception ex)
    {
        if (AuthErrorRedirector.TryRedirect(ex, _session, _navigationManager))
        {
            return true;
        }

        if (ex is not HttpRequestException http)
        {
            return false;
        }

        var message = http.Message ?? string.Empty;
        if (message.Contains("401", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
        {
            var mapped = new HttpRequestException(message, http, HttpStatusCode.Unauthorized);
            return AuthErrorRedirector.TryRedirect(mapped, _session, _navigationManager);
        }

        if (message.Contains("403", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Forbidden", StringComparison.OrdinalIgnoreCase))
        {
            var mapped = new HttpRequestException(message, http, HttpStatusCode.Forbidden);
            return AuthErrorRedirector.TryRedirect(mapped, _session, _navigationManager);
        }

        return false;
    }

    private async Task SafeDisposeConnectionAsync()
    {
        if (_connection is null)
        {
            return;
        }

        try
        {
            _connection.Closed -= HandleConnectionClosedAsync;
            await _connection.DisposeAsync();
        }
        finally
        {
            _connection = null;
        }
    }
}
