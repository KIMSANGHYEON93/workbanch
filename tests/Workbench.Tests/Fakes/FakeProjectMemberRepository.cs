using Workbench.Domain.Entities;
using Workbench.Domain.Enums;
using Workbench.Domain.Interfaces;

namespace Workbench.Tests.Fakes;

public sealed class FakeProjectMemberRepository : IProjectMemberRepository
{
    private readonly List<ProjectMember> _members = [];

    public IReadOnlyList<ProjectMember> Members => _members;

    public ProjectMember Seed(Guid projectId, Guid userId, ProjectRole role)
    {
        var member = new ProjectMember { ProjectId = projectId, UserId = userId, Role = role };
        _members.Add(member);

        return member;
    }

    public Task<IReadOnlyList<ProjectMember>> ListForProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProjectMember>>([.. _members.Where(m => m.ProjectId == projectId)]);

    public Task<IReadOnlyList<ProjectMember>> ListForProjectsAsync(
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProjectMember>>(
            [.. _members.Where(m => projectIds.Contains(m.ProjectId))]);

    public Task<ProjectMember?> FindAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_members.SingleOrDefault(m => m.ProjectId == projectId && m.UserId == userId));

    public Task<ProjectMember?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_members.SingleOrDefault(m => m.Id == id));

    public Task<IReadOnlyList<ProjectMember>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProjectMember>>(_members);

    public Task AddAsync(ProjectMember entity, CancellationToken cancellationToken = default)
    {
        _members.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(ProjectMember entity)
    {
    }

    public void Remove(ProjectMember entity) => _members.Remove(entity);
}
