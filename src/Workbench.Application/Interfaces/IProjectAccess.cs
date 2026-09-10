using Workbench.Application.DTOs;
using Workbench.Domain.Enums;

namespace Workbench.Application.Interfaces;

/// <summary>
/// 프로젝트 단위 권한. <b>화면이 아니라 서비스가 판정한다</b> — 버튼을 감추는 것은 안내이지 통제가 아니다.
/// </summary>
public interface IProjectAccess
{
    Task<ProjectPermissions> GetAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>목록 화면용. 프로젝트마다 따로 묻지 않는다.</summary>
    Task<IReadOnlyDictionary<Guid, ProjectPermissions>> GetManyAsync(
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectMemberItem>> ListMembersAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<OperationResult> SetRoleAsync(
        Guid projectId,
        Guid userId,
        ProjectRole role,
        CancellationToken cancellationToken = default);

    Task<OperationResult> RemoveMemberAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
