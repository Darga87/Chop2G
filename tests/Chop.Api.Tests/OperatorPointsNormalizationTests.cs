using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Chop.Shared.Contracts.Auth;
using Chop.Shared.Contracts.Backoffice;

namespace Chop.Api.Tests;

public sealed class OperatorPointsNormalizationTests : IClassFixture<TestWebApplicationFactory>
{
    private static readonly ConcurrentDictionary<string, string> AccessTokens = new(StringComparer.Ordinal);
    private readonly HttpClient _client;

    public OperatorPointsNormalizationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OperatorPoints_CreateAndUpdate_NormalizesAddress()
    {
        var token = await LoginAndGetAccessTokenAsync("operator", "operator-pass");
        var code = $"PT-{Guid.NewGuid():N}".Substring(0, 10).ToUpperInvariant();

        using var createRequest = CreateAuthed(HttpMethod.Post, "/api/operator/points", token);
        createRequest.Content = JsonContent.Create(new CreateSecurityPointRequestDto
        {
            Code = code,
            Label = "Normalization test",
            Type = "POST",
            Address = "  Almaty   ,   Test   street   1  ",
            Latitude = 43.2389,
            Longitude = 76.8897,
        });

        var createResponse = await _client.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<OperatorPointItemDto>();
        Assert.NotNull(created);
        Assert.Equal("Almaty, Test street 1", created!.Address);
        Assert.NotNull(created.Latitude);
        Assert.NotNull(created.Longitude);
        Assert.InRange(Math.Abs(created.Latitude!.Value - 43.2389), 0, 0.0001);
        Assert.InRange(Math.Abs(created.Longitude!.Value - 76.8897), 0, 0.0001);

        using var updateRequest = CreateAuthed(HttpMethod.Put, $"/api/operator/points/{created.Id:D}", token);
        updateRequest.Content = JsonContent.Create(new UpdateSecurityPointRequestDto
        {
            Code = code,
            Label = "Normalization updated",
            Type = "SITE",
            Address = "  Almaty,   Test    street   2   ",
            Latitude = 43.2390,
            Longitude = 76.8898,
        });

        var updateResponse = await _client.SendAsync(updateRequest);
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<OperatorPointItemDto>();
        Assert.NotNull(updated);
        Assert.Equal("Almaty, Test street 2", updated!.Address);
        Assert.NotNull(updated.Latitude);
        Assert.NotNull(updated.Longitude);
        Assert.InRange(Math.Abs(updated.Latitude!.Value - 43.2390), 0, 0.0001);
        Assert.InRange(Math.Abs(updated.Longitude!.Value - 76.8898), 0, 0.0001);
    }

    private static HttpRequestMessage CreateAuthed(HttpMethod method, string uri, string accessToken)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private async Task<string> LoginAndGetAccessTokenAsync(string login, string password)
    {
        var cacheKey = $"{login}:{password}";
        if (AccessTokens.TryGetValue(cacheKey, out var cachedToken))
        {
            return cachedToken;
        }

        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Login = login,
            Password = password,
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(payload);
        AccessTokens[cacheKey] = payload!.AccessToken;
        return payload.AccessToken;
    }
}
