using Microsoft.AspNetCore.Components.Authorization;
using Workbench.Application.DTOs;
using Workbench.Application.Interfaces;

namespace Workbench.Web.Services;

/// <summary>
/// 회로(circuit) 수명 동안 유효한 사용자. HttpContext 가 아니라
/// <see cref="AuthenticationStateProvider"/> 를 읽으므로 프리렌더·대화형 양쪽에서 같은 값을 준다.
/// </summary>
public class CurrentUser : ICurrentUser
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public CurrentUser(AuthenticationStateProvider authenticationStateProvider)
    {
        _authenticationStateProvider = authenticationStateProvider;
    }

    public async Task<CurrentUserInfo> GetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var state = await _authenticationStateProvider.GetAuthenticationStateAsync();

        return WorkbenchClaims.ToCurrentUser(state.User);
    }
}
