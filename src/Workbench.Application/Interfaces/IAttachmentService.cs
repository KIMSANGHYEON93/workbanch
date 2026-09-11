using Workbench.Application.DTOs;

namespace Workbench.Application.Interfaces;

/// <summary>
/// 첨부도 댓글과 같다 — 이슈 <b>또는</b> 페이지 중 정확히 한쪽에 달리며,
/// 그 규칙을 API 형태로 표현 불가능하게 만든다.
/// </summary>
public interface IAttachmentService
{
    Task<IReadOnlyList<AttachmentItem>> ListForIssueAsync(
        Guid issueId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttachmentItem>> ListForPageAsync(
        Guid pageId,
        CancellationToken cancellationToken = default);

    Task<OperationResult> AttachToIssueAsync(
        Guid issueId,
        Stream content,
        string fileName,
        string? contentType,
        long size,
        CancellationToken cancellationToken = default);

    Task<OperationResult> AttachToPageAsync(
        Guid pageId,
        Stream content,
        string fileName,
        string? contentType,
        long size,
        CancellationToken cancellationToken = default);

    Task<AttachmentContent?> OpenAsync(Guid attachmentId, CancellationToken cancellationToken = default);

    Task<OperationResult> DeleteAsync(Guid attachmentId, CancellationToken cancellationToken = default);
}
