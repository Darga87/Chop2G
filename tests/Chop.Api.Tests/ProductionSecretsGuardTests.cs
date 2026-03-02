using Chop.Api.Platform;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Chop.Api.Tests;

public sealed class ProductionSecretsGuardTests
{
    [Fact]
    public void EnsureConfigured_DoesNotThrow_InDevelopment()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Database=dev;",
                ["Jwt:SigningKey"] = "dev-only-signing-key-at-least-32chars",
            })
            .Build();

        var env = new FakeHostEnvironment { EnvironmentName = "Development" };
        ProductionSecretsGuard.EnsureConfigured(configuration, env);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("replace-in-production")]
    [InlineData("__DB_CONNECTION__")]
    public void EnsureConfigured_Throws_WhenConnectionStringIsUnsafe(string? value)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = value,
                ["Jwt:SigningKey"] = "prod-signing-key-very-strong-123456789",
            })
            .Build();

        var env = new FakeHostEnvironment { EnvironmentName = "Production" };
        Assert.Throws<InvalidOperationException>(() => ProductionSecretsGuard.EnsureConfigured(configuration, env));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("replace-in-production")]
    [InlineData("__JWT_SIGNING_KEY__")]
    public void EnsureConfigured_Throws_WhenJwtSigningKeyIsUnsafe(string? value)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=10.0.0.12;Database=prod;",
                ["Jwt:SigningKey"] = value,
            })
            .Build();

        var env = new FakeHostEnvironment { EnvironmentName = "Production" };
        Assert.Throws<InvalidOperationException>(() => ProductionSecretsGuard.EnsureConfigured(configuration, env));
    }

    [Fact]
    public void EnsureConfigured_DoesNotThrow_WhenSecretsPresent()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=10.0.0.12;Database=prod;Username=svc;Password=secret;",
                ["Jwt:SigningKey"] = "prod-signing-key-very-strong-123456789",
            })
            .Build();

        var env = new FakeHostEnvironment { EnvironmentName = "Production" };
        ProductionSecretsGuard.EnsureConfigured(configuration, env);
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Chop.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
