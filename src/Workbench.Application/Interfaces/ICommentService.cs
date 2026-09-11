using Workbench.Application.DTOs;

namespace Workbench.Application.Interfaces;

/// <summary>
/// 댓글은 이슈 <b>또는</b> 페이지 중 정확히 한쪽에 달린다(DB CHECK 제약).
/// 그 규칙을 검증으로 확인하는 대신 <b>API 형태로 표현 불가능하게</b> 만든다 —
/// 대상 두 개를 받는 메서드가 없으므로 둘 다 채우거나 둘 다 비우는 호출을 쓸 수 없다.
/// </summary>
public interface ICommentService
{
    Task<IReadOnlyList<CommentItem>> ListForIssueAsync(
        Guid issueId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CommentItem>> ListForPageAsync(
        Guid pageId,
        CancellationToken cancellationToken = default);

    Task<OperationResult> AddToIssueAsync(
        Guid issueId,
        string? contentMarkdown,
        CancellationToken cancellationToken = default);

    Task<OperationResult> AddToPageAsync(
        Guid pageId,
        string? contentMarkdown,
        CancellationToken cancellationToken = default);

    Task<OperationResult> DeleteAsync(Guid commentId, CancellationToken cancellationToken = default);
}
