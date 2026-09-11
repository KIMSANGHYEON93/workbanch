using Workbench.Application.DTOs;
using Workbench.Application.Interfaces;
using Workbench.Domain;
using Workbench.Domain.Entities;
using Workbench.Domain.Interfaces;

namespace Workbench.Application.Services;

public class PageService : IPageService
{
    private readonly IPageRepository _pages;
    private readonly IProjectRepository _projects;
    private readonly IProjectAccess _access;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public PageService(
        IPageRepository pages,
        IProjectRepository projects,
        IProjectAccess access,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _pages = pages;
        _projects = projects;
        _access = access;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<PageTreeItem>> ListTreeAsync(
        CancellationToken cancellationToken = default) =>
        Flatten(await _pages.ListAllAsync(cancellationToken));

    public async Task<IReadOnlyList<PageTreeItem>> SearchAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var matches = await _pages.SearchAsync(text, cancellationToken);

        // 검색 결과는 트리가 아니라 평평한 목록이다 — 부모가 결과에 없을 수 있으므로
        // 깊이를 0 으로 두어 들여쓰기가 거짓 계층을 암시하지 않게 한다.
        return [.. matches.Select(p => ToTreeItem(p, depth: 0))];
    }

    public async Task<PageDetail?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var page = await _pages.GetBySlugAsync(PageSlug.Normalize(slug), cancellationToken);

