using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Chop.Shared.Contracts.Auth;
using Chop.Shared.Contracts.Clients;

namespace Chop.Api.Tests;

public sealed class ClientsApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public ClientsApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ClientsMe_Get_WithClientRole_Returns200()
    {
        await _factory.SeedClientProfileAsync(
            TestUsers.Client.UserId,
            "Клиент Тест",
            [("+77010000001", "PRIMARY", true)],
            [("HOME", "г. Алматы, ул. Тест 1", 43.2389, 76.8897, true)]);

        var token = await LoginAndGetAccessTokenAsync("client", "client-pass");
        using var request = CreateAuthed(HttpMethod.Get, "/api/clients/me", token);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var profile = await response.Content.ReadFromJsonAsync<ClientProfileDto>();
        Assert.NotNull(profile);
        Assert.Equal("Клиент Тест", profile!.FullName);
        Assert.Single(profile.Phones);
        Assert.Single(profile.Addresses);
    }

    [Fact]
    public async Task ClientsMe_Update_WithClientRole_Works()
    {
        await _factory.SeedClientProfileAsync(
            TestUsers.Client.UserId,
            "Клиент До",
            [("+77010000002", "PRIMARY", true)],
            [("HOME", "г. Алматы, ул. До 2", 43.2389, 76.8897, true)]);

        var token = await LoginAndGetAccessTokenAsync("client", "client-pass");
        using var request = CreateAuthed(HttpMethod.Put, "/api/clients/me", token);
        request.Content = JsonContent.Create(new UpdateClientProfileDto
        {
            FullName = "Клиент После",
            Email = "client-updated@example.com",
            Phones =
            [
                new UpdateClientPhoneDto { Phone = "+77010000003", Type = "PRIMARY", IsPrimary = true },
                new UpdateClientPhoneDto { Phone = "+77010000004", Type = "MOBILE", IsPrimary = false },
            ],
            Addresses =
            [
                new UpdateClientAddressDto
                {
                    Label = "HOME",
                    AddressText = "г. Алматы, ул. После 3",
                    Latitude = 43.25,
                    Longitude = 76.91,
                    IsPrimary = true,
                },
            ],
        });

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var profile = await response.Content.ReadFromJsonAsync<ClientProfileDto>();
        Assert.NotNull(profile);
        Assert.Equal("Клиент После", profile!.FullName);
        Assert.Equal("client-updated@example.com", profile.Email);
        Assert.Equal(2, profile.Phones.Count);
        Assert.Single(profile.Addresses);
        Assert.Contains(profile.Phones, x => x.IsPrimary && x.Phone == "+77010000003");
    }

    [Fact]
    public async Task ClientsMe_Get_WithOperatorRole_Returns403()
    {
        var token = await LoginAndGetAccessTokenAsync("operator", "operator-pass");
        using var request = CreateAuthed(HttpMethod.Get, "/api/clients/me", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static HttpRequestMessage CreateAuthed(HttpMethod method, string uri, string accessToken)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
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
