using Workbench.Domain.Entities;
using Workbench.Domain.Interfaces;

namespace Workbench.Tests.Fakes;

public sealed class FakePageRepository : IPageRepository
{
    private readonly List<Page> _pages = [];

    public IReadOnlyList<Page> Pages => _pages;

    public Page Seed(string title, string slug, Guid? parentId = null, Guid? projectId = null)
    {
        var page = new Page
        {
            Title = title,
            Slug = slug,
            ParentPageId = parentId,
            ProjectId = projectId,
            CreatedById = FakeCurrentUser.DefaultUserId,
        };

        _pages.Add(page);
        return page;
    }

    public Task<IReadOnlyList<Page>> ListAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Page>>([.. _pages]);

    public Task<Page?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
        Task.FromResult(_pages.SingleOrDefault(p => p.Slug == slug));

    public Task<bool> SlugExistsAsync(
        string slug,
        Guid? excludingPageId = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_pages.Any(p => p.Slug == slug && p.Id != excludingPageId));

    public Task<int> CountChildrenAsync(Guid pageId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_pages.Count(p => p.ParentPageId == pageId));

    public Task<IReadOnlyList<Page>> SearchAsync(string text, CancellationToken cancellationToken = default)
    {
        var search = (text ?? string.Empty).Trim();

        return Task.FromResult<IReadOnlyList<Page>>(search.Length == 0
            ? []
            : [.. _pages.Where(p => p.Title.Contains(search) || p.ContentMarkdown.Contains(search))]);
    }

    public Task<Page?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_pages.SingleOrDefault(p => p.Id == id));

    public Task<IReadOnlyList<Page>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Page>>(_pages);

    public Task AddAsync(Page entity, CancellationToken cancellationToken = default)
    {
        _pages.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(Page entity)
    {
    }

    public void Remove(Page entity) => _pages.Remove(entity);
}
