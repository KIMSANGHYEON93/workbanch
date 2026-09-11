using Workbench.Application.DTOs;

namespace Workbench.Application.Interfaces;

/// <summary>
/// Entra ID 사용자를 로컬 <c>Users</c> 테이블에 반영한다.
/// 이슈 담당자·작성자가 FK 로 이 테이블을 가리키므로, 로그인한 사람은 조회 전에 행이 있어야 한다.
/// </summary>
public interface IUserProvisioner
{
    /// <summary>
    /// 사용자 행을 만들거나 갱신한다. <b>DB 장애를 호출자에게 던지지 않는다</b> —
    /// 프로비저닝은 화면 렌더의 전제조건이 아니라 부수 효과라서, 실패해도 요청은 계속돼야 한다.
    /// </summary>
    /// <returns>반영에 성공했으면 <c>true</c>. <c>false</c> 면 호출자는 결과를 캐시하지 말고 다음에 다시 시도한다.</returns>
    Task<bool> EnsureProvisionedAsync(CurrentUserInfo user, CancellationToken cancellationToken = default);
}
