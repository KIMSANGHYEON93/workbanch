using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Workbench.Application.DTOs;
using Workbench.Application.Interfaces;
using Workbench.Domain;
using Workbench.Domain.Entities;
using Workbench.Domain.Interfaces;

namespace Workbench.Application.Services;

public class AttachmentService : IAttachmentService
{
    /// <summary>브라우저가 형식을 알리지 않았을 때. 렌더되지 않는 안전한 기본값이다.</summary>
    private const string DefaultContentType = "application/octet-stream";

    private readonly IAttachmentRepository _attachments;
    private readonly IIssueRepository _issues;
    private readonly IPageRepository _pages;
    private readonly IFileStorage _storage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly AttachmentOptions _options;
    private readonly ILogger<AttachmentService> _logger;

    public AttachmentService(
        IAttachmentRepository attachments,
        IIssueRepository issues,
        IPageRepository pages,
        IFileStorage storage,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IOptions<AttachmentOptions> options,
        ILogger<AttachmentService> logger)
    {
        _attachments = attachments;
        _issues = issues;
        _pages = pages;
        _storage = storage;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AttachmentItem>> ListForIssueAsync(
        Guid issueId,
        CancellationToken cancellationToken = default) =>
        await ToItemsAsync(await _attachments.ListForIssueAsync(issueId, cancellationToken), cancellationToken);

    public async Task<IReadOnlyList<AttachmentItem>> ListForPageAsync(
        Guid pageId,
        CancellationToken cancellationToken = default) =>
        await ToItemsAsync(await _attachments.ListForPageAsync(pageId, cancellationToken), cancellationToken);

    public async Task<OperationResult> AttachToIssueAsync(
        Guid issueId,
        Stream content,
        string fileName,
        string? contentType,
        long size,
        CancellationToken cancellationToken = default)
    {
        if (await _issues.GetByIdAsync(issueId, cancellationToken) is null)
        {
            return OperationResult.Failure("이슈를 찾을 수 없습니다.");
        }

        return await AttachAsync(
            attachment => attachment.IssueId = issueId,
            content,
            fileName,
            contentType,
            size,
            cancellationToken);
    }

    public async Task<OperationResult> AttachToPageAsync(
        Guid pageId,
        Stream content,
        string fileName,
        string? contentType,
        long size,
        CancellationToken cancellationToken = default)
    {
        if (await _pages.GetByIdAsync(pageId, cancellationToken) is null)
        {
            return OperationResult.Failure("페이지를 찾을 수 없습니다.");
        }

        return await AttachAsync(
            attachment => attachment.PageId = pageId,
            content,
            fileName,
            contentType,
            size,
            cancellationToken);
    }

    public async Task<AttachmentContent?> OpenAsync(
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        var attachment = await _attachments.GetByIdAsync(attachmentId, cancellationToken);
        if (attachment is null)
        {
            return null;
        }

        var content = await _storage.OpenReadAsync(attachment.BlobPath, cancellationToken);

        return content is null
            ? null
            : new AttachmentContent(content, attachment.FileName, attachment.ContentType);
    }

    public async Task<OperationResult> DeleteAsync(
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        var attachment = await _attachments.GetByIdAsync(attachmentId, cancellationToken);
        if (attachment is null)
        {
            return OperationResult.Failure("첨부 파일을 찾을 수 없습니다.");
        }

        var user = await _currentUser.GetAsync(cancellationToken);
        if (attachment.UploadedById != user.Id)
        {
            return OperationResult.Failure("본인이 올린 파일만 삭제할 수 있습니다.");
        }

        // DB 행을 먼저 지운다. 순서를 뒤집으면 바이트는 사라졌는데 목록에는 남는 상태가 되고,
        // 사용자는 열리지 않는 링크를 본다. 반대 순서의 잔여물(고아 blob)은 조용하고 무해하다.
        _attachments.Remove(attachment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await _storage.DeleteAsync(attachment.BlobPath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "첨부 바이트 삭제 실패 (BlobPath={BlobPath})", attachment.BlobPath);
        }

        return OperationResult.Success();
    }

    private async Task<OperationResult> AttachAsync(
        Action<Attachment> setTarget,
        Stream content,
        string fileName,
        string? contentType,
        long size,
        CancellationToken cancellationToken)
    {
        var name = (fileName ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return OperationResult.Failure("파일 이름이 없습니다.");
        }

        if (size <= 0)
        {
            return OperationResult.Failure("빈 파일은 첨부할 수 없습니다.");
        }

        if (size > _options.MaxFileSizeBytes)
        {
            return OperationResult.Failure(
                $"파일이 너무 큽니다. 최대 {_options.MaxFileSizeBytes / (1024 * 1024)}MB 까지 첨부할 수 있습니다.");
        }

        var user = await _currentUser.GetAsync(cancellationToken);
        var resolvedContentType = string.IsNullOrWhiteSpace(contentType)
            ? DefaultContentType
            : contentType.Trim();

        // 저장소가 안전한 경로를 만들어 돌려준다 — 사용자 파일명은 경로에 들어가지 않는다.
        var blobPath = await _storage.SaveAsync(content, name, resolvedContentType, cancellationToken);

        var attachment = new Attachment
        {
            FileName = Truncate(name, DomainConstants.Lengths.FileName),
            ContentType = Truncate(resolvedContentType, DomainConstants.Lengths.ContentType),
            Size = size,
            BlobPath = blobPath,
            UploadedById = user.Id,
            UploadedAt = DateTimeOffset.UtcNow,
        };

        setTarget(attachment);

        await _attachments.AddAsync(attachment, cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception)
        {
            // 메타데이터를 남기지 못했으면 바이트도 남기지 않는다 — 아무도 모르는 파일이 쌓인다.
            await _storage.DeleteAsync(blobPath, CancellationToken.None);
            throw;
        }

        return OperationResult.Success();
    }

    private async Task<IReadOnlyList<AttachmentItem>> ToItemsAsync(
        IReadOnlyList<Attachment> attachments,
        CancellationToken cancellationToken)
    {
        var user = await _currentUser.GetAsync(cancellationToken);

        return
        [
            .. attachments.Select(a => new AttachmentItem(
                a.Id,
                a.FileName,
                a.ContentType,
                a.Size,
                a.UploadedBy?.DisplayName,
                a.UploadedAt,
                CanDelete: a.UploadedById == user.Id)),
        ];
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
