using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Pokemon2.Server.Infrastructure;

namespace Pokemon2.Server.Tests.Infrastructure;

public sealed class AdminAccessServiceTests
{
    [Fact]
    public void IsAuthorized_AllowsLoopbackInDevelopmentWithoutToken()
    {
        var service = CreateService(new Dictionary<string, string?>(), Environments.Development);
        var context = CreateContext("127.0.0.1");

        Assert.True(service.IsAuthorized(context));
    }

    [Fact]
    public void IsAuthorized_AcceptsMatchingToken()
    {
        var service = CreateService(new Dictionary<string, string?>
        {
            ["POKEMON2_ADMIN_TOKEN"] = "secret-token"
        }, Environments.Production);
        var context = CreateContext("10.0.0.5");
        context.Request.Headers[AdminAccessService.HeaderName] = "secret-token";

        Assert.True(service.IsAuthorized(context));
    }

    [Fact]
    public void IsAuthorized_AcceptsAllowlistedAddress()
    {
        var service = CreateService(new Dictionary<string, string?>
        {
            ["POKEMON2_ADMIN_ALLOWLIST"] = "10.0.0.5,10.0.0.6"
        }, Environments.Production);
        var context = CreateContext("10.0.0.6");

        Assert.True(service.IsAuthorized(context));
    }

    [Fact]
    public void IsAuthorized_RejectsUnknownRequest()
    {
        var service = CreateService(new Dictionary<string, string?>
        {
            ["POKEMON2_ADMIN_TOKEN"] = "secret-token"
        }, Environments.Production);
        var context = CreateContext("10.0.0.5");

        Assert.False(service.IsAuthorized(context));
    }

    private static AdminAccessService CreateService(
        Dictionary<string, string?> settings,
        string environmentName)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        return new AdminAccessService(configuration, new FakeHostEnvironment(environmentName));
    }

    private static DefaultHttpContext CreateContext(string remoteIp)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(remoteIp);
        return context;
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "Pokemon2.Server.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
