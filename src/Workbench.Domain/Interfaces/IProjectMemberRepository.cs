using Workbench.Domain.Entities;

namespace Workbench.Domain.Interfaces;

public interface IProjectMemberRepository : IRepository<ProjectMember>
{
    Task<IReadOnlyList<ProjectMember>> ListForProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    /// <summary>여러 프로젝트의 구성원을 한 번에. 목록 화면이 N+1 쿼리를 내지 않게 한다.</summary>
    Task<IReadOnlyList<ProjectMember>> ListForProjectsAsync(
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken = default);

    Task<ProjectMember?> FindAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
