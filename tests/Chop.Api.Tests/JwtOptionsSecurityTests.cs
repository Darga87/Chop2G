using Chop.Api.Auth;

namespace Chop.Api.Tests;

public sealed class JwtOptionsSecurityTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData(JwtOptions.DefaultSigningKey)]
    [InlineData(JwtOptions.DevelopmentSigningKey)]
    [InlineData(JwtOptions.PlaceholderProductionSigningKey)]
    public void IsUnsafeSigningKey_ReturnsTrue_ForWeakOrPlaceholderValues(string? signingKey)
    {
        Assert.True(JwtOptions.IsUnsafeSigningKey(signingKey));
    }

    [Fact]
    public void IsUnsafeSigningKey_ReturnsFalse_ForStrongCustomValue()
    {
        const string strongKey = "this-is-a-very-strong-non-default-signing-key-12345";
        Assert.False(JwtOptions.IsUnsafeSigningKey(strongKey));
    }
}
