using System.Security.Claims;
using Workbench.Application.DTOs;

namespace Workbench.Web.Services;

/// <summary>
/// 클레임 → <see cref="CurrentUserInfo"/> 변환의 단일 출처.
/// 미들웨어(HttpContext.User)와 회로(AuthenticationStateProvider)가 같은 규칙을 쓰게 해,
/// 프로비저닝된 사용자와 화면이 보는 사용자가 갈라지지 않도록 한다.
/// </summary>
public static class WorkbenchClaims
{
    /// <summary>Entra ID 의 Object Id. 사용자의 영구 식별자이며 메일 주소와 달리 바뀌지 않는다.</summary>
    public const string ObjectId = "http://schemas.microsoft.com/identity/claims/objectidentifier";

    private static readonly string[] IdClaimTypes = [ObjectId, "oid", ClaimTypes.NameIdentifier];

    private static readonly string[] NameClaimTypes = ["name", ClaimTypes.Name, "preferred_username"];

    private static readonly string[] EmailClaimTypes =
        ["preferred_username", "email", ClaimTypes.Email, "upn"];

    public static CurrentUserInfo ToCurrentUser(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return CurrentUserInfo.Anonymous;
        }

        // Object Id 가 없거나 GUID 가 아니면 사용자를 특정할 수 없다. 추측해서 통과시키는 대신
        // 미인증으로 떨어뜨린다(fail-closed) — 잘못된 Id 로 프로비저닝하면 이력이 엉킨다.
        if (!Guid.TryParse(FirstClaim(principal, IdClaimTypes), out var id) || id == Guid.Empty)
        {
            return CurrentUserInfo.Anonymous;
        }

        var email = FirstClaim(principal, EmailClaimTypes) ?? string.Empty;
        var displayName = FirstClaim(principal, NameClaimTypes) ?? email;

        return new CurrentUserInfo(id, displayName, email, IsAuthenticated: true);
    }

    private static string? FirstClaim(ClaimsPrincipal principal, string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
