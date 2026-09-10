using Microsoft.EntityFrameworkCore;
using Workbench.Domain.Entities;
using Workbench.Domain.Interfaces;
using Workbench.Infrastructure.Data;

namespace Workbench.Infrastructure.Repositories;

public class ProjectMemberRepository : Repository<ProjectMember>, IProjectMemberRepository
{
    public ProjectMemberRepository(WorkbenchDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<IReadOnlyList<ProjectMember>> ListForProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Include(m => m.User)
            .Where(m => m.ProjectId == projectId)
            .OrderByDescending(m => m.Role)
            .ThenBy(m => m.User!.DisplayName)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProjectMember>> ListForProjectsAsync(
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken = default)
    {
        if (projectIds.Count == 0)
        {
            return [];
        }

        return await Set.AsNoTracking()
            .Where(m => projectIds.Contains(m.ProjectId))
            .ToListAsync(cancellationToken);
    }

    public Task<ProjectMember?> FindAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId, cancellationToken);
}
