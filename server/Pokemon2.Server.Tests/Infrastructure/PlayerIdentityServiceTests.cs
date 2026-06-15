using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Pokemon2.Server.Infrastructure;

namespace Pokemon2.Server.Tests.Infrastructure;

public sealed class PlayerIdentityServiceTests
{
    [Fact]
    public void Issue_CreatesTokenThatCanBeValidated()
    {
        var service = CreateService();

        var identity = service.Issue();

        Assert.StartsWith("user_", identity.UserId);
        Assert.True(service.TryValidate(identity.Token, out var resolvedUserId));
        Assert.Equal(identity.UserId, resolvedUserId);
    }

    [Fact]
    public void TryValidate_RejectsTamperedToken()
    {
        var service = CreateService();
        var identity = service.Issue();

        var tampered = $"{identity.UserId}.tampered";

        Assert.False(service.TryValidate(tampered, out _));
    }

    private static PlayerIdentityService CreateService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PlayerIdentity:Secret"] = "test-secret"
            })
            .Build();

        return new PlayerIdentityService(configuration, new FakeHostEnvironment());
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Pokemon2.Server.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
