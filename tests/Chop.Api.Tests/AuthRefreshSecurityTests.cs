using System.Net;
using System.Net.Http.Json;
using Chop.Shared.Contracts.Auth;

namespace Chop.Api.Tests;

public sealed class AuthRefreshSecurityTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthRefreshSecurityTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RefreshToken_ReuseDetected_RevokesTokenChain()
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Login = "operator",
            Password = "operator-pass",
        });
        loginResponse.EnsureSuccessStatusCode();
        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(loginPayload);

        var firstRefresh = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequestDto
        {
            RefreshToken = loginPayload!.RefreshToken,
        });
        firstRefresh.EnsureSuccessStatusCode();
        var firstRefreshPayload = await firstRefresh.Content.ReadFromJsonAsync<RefreshResponseDto>();
        Assert.NotNull(firstRefreshPayload);

        var reuseAttempt = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequestDto
        {
            RefreshToken = loginPayload.RefreshToken,
        });
        Assert.Equal(HttpStatusCode.Unauthorized, reuseAttempt.StatusCode);

        var chainTokenAttempt = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequestDto
        {
            RefreshToken = firstRefreshPayload!.RefreshToken,
        });
        Assert.Equal(HttpStatusCode.Unauthorized, chainTokenAttempt.StatusCode);
    }
}