        return page is null
            ? null
            : new PageDetail(
                page.Id,
                page.Slug,
                page.Title,
                page.ContentMarkdown,
                page.ParentPageId,
                page.ParentPage?.Title,
                page.ParentPage?.Slug,
                page.ProjectId,
                page.Project?.Key,
                page.CreatedBy?.DisplayName,
                page.IsPublished,
                page.CreatedAt,
                page.UpdatedAt);
    }

    public async Task<IReadOnlyList<PageTreeItem>> ListParentOptionsAsync(
        Guid? excludingPageId = null,
        CancellationToken cancellationToken = default)
    {
        var pages = await _pages.ListAllAsync(cancellationToken);

        if (excludingPageId is not { } pageId)
        {
            return Flatten(pages);
        }

        // 자기 자신과 자손을 부모로 고르면 트리에서 떨어져 나온 고리가 생기고,
        // 그 가지는 어느 화면에서도 다시 보이지 않는다.
        var forbidden = DescendantsAndSelf(pages, pageId);

        return [.. Flatten(pages).Where(p => !forbidden.Contains(p.Id))];
    }

    public async Task<OperationResult<string>> CreateAsync(
        PageEditModel model,
        CancellationToken cancellationToken = default)
    {
        var title = (model.Title ?? string.Empty).Trim();
        var pages = await _pages.ListAllAsync(cancellationToken);

        var errors = await ValidateAsync(model, title, editingPageId: null, pages, cancellationToken);
        if (errors.Count > 0)
        {
            return OperationResult<string>.Failure([.. errors]);
        }

        if (await DenyIfCannotWriteAsync(model.ProjectId, cancellationToken) is { } denied)
        {
            return OperationResult<string>.Failure([.. denied.Errors]);
        }

        var user = await _currentUser.GetAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var newId = Guid.NewGuid();

        var slug = PageSlug.MakeUnique(
            string.IsNullOrWhiteSpace(model.Slug) ? title : model.Slug,
            candidate => pages.Any(p => p.Slug == candidate),
            newId);

        var page = new Page
        {
            Id = newId,
            Title = title,
            ContentMarkdown = model.ContentMarkdown ?? string.Empty,
            Slug = slug,
            ParentPageId = model.ParentPageId,
            ProjectId = model.ProjectId,
            IsPublished = model.IsPublished,
            CreatedById = user.Id,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _pages.AddAsync(page, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return OperationResult<string>.Success(slug);
    }

    public async Task<OperationResult<string>> UpdateAsync(
        Guid id,
        PageEditModel model,
        CancellationToken cancellationToken = default)
    {
        var page = await _pages.GetByIdAsync(id, cancellationToken);
        if (page is null)
        {
            return OperationResult<string>.Failure("페이지를 찾을 수 없습니다.");
        }

        // 옮겨 가는 쪽과 원래 있던 쪽 모두에 권한이 있어야 한다 — 한쪽만 보면 권한 없는
        // 프로젝트로 문서를 밀어 넣거나 빼낼 수 있다.
        if (await DenyIfCannotWriteAsync(page.ProjectId, cancellationToken) is { } fromDenied)
        {
            return OperationResult<string>.Failure([.. fromDenied.Errors]);
        }

        if (await DenyIfCannotWriteAsync(model.ProjectId, cancellationToken) is { } toDenied)
        {
            return OperationResult<string>.Failure([.. toDenied.Errors]);
        }

        var title = (model.Title ?? string.Empty).Trim();
        var pages = await _pages.ListAllAsync(cancellationToken);

        var errors = await ValidateAsync(model, title, id, pages, cancellationToken);
        if (errors.Count > 0)
        {
            return OperationResult<string>.Failure([.. errors]);
        }

        // 슬러그는 사용자가 비우면 기존 값을 유지한다 — 제목을 고쳤다고 링크가 끊기면 안 된다.
        var slug = string.IsNullOrWhiteSpace(model.Slug)
            ? page.Slug
            : PageSlug.MakeUnique(model.Slug, candidate => pages.Any(p => p.Slug == candidate && p.Id != id), id);

        page.Title = title;
        page.ContentMarkdown = model.ContentMarkdown ?? string.Empty;
        page.Slug = slug;
        page.ParentPageId = model.ParentPageId;
        page.ProjectId = model.ProjectId;
        page.IsPublished = model.IsPublished;
        page.UpdatedAt = DateTimeOffset.UtcNow;

        _pages.Update(page);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return OperationResult<string>.Success(slug);
    }

    public async Task<OperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var page = await _pages.GetByIdAsync(id, cancellationToken);
        if (page is null)
        {
            return OperationResult.Failure("페이지를 찾을 수 없습니다.");
        }

        if (await DenyIfCannotWriteAsync(page.ProjectId, cancellationToken) is { } denied)
        {
            return denied;
        }

        // 자기참조 FK 가 NO ACTION 이라 자식이 있으면 DB 가 거절한다. 드라이버 오류를 화면에
        // 흘리는 대신, 무엇을 먼저 해야 하는지 알려준다.
        var childCount = await _pages.CountChildrenAsync(id, cancellationToken);
        if (childCount > 0)
        {
            return OperationResult.Failure(
                $"하위 페이지 {childCount}건이 있어 삭제할 수 없습니다. 하위 페이지를 먼저 옮기거나 지우세요.");
        }

        _pages.Remove(page);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return OperationResult.Success();
    }

    /// <summary>
    /// 프로젝트에 연결되지 않은 페이지는 전사 문서라 누구나 쓸 수 있다.
    /// 연결돼 있으면 그 프로젝트의 쓰기 권한을 따른다.
    /// </summary>
    private async Task<OperationResult?> DenyIfCannotWriteAsync(
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        if (projectId is not { } id)
        {
            return null;
        }

        return (await _access.GetAsync(id, cancellationToken)).CanWrite
            ? null
            : OperationResult.Failure("이 프로젝트의 페이지를 변경할 권한이 없습니다.");
    }

    private async Task<List<string>> ValidateAsync(
        PageEditModel model,
        string title,
        Guid? editingPageId,
        IReadOnlyList<Page> pages,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(title))
        {
            errors.Add("제목을 입력하세요.");
        }
        else if (title.Length > DomainConstants.Lengths.PageTitle)
        {
            errors.Add($"제목은 {DomainConstants.Lengths.PageTitle}자를 넘을 수 없습니다.");
        }

        if (model.ParentPageId is { } parentId)
        {
            if (pages.All(p => p.Id != parentId))
            {
                errors.Add("상위 페이지를 찾을 수 없습니다.");
            }
            else if (editingPageId is { } pageId && DescendantsAndSelf(pages, pageId).Contains(parentId))
            {
                errors.Add(parentId == editingPageId
                    ? "페이지를 자기 자신의 하위로 옮길 수 없습니다."
                    : "페이지를 자기 하위 페이지의 아래로 옮길 수 없습니다.");
            }
        }

        if (model.ProjectId is { } projectId
            && await _projects.GetByIdAsync(projectId, cancellationToken) is null)
        {
            errors.Add("프로젝트를 찾을 수 없습니다.");
        }

        return errors;
    }

    /// <summary>주어진 페이지와 그 아래 모든 자손의 Id.</summary>
    private static HashSet<Guid> DescendantsAndSelf(IReadOnlyList<Page> pages, Guid rootId)
    {
        var byParent = pages
            .Where(p => p.ParentPageId.HasValue)
            .GroupBy(p => p.ParentPageId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Id).ToList());

        var result = new HashSet<Guid> { rootId };
        var pending = new Stack<Guid>([rootId]);

        while (pending.Count > 0)
        {
            if (!byParent.TryGetValue(pending.Pop(), out var children))
            {
                continue;
            }

            foreach (var childId in children.Where(result.Add))
            {
                pending.Push(childId);
            }
        }

        return result;
    }

    /// <summary>
    /// 부모-자식 목록을 깊이 우선으로 펼친다.
    /// ⚠ 데이터가 어떤 이유로든 고리를 이루면 여기서 무한 순회가 된다 — 방문 집합으로 막는다.
    /// </summary>
    private static List<PageTreeItem> Flatten(IReadOnlyList<Page> pages)
    {
        var byParent = pages
            .GroupBy(p => p.ParentPageId)
            .ToDictionary(g => g.Key ?? Guid.Empty, g => g.OrderBy(p => p.Title, StringComparer.Ordinal).ToList());

        var result = new List<PageTreeItem>(pages.Count);
        var visited = new HashSet<Guid>();

        void Walk(Guid parentKey, int depth)
        {
            if (!byParent.TryGetValue(parentKey, out var children))
            {
                return;
            }

            foreach (var child in children.Where(c => visited.Add(c.Id)))
            {
                result.Add(ToTreeItem(child, depth));
                Walk(child.Id, depth + 1);
            }
        }

        Walk(Guid.Empty, 0);

        // 부모가 사라졌거나(권한·삭제) 고리에 갇힌 페이지도 목록에서 잃지 않는다.
        foreach (var orphan in pages.Where(p => !visited.Contains(p.Id)))
        {
            result.Add(ToTreeItem(orphan, depth: 0));
        }

        return result;
    }

    private static PageTreeItem ToTreeItem(Page page, int depth) =>
        new(
            page.Id,
            page.ParentPageId,
            page.Slug,
            page.Title,
            page.Project?.Key,
            page.IsPublished,
            depth,
            page.UpdatedAt);
}
