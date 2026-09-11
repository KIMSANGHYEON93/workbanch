using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Workbench.Application.Interfaces;

namespace Workbench.Web.Services;

public static class AuthenticationExtensions
{
    /// <summary>Entra ID 앱 등록 값이 들어오는 설정 구역 (Microsoft.Identity.Web 관례).</summary>
    public const string AzureAdSectionName = "AzureAd";

    public static IServiceCollection AddWorkbenchAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var section = configuration.GetSection(WorkbenchAuthenticationOptions.SectionName);
        services.Configure<WorkbenchAuthenticationOptions>(section);

        var options = section.Get<WorkbenchAuthenticationOptions>() ?? new WorkbenchAuthenticationOptions();

        if (options.Mode == AuthenticationMode.Development && !environment.IsDevelopment())
        {
            // 개발용 우회는 "아무나 관리자로 로그인된 상태"와 같다. 설정 실수로 운영에 켜지면
            // 조용히 인증이 없는 서비스가 되므로, 조용히 넘어가는 대신 기동을 막는다.
            throw new InvalidOperationException(
                $"{WorkbenchAuthenticationOptions.SectionName}:Mode 가 "
                + $"{nameof(AuthenticationMode.Development)} 인데 호스트 환경이 "
                + $"'{environment.EnvironmentName}' 입니다. 개발용 인증 우회는 Development 환경에서만 "
                + $"허용됩니다. 운영에는 {nameof(AuthenticationMode.EntraId)} 를 사용하세요.");
        }

        if (options.Mode == AuthenticationMode.Development)
        {
            services
                .AddAuthentication(DevelopmentAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(
                    DevelopmentAuthenticationHandler.SchemeName,
                    configureOptions: null);
        }
        else
        {
            services
                .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApp(configuration.GetSection(AzureAdSectionName));

            // 로그인/로그아웃 엔드포인트(/MicrosoftIdentity/Account/*) 를 제공한다.
            services.AddControllersWithViews().AddMicrosoftIdentityUI();
        }

        // MVP 권한 모델의 바닥: 로그인하지 않으면 어떤 화면도 열리지 않는다.
        services.AddAuthorization(authorization =>
            authorization.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());

        services.AddCascadingAuthenticationState();
        services.AddMemoryCache();
        services.AddScoped<ICurrentUser, CurrentUser>();

        return services;
    }

    /// <summary>
    /// 인증 미들웨어와 사용자 프로비저닝을 순서대로 붙인다.
    /// 프로비저닝은 <c>UseAuthentication</c> 뒤라야 클레임을 볼 수 있다.
    /// </summary>
    public static WebApplication UseWorkbenchAuthentication(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<UserProvisioningMiddleware>();

        var options = app.Services
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<WorkbenchAuthenticationOptions>>()
            .Value;

        // 컨트롤러는 EntraId 모드에서만 등록되므로, 매핑도 그때만 한다
        // (등록 없이 MapControllers 를 부르면 기동이 실패한다).
        if (options.Mode == AuthenticationMode.EntraId)
        {
            app.MapControllers();
        }

        return app;
    }
}
