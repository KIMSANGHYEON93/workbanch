using Workbench.Application.DTOs;
using Workbench.Application.Interfaces;
using Workbench.Domain.Entities;
using Workbench.Domain.Interfaces;

namespace Workbench.Application.Services;

public class CommentService : ICommentService
{
    private readonly ICommentRepository _comments;
    private readonly IIssueRepository _issues;
    private readonly IPageRepository _pages;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public CommentService(
        ICommentRepository comments,
        IIssueRepository issues,
        IPageRepository pages,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _comments = comments;
        _issues = issues;
        _pages = pages;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<CommentItem>> ListForIssueAsync(
        Guid issueId,
        CancellationToken cancellationToken = default) =>
        await ToItemsAsync(await _comments.ListForIssueAsync(issueId, cancellationToken), cancellationToken);

    public async Task<IReadOnlyList<CommentItem>> ListForPageAsync(
        Guid pageId,
        CancellationToken cancellationToken = default) =>
        await ToItemsAsync(await _comments.ListForPageAsync(pageId, cancellationToken), cancellationToken);

    public async Task<OperationResult> AddToIssueAsync(
        Guid issueId,
        string? contentMarkdown,
        CancellationToken cancellationToken = default)
    {
        if (await _issues.GetByIdAsync(issueId, cancellationToken) is null)
        {
            return OperationResult.Failure("이슈를 찾을 수 없습니다.");
        }

        return await AddAsync(comment => comment.IssueId = issueId, contentMarkdown, cancellationToken);
    }

    public async Task<OperationResult> AddToPageAsync(
        Guid pageId,
        string? contentMarkdown,
        CancellationToken cancellationToken = default)
    {
        if (await _pages.GetByIdAsync(pageId, cancellationToken) is null)
        {
            return OperationResult.Failure("페이지를 찾을 수 없습니다.");
        }

        return await AddAsync(comment => comment.PageId = pageId, contentMarkdown, cancellationToken);
    }

    public async Task<OperationResult> DeleteAsync(
        Guid commentId,
        CancellationToken cancellationToken = default)
    {
        var comment = await _comments.GetByIdAsync(commentId, cancellationToken);
        if (comment is null)
        {
            return OperationResult.Failure("댓글을 찾을 수 없습니다.");
        }

        var user = await _currentUser.GetAsync(cancellationToken);

        // 남의 대화 기록을 지울 수 있으면 이력이 신뢰를 잃는다. 화면에서 버튼을 감추는 것만으로는
        // 부족하다 — 서버가 판정한다.
        if (comment.AuthorId != user.Id)
        {
            return OperationResult.Failure("본인이 작성한 댓글만 삭제할 수 있습니다.");
        }

        _comments.Remove(comment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return OperationResult.Success();
    }

    private async Task<OperationResult> AddAsync(
        Action<Comment> setTarget,
        string? contentMarkdown,
        CancellationToken cancellationToken)
    {
        var content = (contentMarkdown ?? string.Empty).Trim();
        if (content.Length == 0)
        {
            return OperationResult.Failure("댓글 내용을 입력하세요.");
        }

        var user = await _currentUser.GetAsync(cancellationToken);

        var comment = new Comment
        {
            ContentMarkdown = content,
            AuthorId = user.Id,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        setTarget(comment);

        await _comments.AddAsync(comment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return OperationResult.Success();
    }

    private async Task<IReadOnlyList<CommentItem>> ToItemsAsync(
        IReadOnlyList<Comment> comments,
        CancellationToken cancellationToken)
    {
        var user = await _currentUser.GetAsync(cancellationToken);

        return
        [
            .. comments.Select(c => new CommentItem(
                c.Id,
                c.ContentMarkdown,
                c.AuthorId,
                c.Author?.DisplayName,
                c.CreatedAt,
                CanDelete: c.AuthorId == user.Id)),
        ];
    }
}
