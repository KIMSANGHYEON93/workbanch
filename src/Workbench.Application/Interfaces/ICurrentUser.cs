using Workbench.Application.DTOs;

namespace Workbench.Application.Interfaces;

/// <summary>
/// Blazor Server 에서는 최초 문서 요청 이후 HttpContext 가 없으므로 동기 접근자를 둘 수 없다.
/// 구현은 <c>AuthenticationStateProvider</c> 를 통해 회로 수명 동안 유효한 주체를 읽는다.
/// </summary>
public interface ICurrentUser
{
    Task<CurrentUserInfo> GetAsync(CancellationToken cancellationToken = default);
}
