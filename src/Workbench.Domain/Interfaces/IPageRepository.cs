using Workbench.Domain.Entities;

namespace Workbench.Domain.Interfaces;

public interface IPageRepository : IRepository<Page>
{
    /// <summary>트리를 그리기 위해 전부 읽는다 — 지식 문서는 수천 건 규모가 되지 않는다.</summary>
    Task<IReadOnlyList<Page>> ListAllAsync(CancellationToken cancellationToken = default);

    Task<Page?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<bool> SlugExistsAsync(
        string slug,
        Guid? excludingPageId = null,
        CancellationToken cancellationToken = default);

    Task<int> CountChildrenAsync(Guid pageId, CancellationToken cancellationToken = default);

    /// <summary>제목·본문 부분 일치. 12단계 검색이 이 위에 얹힌다.</summary>
    Task<IReadOnlyList<Page>> SearchAsync(string text, CancellationToken cancellationToken = default);
}
