using Workbench.Application.DTOs;
using Workbench.Application.Interfaces;
using Workbench.Domain.Entities;
using Workbench.Domain.Enums;
using Workbench.Domain.Interfaces;

namespace Workbench.Application.Services;

public class ProjectAccessService : IProjectAccess
{
    private readonly IProjectMemberRepository _members;
    private readonly IProjectRepository _projects;
    private readonly IAppUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public ProjectAccessService(
        IProjectMemberRepository members,
        IProjectRepository projects,
        IAppUserRepository users,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _members = members;
        _projects = projects;
        _users = users;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ProjectPermissions> GetAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var user = await _currentUser.GetAsync(cancellationToken);
        if (!user.IsAuthenticated)
        {
            return ProjectPermissions.None;
        }

        if (await _projects.GetByIdAsync(projectId, cancellationToken) is null)
        {
            return ProjectPermissions.None;
        }

        return Evaluate(await _members.ListForProjectAsync(projectId, cancellationToken), user.Id);
    }

    /// <summary>
    /// 구성원이 아무도 없으면 열린 프로젝트다. 이 규칙이 없으면 권한 기능을 붙이는 순간
    /// 기존 프로젝트가 전부 아무도 손댈 수 없는 상태가 된다.
    /// </summary>
    private static ProjectPermissions Evaluate(IEnumerable<ProjectMember> members, Guid userId)
    {
        var all = members as IReadOnlyList<ProjectMember> ?? [.. members];

        if (all.Count == 0)
        {
            return new ProjectPermissions(CanWrite: true, CanAdminister: true, IsOpenProject: true);
        }

        var role = all.FirstOrDefault(m => m.UserId == userId)?.Role;

        return new ProjectPermissions(
            CanWrite: role is ProjectRole.Member or ProjectRole.Admin,
            CanAdminister: role is ProjectRole.Admin,
            IsOpenProject: false);
    }

    public async Task<IReadOnlyDictionary<Guid, ProjectPermissions>> GetManyAsync(
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken = default)
    {
        var user = await _currentUser.GetAsync(cancellationToken);
        if (!user.IsAuthenticated || projectIds.Count == 0)
        {
            return projectIds.ToDictionary(id => id, _ => ProjectPermissions.None);
        }

        var members = await _members.ListForProjectsAsync(projectIds, cancellationToken);
        var byProject = members.ToLookup(m => m.ProjectId);

        return projectIds.ToDictionary(id => id, id => Evaluate(byProject[id], user.Id));
    }

    public async Task<IReadOnlyList<ProjectMemberItem>> ListMembersAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var members = await _members.ListForProjectAsync(projectId, cancellationToken);

        return
        [
            .. members.Select(m => new ProjectMemberItem(
                m.UserId,
                m.User?.DisplayName ?? "(알 수 없는 사용자)",
                m.User?.Email ?? string.Empty,
                m.Role)),
        ];
    }

    public async Task<OperationResult> SetRoleAsync(
        Guid projectId,
        Guid userId,
        ProjectRole role,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(role))
        {
            return OperationResult.Failure("알 수 없는 역할입니다.");
        }

        var guard = await RequireAdministerAsync(projectId, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }

        var members = await _members.ListForProjectAsync(projectId, cancellationToken);
        var existing = await _members.FindAsync(projectId, userId, cancellationToken);

        // 활성 계정 확인은 <b>새로 추가할 때만</b> 한다. 이미 구성원인 사람의 역할 변경까지 막으면,
        // 계정이 비활성화된 순간 관리자가 그 사람을 내릴 수도 뺄 수도 없게 된다.
        if (existing is null && !await _users.IsActiveAsync(userId, cancellationToken))
        {
            return OperationResult.Failure("구성원으로 추가할 수 없는 계정입니다(존재하지 않거나 비활성).");
        }

        if (existing is not null && WouldRemoveTheLastAdmin(members, userId, newRole: role))
        {
            return LastAdminFailure();
        }

        if (existing is null)
        {
            await _members.AddAsync(
                new ProjectMember { ProjectId = projectId, UserId = userId, Role = role },
                cancellationToken);
        }
        else
        {
            existing.Role = role;
            _members.Update(existing);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return OperationResult.Success();
    }

    public async Task<OperationResult> RemoveMemberAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var guard = await RequireAdministerAsync(projectId, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }

        var existing = await _members.FindAsync(projectId, userId, cancellationToken);
        if (existing is null)
        {
            return OperationResult.Failure("구성원을 찾을 수 없습니다.");
        }

        var members = await _members.ListForProjectAsync(projectId, cancellationToken);
        if (WouldRemoveTheLastAdmin(members, userId, newRole: null))
        {
            return LastAdminFailure();
        }

        _members.Remove(existing);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return OperationResult.Success();
    }

    private async Task<OperationResult?> RequireAdministerAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var permissions = await GetAsync(projectId, cancellationToken);

        return permissions.CanAdminister
            ? null
            : OperationResult.Failure("이 프로젝트의 구성원을 관리할 권한이 없습니다.");
    }

    /// <summary>
    /// 마지막 관리자를 내리거나 빼면 아무도 그 프로젝트를 다시 관리할 수 없다 —
    /// 구성원이 남아 있는 한 열린 프로젝트로도 돌아가지 않으므로 영구 잠김이다.
    /// </summary>
    private static bool WouldRemoveTheLastAdmin(
        IReadOnlyList<ProjectMember> members,
        Guid userId,
        ProjectRole? newRole)
    {
        var admins = members.Where(m => m.Role == ProjectRole.Admin).ToList();

        if (admins.Count != 1 || admins[0].UserId != userId)
        {
            return false;
        }

        return newRole != ProjectRole.Admin;
    }

    private static OperationResult LastAdminFailure() =>
        OperationResult.Failure(
            "마지막 관리자는 내리거나 뺄 수 없습니다. 다른 구성원을 먼저 관리자로 지정하세요.");
}
