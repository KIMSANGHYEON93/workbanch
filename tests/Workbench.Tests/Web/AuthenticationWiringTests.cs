using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Workbench.Web.Services;

namespace Workbench.Tests.Web;

public class AuthenticationWiringTests
{
    [Fact]
    public void DevelopmentBypass_IsRefusedOutsideDevelopment()
    {
        var services = new ServiceCollection();
        var configuration = Configuration(("Authentication:Mode", "Development"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddWorkbenchAuthentication(configuration, Environment(Environments.Production)));

        // 메시지가 원인과 조치를 함께 말해야 한다 — 기동 실패는 사람이 읽고 고쳐야 하는 순간이다.
        Assert.Contains(WorkbenchAuthenticationOptions.SectionName, exception.Message);
        Assert.Contains(Environments.Production, exception.Message);
        Assert.Contains(nameof(AuthenticationMode.EntraId), exception.Message);
    }

    [Fact]
    public void DevelopmentBypass_IsAllowedInDevelopment()
    {
        var services = new ServiceCollection();
        var configuration = Configuration(("Authentication:Mode", "Development"));

        services.AddWorkbenchAuthentication(configuration, Environment(Environments.Development));

        var options = services.BuildServiceProvider()
            .GetRequiredService<IOptions<WorkbenchAuthenticationOptions>>().Value;

        Assert.Equal(AuthenticationMode.Development, options.Mode);
    }

    [Fact]
    public void MissingAuthenticationSection_DefaultsToEntraId()
    {
        // 설정 누락이 인증 우회로 해석되면 안 된다. 기본값은 안전한 쪽이어야 한다.
        var services = new ServiceCollection();

        services.AddWorkbenchAuthentication(Configuration(), Environment(Environments.Production));

        var options = services.BuildServiceProvider()
            .GetRequiredService<IOptions<WorkbenchAuthenticationOptions>>().Value;

        Assert.Equal(AuthenticationMode.EntraId, options.Mode);
    }

    [Fact]
    public async Task DevelopmentHandler_EmitsClaimsTheMapperUnderstands()
    {
        // 핸들러가 심는 클레임과 매퍼가 읽는 클레임이 갈라지면 "로그인은 됐는데 사용자가 없다" 가 된다.
        var developmentUser = new DevelopmentUserOptions
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            DisplayName = "테스트 개발자",
            Email = "tester@localhost",
        };

        var principal = await AuthenticateWithDevelopmentHandlerAsync(developmentUser);
        var mapped = WorkbenchClaims.ToCurrentUser(principal);

        Assert.True(mapped.IsAuthenticated);
        Assert.Equal(developmentUser.Id, mapped.Id);
        Assert.Equal(developmentUser.DisplayName, mapped.DisplayName);
        Assert.Equal(developmentUser.Email, mapped.Email);
    }

    private static async Task<ClaimsPrincipal> AuthenticateWithDevelopmentHandlerAsync(
        DevelopmentUserOptions developmentUser)
    {
        var handler = new DevelopmentAuthenticationHandler(
            new OptionsMonitorStub(),
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            Options.Create(new WorkbenchAuthenticationOptions { DevelopmentUser = developmentUser }));

        var scheme = new AuthenticationScheme(
            DevelopmentAuthenticationHandler.SchemeName,
            displayName: null,
            typeof(DevelopmentAuthenticationHandler));

        await handler.InitializeAsync(scheme, new DefaultHttpContext());

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        return result.Principal!;
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    private static IHostEnvironment Environment(string environmentName) =>
        new HostEnvironmentStub { EnvironmentName = environmentName };

    private sealed class HostEnvironmentStub : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = nameof(AuthenticationWiringTests);

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }

    private sealed class OptionsMonitorStub : IOptionsMonitor<AuthenticationSchemeOptions>
    {
        public AuthenticationSchemeOptions CurrentValue { get; } = new();

        public AuthenticationSchemeOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<AuthenticationSchemeOptions, string?> listener) => null;
    }
}
