using Workbench.Application.DTOs;

namespace Workbench.Application.Interfaces;

public interface IPageService
{
    /// <summary>루트부터 깊이 우선으로 펼친 트리.</summary>
    Task<IReadOnlyList<PageTreeItem>> ListTreeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PageTreeItem>> SearchAsync(
        string text,
        CancellationToken cancellationToken = default);

    Task<PageDetail?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>부모 선택 상자용. 자기 자신과 자기 자손은 제외한다(순환 방지).</summary>
    Task<IReadOnlyList<PageTreeItem>> ListParentOptionsAsync(
        Guid? excludingPageId = null,
        CancellationToken cancellationToken = default);

    /// <returns>성공 시 확정된 슬러그.</returns>
    Task<OperationResult<string>> CreateAsync(
        PageEditModel model,
        CancellationToken cancellationToken = default);

    Task<OperationResult<string>> UpdateAsync(
        Guid id,
        PageEditModel model,
        CancellationToken cancellationToken = default);

    Task<OperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
