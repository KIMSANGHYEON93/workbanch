using Microsoft.Extensions.Caching.Memory;
using Workbench.Application.Interfaces;

namespace Workbench.Web.Services;

/// <summary>
/// 로그인한 사용자를 로컬 Users 테이블에 반영한다. Blazor 회로가 열리기 전인
/// 최초 문서 요청에서 도는 것이 핵심이다 — 이후 화면들이 담당자 FK 로 이 행을 참조한다.
/// </summary>
public class UserProvisioningMiddleware
{
    /// <summary>같은 사용자를 매 요청마다 DB 에 쓰지 않기 위한 억제 간격.</summary>
    private static readonly TimeSpan ProvisioningInterval = TimeSpan.FromMinutes(10);

    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;

    public UserProvisioningMiddleware(RequestDelegate next, IMemoryCache cache)
    {
        _next = next;
        _cache = cache;
    }

    public async Task InvokeAsync(HttpContext context, IUserProvisioner provisioner)
    {
        var user = WorkbenchClaims.ToCurrentUser(context.User);

        if (user.IsAuthenticated && !_cache.TryGetValue(CacheKey(user.Id), out _)
            && await provisioner.EnsureProvisionedAsync(user, context.RequestAborted))
        {
            // 실패는 캐시하지 않는다 — DB 가 돌아오면 다음 요청에서 바로 다시 시도해야 한다.
            _cache.Set(CacheKey(user.Id), true, ProvisioningInterval);
        }

        await _next(context);
    }

    private static string CacheKey(Guid userId) => $"user-provisioned:{userId}";
}
