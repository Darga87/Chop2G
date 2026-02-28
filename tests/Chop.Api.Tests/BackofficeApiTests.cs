using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Collections.Concurrent;
using Chop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Chop.Shared.Contracts.Auth;
using Chop.Shared.Contracts.Backoffice;

namespace Chop.Api.Tests;

public sealed class BackofficeApiTests : IClassFixture<TestWebApplicationFactory>
{
    private static readonly ConcurrentDictionary<string, string> AccessTokens = new(StringComparer.Ordinal);
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public BackofficeApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HrGuards_WithHrRole_Returns200()
    {
        var token = await LoginAndGetAccessTokenAsync("hr", "hr-pass");
        using var request = CreateAuthed(HttpMethod.Get, "/api/hr/guards?status=all&onShiftOnly=false", token);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<GuardItemDto>>();
        Assert.NotNull(payload);
        Assert.NotEmpty(payload!);
    }

    [Fact]
    public async Task HrGuards_WithClientRole_Returns403()
    {
        var token = await LoginAndGetAccessTokenAsync("client", "client-pass");
        using var request = CreateAuthed(HttpMethod.Get, "/api/hr/guards?status=all&onShiftOnly=false", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task HrGuards_CreateGuard_WithHrRole_Works()
    {
        var token = await LoginAndGetAccessTokenAsync("hr", "hr-pass");
        var login = $"guard-{Guid.NewGuid():N}";

        using var createRequest = CreateAuthed(HttpMethod.Post, "/api/hr/guards", token);
        createRequest.Content = JsonContent.Create(new CreateGuardRequestDto
        {
            FullName = "Тестовый Охранник",
            CallSign = $"T-{Guid.NewGuid():N}".Substring(0, 8).ToUpperInvariant(),
            Login = login,
            Password = "GuardPass!123",
            Phone = "+77000000001",
            Email = $"{login}@example.com",
        });

        var createResponse = await _client.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<GuardItemDto>();
        Assert.NotNull(created);
        Assert.Equal("Тестовый Охранник", created!.FullName);
        Assert.StartsWith("T-", created.CallSign, StringComparison.Ordinal);
        Assert.Equal("+77000000001", created.Phone);

        using var listRequest = CreateAuthed(HttpMethod.Get, "/api/hr/guards?search=%D0%A2%D0%B5%D1%81%D1%82%D0%BE%D0%B2%D1%8B%D0%B9&status=all&onShiftOnly=false", token);
        var listResponse = await _client.SendAsync(listRequest);
        listResponse.EnsureSuccessStatusCode();
        var list = await listResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<GuardItemDto>>();
        Assert.NotNull(list);
        Assert.Contains(list!, x => x.FullName == "Тестовый Охранник");
    }

    [Fact]
    public async Task HrGuards_CreateGuard_WithClientRole_Returns403()
    {
        var token = await LoginAndGetAccessTokenAsync("client", "client-pass");

        using var createRequest = CreateAuthed(HttpMethod.Post, "/api/hr/guards", token);
        createRequest.Content = JsonContent.Create(new CreateGuardRequestDto
        {
            FullName = "Forbidden Guard",
            CallSign = $"F-{Guid.NewGuid():N}".Substring(0, 8).ToUpperInvariant(),
            Login = $"guard-{Guid.NewGuid():N}",
            Password = "GuardPass!123",
        });

        var createResponse = await _client.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
    }

    [Fact]
    public async Task HrGuards_UpdateGuard_WithHrRole_Works()
    {
        var token = await LoginAndGetAccessTokenAsync("hr", "hr-pass");
        var login = $"guard-{Guid.NewGuid():N}";

        using var createRequest = CreateAuthed(HttpMethod.Post, "/api/hr/guards", token);
        createRequest.Content = JsonContent.Create(new CreateGuardRequestDto
        {
            FullName = "Охранник До Обновления",
            CallSign = $"U-{Guid.NewGuid():N}".Substring(0, 8).ToUpperInvariant(),
            Login = login,
            Password = "GuardPass!123",
            Phone = "+77000000011",
            Email = $"{login}@example.com",
        });

        var createResponse = await _client.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<GuardItemDto>();
        Assert.NotNull(created);

        using var updateRequest = CreateAuthed(HttpMethod.Put, $"/api/hr/guards/{created!.Id:D}", token);
        updateRequest.Content = JsonContent.Create(new UpdateGuardRequestDto
        {
            FullName = "Охранник После Обновления",
            CallSign = $"Z-{Guid.NewGuid():N}".Substring(0, 8).ToUpperInvariant(),
            Phone = "+77000000012",
            Email = "updated-guard@example.com",
        });

        var updateResponse = await _client.SendAsync(updateRequest);
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<GuardItemDto>();
        Assert.NotNull(updated);
        Assert.Equal("Охранник После Обновления", updated!.FullName);
        Assert.Equal("+77000000012", updated.Phone);
        Assert.Equal("updated-guard@example.com", updated.Email);
    }

    [Fact]
    public async Task AdminClients_WithAdminRole_Returns200()
    {
        var token = await LoginAndGetAccessTokenAsync("admin", "admin-pass");
        using var request = CreateAuthed(HttpMethod.Get, "/api/admin/clients?billing=all&debtOnly=false", token);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task AdminClients_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/admin/clients?billing=all&debtOnly=false");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminClients_CreateClient_WithAdminRole_Works()
    {
        var token = await LoginAndGetAccessTokenAsync("admin", "admin-pass");
        var login = $"client-{Guid.NewGuid():N}";
        var fullName = "Тестовый Клиент";

        using var createRequest = CreateAuthed(HttpMethod.Post, "/api/admin/clients", token);
        createRequest.Content = JsonContent.Create(new CreateAdminClientRequestDto
        {
            Login = login,
            FullName = fullName,
            Phone = "+77010000009",
            Email = $"{login}@example.com",
            HomeAddress = "г. Алматы, тестовая улица, 1",
            HomeLatitude = 43.2389,
            HomeLongitude = 76.8897,
        });

        var createResponse = await _client.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<CreateAdminClientResponseDto>();
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created!.ClientId);
        Assert.False(string.IsNullOrWhiteSpace(created.InvitationToken));
        Assert.True(created.InvitationExpiresAtUtc > DateTime.UtcNow);

        using var listRequest = CreateAuthed(HttpMethod.Get, $"/api/admin/clients?search={Uri.EscapeDataString(fullName)}&billing=all&debtOnly=false", token);
        var listResponse = await _client.SendAsync(listRequest);
        listResponse.EnsureSuccessStatusCode();
        var list = await listResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<AdminClientItemDto>>();
        Assert.NotNull(list);
        Assert.Contains(list!, x => x.DisplayName == fullName);
    }

    [Fact]
    public async Task AdminClients_CreateClient_WithManagerRole_Returns403()
    {
        var token = await LoginAndGetAccessTokenAsync("manager", "manager-pass");
        using var createRequest = CreateAuthed(HttpMethod.Post, "/api/admin/clients", token);
        createRequest.Content = JsonContent.Create(new CreateAdminClientRequestDto
        {
            Login = $"client-{Guid.NewGuid():N}",
            FullName = "Manager Forbidden",
        });

        var createResponse = await _client.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
    }

    [Fact]
    public async Task AuthInvitationAccept_CreatedClient_CanSetPasswordAndLogin()
    {
        var adminToken = await LoginAndGetAccessTokenAsync("admin", "admin-pass");
        var login = $"client-{Guid.NewGuid():N}";

        using var createRequest = CreateAuthed(HttpMethod.Post, "/api/admin/clients", adminToken);
        createRequest.Content = JsonContent.Create(new CreateAdminClientRequestDto
        {
            Login = login,
            FullName = "Клиент по приглашению",
        });

        var createResponse = await _client.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreateAdminClientResponseDto>();
        Assert.NotNull(created);
        Assert.False(string.IsNullOrWhiteSpace(created!.InvitationToken));

        var newPassword = "ClientPass!123";
        var acceptResponse = await _client.PostAsJsonAsync("/api/auth/invitations/accept", new AcceptInvitationRequestDto
        {
            InvitationToken = created.InvitationToken,
            NewPassword = newPassword,
        });
        acceptResponse.EnsureSuccessStatusCode();

        var accepted = await acceptResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(accepted);
        Assert.False(string.IsNullOrWhiteSpace(accepted!.AccessToken));
        Assert.Contains("CLIENT", accepted.User.Roles, StringComparer.OrdinalIgnoreCase);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Login = login,
            Password = newPassword,
        });
        loginResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Manager_CannotAccessOperatorIncidents_Returns403()
    {
        var token = await LoginAndGetAccessTokenAsync("manager", "manager-pass");
        using var request = CreateAuthed(HttpMethod.Get, "/api/operator/incidents", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Manager_CannotImportPayments_Returns403()
    {
        var token = await LoginAndGetAccessTokenAsync("manager", "manager-pass");
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("1CClientBankExchange\nСекцияДокумент=ПлатежноеПоручение", Encoding.UTF8), "file", "bank.txt");

        using var request = CreateAuthed(HttpMethod.Post, "/api/admin/payments/import", token);
        request.Content = content;
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminClients_UpdateClient_WithAdminRole_Works()
    {
        var token = await LoginAndGetAccessTokenAsync("admin", "admin-pass");
        var login = $"client-{Guid.NewGuid():N}";

        using var createRequest = CreateAuthed(HttpMethod.Post, "/api/admin/clients", token);
        createRequest.Content = JsonContent.Create(new CreateAdminClientRequestDto
        {
            Login = login,
            FullName = "Клиент До Обновления",
            Phone = "+77010000001",
            Tariff = "STANDARD",
            BillingStatus = "ACTIVE",
            HasDebt = false,
        });
        var createdResponse = await _client.SendAsync(createRequest);
        createdResponse.EnsureSuccessStatusCode();
        var created = await createdResponse.Content.ReadFromJsonAsync<CreateAdminClientResponseDto>();
        Assert.NotNull(created);

        using var updateRequest = CreateAuthed(HttpMethod.Put, $"/api/admin/clients/{created!.ClientId:D}", token);
        updateRequest.Content = JsonContent.Create(new UpdateAdminClientRequestDto
        {
            FullName = "Клиент После Обновления",
            Phone = "+77010000002",
            Email = "updated@example.com",
            Tariff = "PREMIUM",
            BillingStatus = "DEBT",
            HasDebt = true,
            LastPaymentAtUtc = DateTime.UtcNow.AddDays(-1),
            HomeAddress = "г. Алматы, обновленный адрес, 2",
            HomeLatitude = 43.255,
            HomeLongitude = 76.945,
        });

        var updateResponse = await _client.SendAsync(updateRequest);
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<AdminClientItemDto>();
        Assert.NotNull(updated);
        Assert.Equal("Клиент После Обновления", updated!.DisplayName);
        Assert.Equal("PREMIUM", updated.Tariff);
        Assert.Equal("DEBT", updated.BillingStatus);
        Assert.True(updated.HasDebt);
        Assert.Equal("+77010000002", updated.ContactPhone);
    }

    [Fact]
    public async Task AdminClients_UpdateClient_WithManagerRole_Returns403()
    {
        var adminToken = await LoginAndGetAccessTokenAsync("admin", "admin-pass");
        var managerToken = await LoginAndGetAccessTokenAsync("manager", "manager-pass");
        var login = $"client-{Guid.NewGuid():N}";

        using var createRequest = CreateAuthed(HttpMethod.Post, "/api/admin/clients", adminToken);
        createRequest.Content = JsonContent.Create(new CreateAdminClientRequestDto
        {
            Login = login,
            FullName = "Update Forbidden",
        });
        var createdResponse = await _client.SendAsync(createRequest);
        createdResponse.EnsureSuccessStatusCode();
        var created = await createdResponse.Content.ReadFromJsonAsync<CreateAdminClientResponseDto>();
        Assert.NotNull(created);

        using var updateRequest = CreateAuthed(HttpMethod.Put, $"/api/admin/clients/{created!.ClientId:D}", managerToken);
        updateRequest.Content = JsonContent.Create(new UpdateAdminClientRequestDto
        {
            FullName = "Should Be Forbidden",
            Tariff = "STANDARD",
            BillingStatus = "ACTIVE",
        });

        var updateResponse = await _client.SendAsync(updateRequest);
        Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);
    }

    [Fact]
    public async Task AdminClients_DetailsAndUpdate_WithMultiplePhonesAddresses_Works()
    {
        var token = await LoginAndGetAccessTokenAsync("admin", "admin-pass");
        var login = $"client-{Guid.NewGuid():N}";

        using var createRequest = CreateAuthed(HttpMethod.Post, "/api/admin/clients", token);
        createRequest.Content = JsonContent.Create(new CreateAdminClientRequestDto
        {
            Login = login,
            FullName = "Клиент Мульти",
            Email = $"{login}@example.com",
            Phones =
            [
                new AdminClientPhoneInputDto { Phone = "+77015550101", Type = "PRIMARY", IsPrimary = true },
                new AdminClientPhoneInputDto { Phone = "+77015550102", Type = "MOBILE", IsPrimary = false },
            ],
            Addresses =
            [
                new AdminClientAddressInputDto { Label = "HOME", AddressText = "г. Алматы, Дом 1", Latitude = 43.2389, Longitude = 76.8897, IsPrimary = true },
                new AdminClientAddressInputDto { Label = "WORK", AddressText = "г. Алматы, Офис 2", IsPrimary = false },
            ],
        });

        var createResponse = await _client.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreateAdminClientResponseDto>();
        Assert.NotNull(created);

        using var detailsRequest = CreateAuthed(HttpMethod.Get, $"/api/admin/clients/{created!.ClientId:D}", token);
        var detailsResponse = await _client.SendAsync(detailsRequest);
        detailsResponse.EnsureSuccessStatusCode();
        var details = await detailsResponse.Content.ReadFromJsonAsync<AdminClientDetailsDto>();
        Assert.NotNull(details);
        Assert.Equal(2, details!.Phones.Count);
        Assert.Equal(2, details.Addresses.Count);
        Assert.Contains(details.Phones, x => x.IsPrimary && x.Phone == "+77015550101");
        Assert.Contains(details.Addresses, x => x.IsPrimary && x.Label == "HOME");

        using var updateRequest = CreateAuthed(HttpMethod.Put, $"/api/admin/clients/{created.ClientId:D}", token);
        updateRequest.Content = JsonContent.Create(new UpdateAdminClientRequestDto
        {
            FullName = "Клиент Мульти Обновлен",
            Email = "updated-multi@example.com",
            Tariff = "STANDARD",
            BillingStatus = "ACTIVE",
            HasDebt = false,
            Phones =
            [
                new AdminClientPhoneInputDto { Phone = "+77015550103", Type = "PRIMARY", IsPrimary = true },
            ],
            Addresses =
            [
                new AdminClientAddressInputDto { Label = "HOME", AddressText = "г. Алматы, Новый дом", Latitude = 43.25, Longitude = 76.91, IsPrimary = true },
            ],
        });

        var updateResponse = await _client.SendAsync(updateRequest);
        updateResponse.EnsureSuccessStatusCode();

        using var detailsAfterUpdateRequest = CreateAuthed(HttpMethod.Get, $"/api/admin/clients/{created.ClientId:D}", token);
        var detailsAfterUpdateResponse = await _client.SendAsync(detailsAfterUpdateRequest);
        detailsAfterUpdateResponse.EnsureSuccessStatusCode();
        var detailsAfterUpdate = await detailsAfterUpdateResponse.Content.ReadFromJsonAsync<AdminClientDetailsDto>();
        Assert.NotNull(detailsAfterUpdate);
        Assert.Single(detailsAfterUpdate!.Phones);
        Assert.Single(detailsAfterUpdate.Addresses);
        Assert.Equal("+77015550103", detailsAfterUpdate.Phones.First().Phone);
        Assert.Equal("г. Алматы, Новый дом", detailsAfterUpdate.Addresses.First().AddressText);
    }

    [Fact]
    public async Task SuperAdminAudit_OnlySuperAdmin_CanAccess()
    {
        var superAdminToken = await LoginAndGetAccessTokenAsync("superadmin", "superadmin-pass");
        var adminToken = await LoginAndGetAccessTokenAsync("admin", "admin-pass");

        using var okRequest = CreateAuthed(HttpMethod.Get, "/api/superadmin/audit", superAdminToken);
        var okResponse = await _client.SendAsync(okRequest);
        Assert.Equal(HttpStatusCode.OK, okResponse.StatusCode);

        using var forbiddenRequest = CreateAuthed(HttpMethod.Get, "/api/superadmin/audit", adminToken);
        var forbiddenResponse = await _client.SendAsync(forbiddenRequest);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
    }

    [Fact]
    public async Task Tariffs_Get_WithManagerRole_Returns200()
    {
        var token = await LoginAndGetAccessTokenAsync("manager", "manager-pass");
        using var request = CreateAuthed(HttpMethod.Get, "/api/admin/tariffs?includeInactive=true", token);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<BillingTariffItemDto>>();
        Assert.NotNull(payload);
        Assert.Contains(payload!, x => x.Code == "STANDARD");
    }

    [Fact]
    public async Task Tariffs_Create_WithSuperAdminRole_Works()
    {
        var token = await LoginAndGetAccessTokenAsync("superadmin", "superadmin-pass");
        var code = $"T{Guid.NewGuid():N}".Substring(0, 8).ToUpperInvariant();

        using var request = CreateAuthed(HttpMethod.Post, "/api/superadmin/tariffs", token);
        request.Content = JsonContent.Create(new UpsertBillingTariffRequestDto
        {
            Code = code,
            Name = "Тестовый тариф",
            Description = "Тариф из теста",
            MonthlyFee = 12500m,
            Currency = "KZT",
            IsActive = true,
            SortOrder = 99,
        });

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<BillingTariffItemDto>();
        Assert.NotNull(created);
        Assert.Equal(code, created!.Code);
        Assert.Equal("Тестовый тариф", created.Name);
    }

    [Fact]
    public async Task Tariffs_Create_WithAdminRole_Returns403()
    {
        var token = await LoginAndGetAccessTokenAsync("admin", "admin-pass");
        using var request = CreateAuthed(HttpMethod.Post, "/api/superadmin/tariffs", token);
        request.Content = JsonContent.Create(new UpsertBillingTariffRequestDto
        {
            Code = "TMP",
            Name = "Forbidden",
            MonthlyFee = 0m,
            Currency = "KZT",
            IsActive = true,
            SortOrder = 1,
        });

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Tariffs_Delete_UnusedTariff_Works()
    {
        var token = await LoginAndGetAccessTokenAsync("superadmin", "superadmin-pass");
        var code = $"D{Guid.NewGuid():N}".Substring(0, 8).ToUpperInvariant();

        using (var createRequest = CreateAuthed(HttpMethod.Post, "/api/superadmin/tariffs", token))
        {
            createRequest.Content = JsonContent.Create(new UpsertBillingTariffRequestDto
            {
                Code = code,
                Name = "Удаляемый тариф",
                MonthlyFee = 1000m,
                Currency = "KZT",
                IsActive = true,
                SortOrder = 100,
            });
            var createResponse = await _client.SendAsync(createRequest);
            createResponse.EnsureSuccessStatusCode();
        }

        using var deleteRequest = CreateAuthed(HttpMethod.Delete, $"/api/superadmin/tariffs/{code}", token);
        var deleteResponse = await _client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Tariffs_CannotDeactivateOrDelete_IfAssignedToClient()
    {
        var superAdminToken = await LoginAndGetAccessTokenAsync("superadmin", "superadmin-pass");
        var adminToken = await LoginAndGetAccessTokenAsync("admin", "admin-pass");
        var code = $"U{Guid.NewGuid():N}".Substring(0, 8).ToUpperInvariant();

        using (var createTariffRequest = CreateAuthed(HttpMethod.Post, "/api/superadmin/tariffs", superAdminToken))
        {
            createTariffRequest.Content = JsonContent.Create(new UpsertBillingTariffRequestDto
            {
                Code = code,
                Name = "РСЃРїРѕР»СЊР·СѓРµРјС‹Р№ С‚Р°СЂРёС„",
                MonthlyFee = 2500m,
                Currency = "KZT",
                IsActive = true,
                SortOrder = 101,
            });
            var createTariffResponse = await _client.SendAsync(createTariffRequest);
            createTariffResponse.EnsureSuccessStatusCode();
        }

        using (var createClientRequest = CreateAuthed(HttpMethod.Post, "/api/admin/clients", adminToken))
        {
            createClientRequest.Content = JsonContent.Create(new CreateAdminClientRequestDto
            {
                Login = $"client-{Guid.NewGuid():N}",
                FullName = "Клиент на тарифе",
                Tariff = code,
                BillingStatus = "ACTIVE",
                HasDebt = false,
            });
            var createClientResponse = await _client.SendAsync(createClientRequest);
            createClientResponse.EnsureSuccessStatusCode();
        }

        using (var deactivateRequest = CreateAuthed(HttpMethod.Put, $"/api/superadmin/tariffs/{code}", superAdminToken))
        {
            deactivateRequest.Content = JsonContent.Create(new UpsertBillingTariffRequestDto
            {
                Code = code,
                Name = "РСЃРїРѕР»СЊР·СѓРµРјС‹Р№ С‚Р°СЂРёС„",
                MonthlyFee = 2500m,
                Currency = "KZT",
                IsActive = false,
                SortOrder = 101,
            });
            var deactivateResponse = await _client.SendAsync(deactivateRequest);
            Assert.Equal(HttpStatusCode.BadRequest, deactivateResponse.StatusCode);
        }

        using (var deleteRequest = CreateAuthed(HttpMethod.Delete, $"/api/superadmin/tariffs/{code}", superAdminToken))
        {
            var deleteResponse = await _client.SendAsync(deleteRequest);
            Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);
        }
    }

    [Fact]
    public async Task SuperAdminUsers_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/superadmin/users");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SuperAdminUsers_WithAdminRole_Returns403()
    {
        var adminToken = await LoginAndGetAccessTokenAsync("admin", "admin-pass");
        using var request = CreateAuthed(HttpMethod.Get, "/api/superadmin/users", adminToken);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SuperAdminUsers_CreateAndRead_Works()
    {
        var superAdminToken = await LoginAndGetAccessTokenAsync("superadmin", "superadmin-pass");
        var login = $"hr-{Guid.NewGuid():N}";

        using var createRequest = CreateAuthed(HttpMethod.Post, "/api/superadmin/users", superAdminToken);
        createRequest.Content = JsonContent.Create(new CreateBackofficeUserRequestDto
        {
            Login = login,
            Password = "TempPass!123",
            Email = $"{login}@example.com",
            Phone = "+77000000000",
            Roles = ["HR"],
        });

        var createResponse = await _client.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<BackofficeUserItemDto>();
        Assert.NotNull(created);
        Assert.Equal(login, created!.Login);
        Assert.Contains("HR", created.Roles, StringComparer.OrdinalIgnoreCase);

        using var listRequest = CreateAuthed(HttpMethod.Get, $"/api/superadmin/users?search={login}", superAdminToken);
        var listResponse = await _client.SendAsync(listRequest);
        listResponse.EnsureSuccessStatusCode();
        var users = await listResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<BackofficeUserItemDto>>();
        Assert.NotNull(users);
        Assert.Contains(users!, x => string.Equals(x.Login, login, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SuperAdminUsers_CannotRemoveLastRole_Returns400()
    {
        var superAdminToken = await LoginAndGetAccessTokenAsync("superadmin", "superadmin-pass");
        var login = $"manager-{Guid.NewGuid():N}";

        using var createRequest = CreateAuthed(HttpMethod.Post, "/api/superadmin/users", superAdminToken);
        createRequest.Content = JsonContent.Create(new CreateBackofficeUserRequestDto
        {
            Login = login,
            Password = "TempPass!123",
            Roles = ["MANAGER"],
        });

        var createResponse = await _client.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<BackofficeUserItemDto>();
        Assert.NotNull(created);

        using var removeRequest = CreateAuthed(
            HttpMethod.Post,
            $"/api/superadmin/users/{created!.Id:D}/roles/remove",
            superAdminToken);
        removeRequest.Content = JsonContent.Create(new ChangeBackofficeUserRoleRequestDto
        {
            Role = "MANAGER",
        });

        var removeResponse = await _client.SendAsync(removeRequest);
        Assert.Equal(HttpStatusCode.BadRequest, removeResponse.StatusCode);
    }

    [Fact]
    public async Task SuperAdminUsers_CannotDeactivateSelf_Returns400()
    {
        var superAdminToken = await LoginAndGetAccessTokenAsync("superadmin", "superadmin-pass");

        using var toggleSelfRequest = CreateAuthed(
            HttpMethod.Post,
            $"/api/superadmin/users/{TestUsers.SuperAdmin.Id:D}/toggle-active",
            superAdminToken);

        var toggleSelfResponse = await _client.SendAsync(toggleSelfRequest);
        Assert.Equal(HttpStatusCode.BadRequest, toggleSelfResponse.StatusCode);
    }

    [Fact]
    public async Task PaymentsImport_WithAdminRole_Works()
    {
        var token = await LoginAndGetAccessTokenAsync("admin", "admin-pass");
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("1CClientBankExchange\nСекцияДокумент=ПлатежноеПоручение"), "file", "bank.txt");

        using var request = CreateAuthed(HttpMethod.Post, "/api/admin/payments/import", token);
        request.Content = content;
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<PaymentImportItemDto>();
        Assert.NotNull(payload);
        Assert.True(payload!.TotalRows > 0);
    }

    [Fact]
    public async Task PaymentsImport_WithAccountantRole_Works()
    {
        var token = await LoginAndGetAccessTokenAsync("accountant", "accountant-pass");
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("1CClientBankExchange\nСекцияДокумент=ПлатежноеПоручение", Encoding.UTF8), "file", "bank.txt");

        using var request = CreateAuthed(HttpMethod.Post, "/api/admin/payments/import", token);
        request.Content = content;
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task PaymentsImport_GetImportById_WithAccountantRole_Returns200()
    {
        var token = await LoginAndGetAccessTokenAsync("accountant", "accountant-pass");
        const string payload = """
1CClientBankExchange=1.03
СекцияДокумент=Платежное поручение
Номер=42
Дата=01.02.2026
Сумма=1000
НазначениеПлатежа=Тест
КонецДокумента
""";

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(payload, Encoding.UTF8), "file", "bank-42.txt");
        using var importRequest = CreateAuthed(HttpMethod.Post, "/api/admin/payments/import", token);
        importRequest.Content = content;
        var importResponse = await _client.SendAsync(importRequest);
        importResponse.EnsureSuccessStatusCode();
        var importItem = await importResponse.Content.ReadFromJsonAsync<PaymentImportItemDto>();
        Assert.NotNull(importItem);

        using var detailsRequest = CreateAuthed(HttpMethod.Get, $"/api/admin/payments/imports/{importItem!.Id:D}", token);
        var detailsResponse = await _client.SendAsync(detailsRequest);
        detailsResponse.EnsureSuccessStatusCode();
        var details = await detailsResponse.Content.ReadFromJsonAsync<PaymentImportItemDto>();
        Assert.NotNull(details);
        Assert.Equal(importItem.Id, details!.Id);
        Assert.Equal(importItem.FileName, details.FileName);
    }

    [Fact]
    public async Task PaymentsImport_AutoMatch_ExactCandidate_ReturnsMatchedRow()
    {
        var token = await LoginAndGetAccessTokenAsync("accountant", "accountant-pass");
        var contentValue = $$"""
1CClientBankExchange=1.03
СекцияДокумент=ПлатежноеПоручение
Номер=PM-1
Дата=25.02.2026
Сумма=1000
НазначениеПлатежа=ID:{{TestUsers.Client.UserId}}
КонецДокумента=
""";

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(contentValue, Encoding.UTF8), "file", "bank.txt");
        using var importRequest = CreateAuthed(HttpMethod.Post, "/api/admin/payments/import", token);
        importRequest.Content = content;
        var importResponse = await _client.SendAsync(importRequest);
        importResponse.EnsureSuccessStatusCode();
        var importItem = await importResponse.Content.ReadFromJsonAsync<PaymentImportItemDto>();
        Assert.NotNull(importItem);

        using var rowsRequest = CreateAuthed(HttpMethod.Get, $"/api/admin/payments/imports/{importItem!.Id:D}/rows", token);
        var rowsResponse = await _client.SendAsync(rowsRequest);
        rowsResponse.EnsureSuccessStatusCode();
        var rows = await rowsResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PaymentImportRowItemDto>>();
        var row = Assert.Single(rows!);
        Assert.Equal("MATCHED", row.MatchStatus);
    }

    [Fact]
    public async Task PaymentsImport_AutoMatch_TwoCandidates_ReturnsAmbiguousRow()
    {
        var token = await LoginAndGetAccessTokenAsync("accountant", "accountant-pass");
        var contentValue = $$"""
1CClientBankExchange=1.03
СекцияДокумент=ПлатежноеПоручение
Номер=PM-2
Дата=25.02.2026
Сумма=1000
НазначениеПлатежа=ID:{{TestUsers.Client.UserId}} ID:{{TestUsers.ClientDedupKey.UserId}}
КонецДокумента=
""";

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(contentValue, Encoding.UTF8), "file", "bank2.txt");
        using var importRequest = CreateAuthed(HttpMethod.Post, "/api/admin/payments/import", token);
        importRequest.Content = content;
        var importResponse = await _client.SendAsync(importRequest);
        importResponse.EnsureSuccessStatusCode();
        var importItem = await importResponse.Content.ReadFromJsonAsync<PaymentImportItemDto>();
        Assert.NotNull(importItem);

        using var rowsRequest = CreateAuthed(HttpMethod.Get, $"/api/admin/payments/imports/{importItem!.Id:D}/rows", token);
        var rowsResponse = await _client.SendAsync(rowsRequest);
        rowsResponse.EnsureSuccessStatusCode();
        var rows = await rowsResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PaymentImportRowItemDto>>();
        var row = Assert.Single(rows!);
        Assert.Equal("AMBIGUOUS", row.MatchStatus);
    }

    [Fact]
    public async Task PaymentsImport_Apply_CreatesPaymentAndMarksImportApplied()
    {
        var token = await LoginAndGetAccessTokenAsync("accountant", "accountant-pass");
        var contentValue = $$"""
1CClientBankExchange=1.03
СекцияДокумент=ПлатежноеПоручение
Номер=PM-APPLY-1
Дата=25.02.2026
Сумма=1500
НазначениеПлатежа=ID:{{TestUsers.Client.UserId}}
КонецДокумента=
""";

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(contentValue, Encoding.UTF8), "file", "bank-apply.txt");
        using var importRequest = CreateAuthed(HttpMethod.Post, "/api/admin/payments/import", token);
        importRequest.Content = content;
        var importResponse = await _client.SendAsync(importRequest);
        importResponse.EnsureSuccessStatusCode();
        var importItem = await importResponse.Content.ReadFromJsonAsync<PaymentImportItemDto>();
        Assert.NotNull(importItem);

        using var applyRequest = CreateAuthed(HttpMethod.Post, $"/api/admin/payments/imports/{importItem!.Id:D}/apply", token);
        applyRequest.Content = JsonContent.Create(new { });
        var applyResponse = await _client.SendAsync(applyRequest);
        applyResponse.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var import = await dbContext.BankImports.AsNoTracking().SingleAsync(x => x.Id == importItem.Id);
        Assert.Equal("APPLIED", import.Status);

        var paymentCount = await dbContext.Payments
            .AsNoTracking()
            .CountAsync(x => x.ImportId == importItem.Id && x.ClientUserId == TestUsers.Client.UserId);
        Assert.Equal(1, paymentCount);
    }

    [Fact]
    public async Task PaymentsImport_WithGuardRole_Returns403()
    {
        var token = await LoginAndGetAccessTokenAsync("guard", "guard-pass");
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("1CClientBankExchange\nСекцияДокумент=ПлатежноеПоручение", Encoding.UTF8), "file", "bank.txt");

        using var request = CreateAuthed(HttpMethod.Post, "/api/admin/payments/import", token);
        request.Content = content;
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PaymentsImports_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/admin/payments/imports");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Accountant_CannotReadAdminClients_Returns403()
    {
        var token = await LoginAndGetAccessTokenAsync("accountant", "accountant-pass");
        using var request = CreateAuthed(HttpMethod.Get, "/api/admin/clients?billing=all&debtOnly=false", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Accountant_CannotAccessOperatorIncidents_Returns403()
    {
        var token = await LoginAndGetAccessTokenAsync("accountant", "accountant-pass");
        using var request = CreateAuthed(HttpMethod.Get, "/api/operator/incidents", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Accountant_CannotReadHrGuards_Returns403()
    {
        var token = await LoginAndGetAccessTokenAsync("accountant", "accountant-pass");
        using var request = CreateAuthed(HttpMethod.Get, "/api/hr/guards?status=all&onShiftOnly=false", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SuperAdminUserRoleAdd_WithHrRole_Returns403()
    {
        var superAdminToken = await LoginAndGetAccessTokenAsync("superadmin", "superadmin-pass");
        var hrToken = await LoginAndGetAccessTokenAsync("hr", "hr-pass");
        var login = $"rbac-{Guid.NewGuid():N}";

        using var createRequest = CreateAuthed(HttpMethod.Post, "/api/superadmin/users", superAdminToken);
        createRequest.Content = JsonContent.Create(new CreateBackofficeUserRequestDto
        {
            Login = login,
            Password = "TempPass!123",
            Roles = ["MANAGER"],
        });
        var createResponse = await _client.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<BackofficeUserItemDto>();
        Assert.NotNull(created);

        using var addRequest = CreateAuthed(HttpMethod.Post, $"/api/superadmin/users/{created!.Id:D}/roles/add", hrToken);
        addRequest.Content = JsonContent.Create(new ChangeBackofficeUserRoleRequestDto { Role = "ACCOUNTANT" });
        var response = await _client.SendAsync(addRequest);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SuperAdminUserRoleAdd_UnsupportedRole_Returns400()
    {
        var superAdminToken = await LoginAndGetAccessTokenAsync("superadmin", "superadmin-pass");
        var login = $"rbac-{Guid.NewGuid():N}";

        using var createRequest = CreateAuthed(HttpMethod.Post, "/api/superadmin/users", superAdminToken);
        createRequest.Content = JsonContent.Create(new CreateBackofficeUserRequestDto
        {
            Login = login,
            Password = "TempPass!123",
            Roles = ["MANAGER"],
        });
        var createResponse = await _client.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<BackofficeUserItemDto>();
        Assert.NotNull(created);

        using var addRequest = CreateAuthed(HttpMethod.Post, $"/api/superadmin/users/{created!.Id:D}/roles/add", superAdminToken);
        addRequest.Content = JsonContent.Create(new ChangeBackofficeUserRoleRequestDto { Role = "ROOT" });
        var response = await _client.SendAsync(addRequest);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SuperAdminUsers_RoleOps_WriteAuditTrail()
    {
        var superAdminToken = await LoginAndGetAccessTokenAsync("superadmin", "superadmin-pass");
        var login = $"audit-{Guid.NewGuid():N}";

        using var createRequest = CreateAuthed(HttpMethod.Post, "/api/superadmin/users", superAdminToken);
        createRequest.Content = JsonContent.Create(new CreateBackofficeUserRequestDto
        {
            Login = login,
            Password = "TempPass!123",
            Roles = ["HR"],
        });
        var createResponse = await _client.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<BackofficeUserItemDto>();
        Assert.NotNull(created);

        using (var addRoleRequest = CreateAuthed(HttpMethod.Post, $"/api/superadmin/users/{created!.Id:D}/roles/add", superAdminToken))
        {
            addRoleRequest.Content = JsonContent.Create(new ChangeBackofficeUserRoleRequestDto { Role = "MANAGER" });
            var addRoleResponse = await _client.SendAsync(addRoleRequest);
            addRoleResponse.EnsureSuccessStatusCode();
        }

        using (var removeRoleRequest = CreateAuthed(HttpMethod.Post, $"/api/superadmin/users/{created.Id:D}/roles/remove", superAdminToken))
        {
            removeRoleRequest.Content = JsonContent.Create(new ChangeBackofficeUserRoleRequestDto { Role = "MANAGER" });
            var removeRoleResponse = await _client.SendAsync(removeRoleRequest);
            removeRoleResponse.EnsureSuccessStatusCode();
        }

        using (var toggleRequest = CreateAuthed(HttpMethod.Post, $"/api/superadmin/users/{created.Id:D}/toggle-active", superAdminToken))
        {
            var toggleResponse = await _client.SendAsync(toggleRequest);
            toggleResponse.EnsureSuccessStatusCode();
        }

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var auditRows = await dbContext.AuditLogEntries
            .AsNoTracking()
            .Where(x => x.EntityType == "user" && x.EntityId == created.Id)
            .Select(x => x.Action)
            .ToListAsync();

        Assert.Contains("identity.user.role.add", auditRows);
        Assert.Contains("identity.user.role.remove", auditRows);
        Assert.Contains("identity.user.toggle-active", auditRows);
    }

    [Fact]
    public async Task HrGroups_CreateAddMember_AndList_Works()
    {
        var token = await LoginAndGetAccessTokenAsync("hr", "hr-pass");

        using var createGroupRequest = CreateAuthed(HttpMethod.Post, "/api/hr/groups", token);
        createGroupRequest.Content = JsonContent.Create(new CreateGuardGroupRequestDto
        {
            Name = $"G-{Guid.NewGuid():N}",
        });

        var createGroupResponse = await _client.SendAsync(createGroupRequest);
        createGroupResponse.EnsureSuccessStatusCode();
        var group = await createGroupResponse.Content.ReadFromJsonAsync<GuardGroupItemDto>();
        Assert.NotNull(group);

        using var addMemberRequest = CreateAuthed(HttpMethod.Post, $"/api/hr/groups/{group!.Id:D}/members", token);
        addMemberRequest.Content = JsonContent.Create(new AddGuardToGroupRequestDto
        {
            GuardUserId = TestUsers.Guard.UserId,
            IsCommander = true,
        });

        var addMemberResponse = await _client.SendAsync(addMemberRequest);
        addMemberResponse.EnsureSuccessStatusCode();

        using var listRequest = CreateAuthed(HttpMethod.Get, "/api/hr/groups", token);
        var listResponse = await _client.SendAsync(listRequest);
        listResponse.EnsureSuccessStatusCode();
        var groups = await listResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<GuardGroupItemDto>>();
        Assert.NotNull(groups);
        Assert.Contains(groups!, x => x.Id == group.Id && x.Members.Any(m => m.GuardUserId == TestUsers.Guard.UserId));
    }

    [Fact]
    public async Task HrShifts_StartEnd_ChangesOnShiftInGuards()
    {
        var token = await LoginAndGetAccessTokenAsync("hr", "hr-pass");

        using var createPointRequest = CreateAuthed(HttpMethod.Post, "/api/operator/points", token);
        createPointRequest.Content = JsonContent.Create(new CreateSecurityPointRequestDto
        {
            Code = $"PT-{Guid.NewGuid():N}".Substring(0, 10).ToUpperInvariant(),
            Label = "Тестовая точка",
            Type = "POST",
            Address = "Test address",
            Latitude = 43.2389,
            Longitude = 76.8897,
        });

        var createPointResponse = await _client.SendAsync(createPointRequest);
        createPointResponse.EnsureSuccessStatusCode();
        var point = await createPointResponse.Content.ReadFromJsonAsync<OperatorPointItemDto>();
        Assert.NotNull(point);

        using var startShiftRequest = CreateAuthed(HttpMethod.Post, "/api/hr/shifts/start", token);
        startShiftRequest.Content = JsonContent.Create(new StartGuardShiftRequestDto
        {
            GuardUserId = TestUsers.Guard.UserId,
            SecurityPointId = point!.Id,
        });

        var startShiftResponse = await _client.SendAsync(startShiftRequest);
        startShiftResponse.EnsureSuccessStatusCode();

        using var guardsAfterStartRequest = CreateAuthed(HttpMethod.Get, "/api/hr/guards?status=all&onShiftOnly=true", token);
        var guardsAfterStartResponse = await _client.SendAsync(guardsAfterStartRequest);
        guardsAfterStartResponse.EnsureSuccessStatusCode();
        var onShiftGuards = await guardsAfterStartResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<GuardItemDto>>();
        Assert.NotNull(onShiftGuards);
        Assert.Contains(onShiftGuards!, x => x.Id == TestUsers.Guard.Id);

        using var endShiftRequest = CreateAuthed(HttpMethod.Post, "/api/hr/shifts/end", token);
        endShiftRequest.Content = JsonContent.Create(new EndGuardShiftRequestDto
        {
            GuardUserId = TestUsers.Guard.UserId,
        });

        var endShiftResponse = await _client.SendAsync(endShiftRequest);
        endShiftResponse.EnsureSuccessStatusCode();

        using var activeShiftsRequest = CreateAuthed(HttpMethod.Get, "/api/hr/shifts/active", token);
        var activeShiftsResponse = await _client.SendAsync(activeShiftsRequest);
        activeShiftsResponse.EnsureSuccessStatusCode();
        var activeShifts = await activeShiftsResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<GuardShiftItemDto>>();
        Assert.NotNull(activeShifts);
        Assert.DoesNotContain(activeShifts!, x => x.GuardUserId == TestUsers.Guard.UserId);
    }

    [Fact]
    public async Task OperatorPoints_UpdateToggleAndIncludeInactive_Works()
    {
        var token = await LoginAndGetAccessTokenAsync("operator", "operator-pass");
        var code = $"PT-{Guid.NewGuid():N}".Substring(0, 10).ToUpperInvariant();

        using var createRequest = CreateAuthed(HttpMethod.Post, "/api/operator/points", token);
        createRequest.Content = JsonContent.Create(new CreateSecurityPointRequestDto
        {
            Code = code,
            Label = "Точка до обновления",
            Type = "POST",
            Address = "г. Алматы, Тестовая 1",
            Latitude = 43.2389,
            Longitude = 76.8897,
        });
        var createResponse = await _client.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<OperatorPointItemDto>();
        Assert.NotNull(created);
        Assert.True(created!.IsActive);
        Assert.NotNull(created.Latitude);
        Assert.InRange(Math.Abs(created.Latitude!.Value - 43.2389), 0, 0.0001);
        Assert.NotNull(created.Longitude);
        Assert.InRange(Math.Abs(created.Longitude!.Value - 76.8897), 0, 0.0001);

        using var updateRequest = CreateAuthed(HttpMethod.Put, $"/api/operator/points/{created.Id:D}", token);
        updateRequest.Content = JsonContent.Create(new UpdateSecurityPointRequestDto
        {
            Code = code,
            Label = "Точка после обновления",
            Type = "SITE",
            Address = "г. Алматы, Тестовая 2",
            Latitude = 43.2390,
            Longitude = 76.8898,
        });
        var updateResponse = await _client.SendAsync(updateRequest);
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<OperatorPointItemDto>();
        Assert.NotNull(updated);
        Assert.Equal("Точка после обновления", updated!.Label);
        Assert.Equal("SITE", updated.Type);
        Assert.NotNull(updated.Latitude);
        Assert.InRange(Math.Abs(updated.Latitude!.Value - 43.2390), 0, 0.0001);
        Assert.NotNull(updated.Longitude);
        Assert.InRange(Math.Abs(updated.Longitude!.Value - 76.8898), 0, 0.0001);

        using var toggleRequest = CreateAuthed(HttpMethod.Post, $"/api/operator/points/{created.Id:D}/toggle-active", token);
        toggleRequest.Content = JsonContent.Create(new { });
        var toggleResponse = await _client.SendAsync(toggleRequest);
        toggleResponse.EnsureSuccessStatusCode();

        using var listActiveOnlyRequest = CreateAuthed(HttpMethod.Get, "/api/operator/points?includeInactive=false", token);
        var listActiveOnlyResponse = await _client.SendAsync(listActiveOnlyRequest);
        listActiveOnlyResponse.EnsureSuccessStatusCode();
        var activeOnly = await listActiveOnlyResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<OperatorPointItemDto>>();
        Assert.NotNull(activeOnly);
        Assert.DoesNotContain(activeOnly!, x => x.Id == created.Id);

        using var listWithInactiveRequest = CreateAuthed(HttpMethod.Get, "/api/operator/points?includeInactive=true", token);
        var listWithInactiveResponse = await _client.SendAsync(listWithInactiveRequest);
        listWithInactiveResponse.EnsureSuccessStatusCode();
        var withInactive = await listWithInactiveResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<OperatorPointItemDto>>();
        Assert.NotNull(withInactive);
        var found = withInactive!.Single(x => x.Id == created.Id);
        Assert.False(found.IsActive);
    }

    [Fact]
    public async Task OperatorPoints_Create_WithInvalidCoordinates_Returns400()
    {
        var token = await LoginAndGetAccessTokenAsync("operator", "operator-pass");

        using var createRequest = CreateAuthed(HttpMethod.Post, "/api/operator/points", token);
        createRequest.Content = JsonContent.Create(new CreateSecurityPointRequestDto
        {
            Code = $"PT-{Guid.NewGuid():N}".Substring(0, 10).ToUpperInvariant(),
            Label = "Плохая гео-точка",
            Type = "POST",
            Address = "г. Алматы, Ошибка 1",
            Latitude = 120,
            Longitude = 76.9,
        });

        var createResponse = await _client.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.BadRequest, createResponse.StatusCode);
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

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
            {
                Login = login,
                Password = password,
            });

            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < 5)
            {
                await Task.Delay(150 * attempt);
                continue;
            }

            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
            Assert.NotNull(payload);
            AccessTokens[cacheKey] = payload!.AccessToken;
            return payload.AccessToken;
        }

        throw new InvalidOperationException("Failed to obtain access token due to repeated rate limiting.");
    }
}
