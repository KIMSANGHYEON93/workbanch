using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Workbench.Web.Services;

/// <summary>
/// Entra ID 앱 등록 없이 개발할 수 있게 고정 사용자로 항상 인증을 성립시킨다.
/// <b>운영에서는 등록되지 않는다</b> — <see cref="AuthenticationServiceCollectionExtensions"/> 가
/// Development 환경이 아닌 곳에서 이 모드를 만나면 기동을 막는다.
/// </summary>
public class DevelopmentAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "WorkbenchDevelopment";

    private readonly DevelopmentUserOptions _developmentUser;

    public DevelopmentAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<WorkbenchAuthenticationOptions> workbenchOptions)
        : base(options, logger, encoder)
    {
        _developmentUser = workbenchOptions.Value.DevelopmentUser;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(WorkbenchClaims.ObjectId, _developmentUser.Id.ToString()),
            new Claim("name", _developmentUser.DisplayName),
            new Claim("preferred_username", _developmentUser.Email),
        };

        var identity = new ClaimsIdentity(claims, SchemeName, "name", ClaimTypes.Role);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
