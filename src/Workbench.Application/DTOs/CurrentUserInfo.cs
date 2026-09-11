namespace Workbench.Application.DTOs;

/// <summary>
/// 현재 요청/회로의 사용자. 미인증이면 <see cref="Anonymous"/> 를 쓴다 —
/// null 을 돌려주면 호출자마다 다른 방식으로 처리하다 빠뜨린다.
/// </summary>
public sealed record CurrentUserInfo(Guid Id, string DisplayName, string Email, bool IsAuthenticated)
{
    public static readonly CurrentUserInfo Anonymous =
        new(Guid.Empty, string.Empty, string.Empty, IsAuthenticated: false);
}
