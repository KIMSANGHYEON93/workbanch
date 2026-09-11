using Microsoft.EntityFrameworkCore;
using Workbench.Domain.Entities;
using Workbench.Domain.Interfaces;
using Workbench.Infrastructure.Data;

namespace Workbench.Infrastructure.Repositories;

public class ProjectRepository : Repository<Project>, IProjectRepository
{
    public ProjectRepository(WorkbenchDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<IReadOnlyList<Project>> ListOrderedByKeyAsync(
        CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .OrderBy(p => p.Key)
            .ToListAsync(cancellationToken);

    public Task<Project?> GetByKeyAsync(string key, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(p => p.Key == key, cancellationToken);

    public Task<bool> KeyExistsAsync(
        string key,
        Guid? excludingProjectId = null,
        CancellationToken cancellationToken = default) =>
        Set.AsNoTracking()
            .AnyAsync(
                p => p.Key == key && (excludingProjectId == null || p.Id != excludingProjectId),
                cancellationToken);

    public async Task<IReadOnlyList<Project>> SearchAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var search = (text ?? string.Empty).Trim();
        if (search.Length == 0)
        {
            return [];
        }

        return await Set.AsNoTracking()
            .Where(p => p.Key.Contains(search)
                || p.Name.Contains(search)
                || (p.Description != null && p.Description.Contains(search)))
            .OrderBy(p => p.Key)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetIssueCountsAsync(
        CancellationToken cancellationToken = default)
    {
        var counts = await DbContext.Issues
            .AsNoTracking()
            .GroupBy(i => i.ProjectId)
            .Select(g => new { ProjectId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(c => c.ProjectId, c => c.Count);
    }
}
