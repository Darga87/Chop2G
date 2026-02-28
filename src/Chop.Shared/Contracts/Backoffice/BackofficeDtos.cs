namespace Chop.Shared.Contracts.Backoffice;

public sealed class GuardItemDto
{
    public Guid Id { get; set; }
    public string CallSign { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool OnShift { get; set; }
    public string? AssignedPost { get; set; }
    public string? GroupName { get; set; }
    public DateTime? ShiftStartedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class CreateGuardRequestDto
{
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string CallSign { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

public sealed class UpdateGuardRequestDto
{
    public string FullName { get; set; } = string.Empty;
    public string CallSign { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

public sealed class AdminClientItemDto
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string Tariff { get; set; } = "STANDARD";
    public string BillingStatus { get; set; } = "ACTIVE";
    public DateTime LastPaymentAtUtc { get; set; }
    public bool HasDebt { get; set; }
}

public sealed class AdminClientPhoneItemDto
{
    public string Phone { get; set; } = string.Empty;
    public string Type { get; set; } = "PRIMARY";
    public bool IsPrimary { get; set; }
}

public sealed class AdminClientAddressItemDto
{
    public string Label { get; set; } = string.Empty;
    public string AddressText { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool IsPrimary { get; set; }
}

public sealed class AdminClientDetailsDto
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Tariff { get; set; } = "STANDARD";
    public string BillingStatus { get; set; } = "ACTIVE";
    public DateTime LastPaymentAtUtc { get; set; }
    public bool HasDebt { get; set; }
    public IReadOnlyCollection<AdminClientPhoneItemDto> Phones { get; set; } = [];
    public IReadOnlyCollection<AdminClientAddressItemDto> Addresses { get; set; } = [];
}

public sealed class AdminClientPhoneInputDto
{
    public string Phone { get; set; } = string.Empty;
    public string Type { get; set; } = "PRIMARY";
    public bool IsPrimary { get; set; }
}

public sealed class AdminClientAddressInputDto
{
    public string Label { get; set; } = string.Empty;
    public string AddressText { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool IsPrimary { get; set; }
}

public sealed class CreateAdminClientRequestDto
{
    public string Login { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string Tariff { get; set; } = "STANDARD";
    public string BillingStatus { get; set; } = "ACTIVE";
    public bool HasDebt { get; set; }
    public DateTime? LastPaymentAtUtc { get; set; }
    public string? HomeAddress { get; set; }
    public double? HomeLatitude { get; set; }
    public double? HomeLongitude { get; set; }
    public IReadOnlyCollection<AdminClientPhoneInputDto> Phones { get; set; } = [];
    public IReadOnlyCollection<AdminClientAddressInputDto> Addresses { get; set; } = [];
}

public sealed class CreateAdminClientResponseDto
{
    public Guid ClientId { get; set; }
    public string InvitationToken { get; set; } = string.Empty;
    public DateTime InvitationExpiresAtUtc { get; set; }
}

public sealed class UpdateAdminClientRequestDto
{
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string Tariff { get; set; } = "STANDARD";
    public string BillingStatus { get; set; } = "ACTIVE";
    public bool HasDebt { get; set; }
    public DateTime? LastPaymentAtUtc { get; set; }
    public string? HomeAddress { get; set; }
    public double? HomeLatitude { get; set; }
    public double? HomeLongitude { get; set; }
    public IReadOnlyCollection<AdminClientPhoneInputDto> Phones { get; set; } = [];
    public IReadOnlyCollection<AdminClientAddressInputDto> Addresses { get; set; } = [];
}

public sealed class OperatorForceItemDto
{
    public Guid Id { get; set; }
    public string CallSign { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Post { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Radio { get; set; } = string.Empty;
    public string Availability { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public DateTime LastLocationAtUtc { get; set; }
}

public sealed class OperatorPointItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool IsActive { get; set; }
    public string ShiftStatus { get; set; } = string.Empty;
    public int ActiveForces { get; set; }
    public DateTime LastEventAtUtc { get; set; }
}

public sealed class SuperAdminSettingItemDto
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class BillingTariffItemDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MonthlyFee { get; set; }
    public string Currency { get; set; } = "KZT";
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class UpsertBillingTariffRequestDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MonthlyFee { get; set; }
    public string Currency { get; set; } = "KZT";
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed class SuperAdminAuditItemDto
{
    public Guid Id { get; set; }
    public DateTime AtUtc { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
}

public sealed class PaymentImportItemDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int MatchedRows { get; set; }
    public int AmbiguousRows { get; set; }
    public int InvalidRows { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class PaymentImportRowItemDto
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime PaymentDateUtc { get; set; }
    public string MatchStatus { get; set; } = string.Empty;
    public string? ClientDisplayName { get; set; }
}

public sealed class ManualMatchRequestDto
{
    public string? ClientUserId { get; set; }
    public string ClientDisplayName { get; set; } = string.Empty;
}

public sealed class BackofficeUserItemDto
{
    public Guid Id { get; set; }
    public string Login { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public IReadOnlyCollection<string> Roles { get; set; } = [];
}

public sealed class CreateBackofficeUserRequestDto
{
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public IReadOnlyCollection<string> Roles { get; set; } = [];
}

public sealed class ChangeBackofficeUserRoleRequestDto
{
    public string Role { get; set; } = string.Empty;
}

public sealed class GuardGroupItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int MembersCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public IReadOnlyCollection<GuardGroupMemberItemDto> Members { get; set; } = [];
}

public sealed class GuardGroupMemberItemDto
{
    public Guid Id { get; set; }
    public string GuardUserId { get; set; } = string.Empty;
    public string GuardFullName { get; set; } = string.Empty;
    public bool IsCommander { get; set; }
    public DateTime AddedAtUtc { get; set; }
}

public sealed class CreateGuardGroupRequestDto
{
    public string Name { get; set; } = string.Empty;
}

public sealed class AddGuardToGroupRequestDto
{
    public string GuardUserId { get; set; } = string.Empty;
    public bool IsCommander { get; set; }
}

public sealed class GuardShiftItemDto
{
    public Guid Id { get; set; }
    public string GuardUserId { get; set; } = string.Empty;
    public string GuardFullName { get; set; } = string.Empty;
    public Guid? GuardGroupId { get; set; }
    public string? GuardGroupName { get; set; }
    public Guid? SecurityPointId { get; set; }
    public string? SecurityPointLabel { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
}

public sealed class StartGuardShiftRequestDto
{
    public string GuardUserId { get; set; } = string.Empty;
    public Guid? GuardGroupId { get; set; }
    public Guid? SecurityPointId { get; set; }
}

public sealed class EndGuardShiftRequestDto
{
    public string GuardUserId { get; set; } = string.Empty;
}

public sealed class CreateSecurityPointRequestDto
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = "POST";
    public string Address { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public sealed class UpdateSecurityPointRequestDto
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = "POST";
    public string Address { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}
