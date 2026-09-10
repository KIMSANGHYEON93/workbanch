using Workbench.Domain.Enums;

namespace Workbench.Application.DTOs;

/// <summary>
/// 한 프로젝트에 대한 현재 사용자의 권한.
/// <paramref name="IsOpenProject"/> 는 구성원이 아무도 없어 모두에게 열려 있다는 뜻이다 —
/// 화면이 그 사실을 말해 줘야 사용자가 "왜 아무나 고칠 수 있지" 를 이해한다.
/// </summary>
public sealed record ProjectPermissions(bool CanWrite, bool CanAdminister, bool IsOpenProject)
{
    /// <summary>프로젝트를 찾을 수 없거나 미인증일 때. 아무것도 할 수 없다(fail-closed).</summary>
    public static readonly ProjectPermissions None = new(false, false, false);
}

public sealed record ProjectMemberItem(Guid UserId, string DisplayName, string Email, ProjectRole Role);
