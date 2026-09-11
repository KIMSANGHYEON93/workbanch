using Microsoft.EntityFrameworkCore;
using Workbench.Domain.Entities;
using Workbench.Domain.Interfaces;
using Workbench.Infrastructure.Data;

namespace Workbench.Infrastructure.Repositories;

public class PageRepository : Repository<Page>, IPageRepository
{
    public PageRepository(WorkbenchDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<IReadOnlyList<Page>> ListAllAsync(CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Include(p => p.Project)
            .OrderBy(p => p.Title)
            .ToListAsync(cancellationToken);

    public Task<Page?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking()
            .Include(p => p.Project)
            .Include(p => p.CreatedBy)
            .Include(p => p.ParentPage)
            .FirstOrDefaultAsync(p => p.Slug == slug, cancellationToken);

    public Task<bool> SlugExistsAsync(
        string slug,
        Guid? excludingPageId = null,
        CancellationToken cancellationToken = default) =>
        Set.AsNoTracking()
            .AnyAsync(
                p => p.Slug == slug && (excludingPageId == null || p.Id != excludingPageId),
                cancellationToken);

    public Task<int> CountChildrenAsync(Guid pageId, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking().CountAsync(p => p.ParentPageId == pageId, cancellationToken);

    public async Task<IReadOnlyList<Page>> SearchAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var search = (text ?? string.Empty).Trim();
        if (search.Length == 0)
        {
            return [];
        }

        return await Set.AsNoTracking()
            .Include(p => p.Project)
            .Where(p => p.Title.Contains(search) || p.ContentMarkdown.Contains(search))
            .OrderBy(p => p.Title)
            .ToListAsync(cancellationToken);
    }
}
