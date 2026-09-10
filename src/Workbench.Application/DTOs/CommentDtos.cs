namespace Workbench.Application.DTOs;

public sealed record CommentItem(
    Guid Id,
    string ContentMarkdown,
    Guid AuthorId,
    string? AuthorName,
    DateTimeOffset CreatedAt,
    bool CanDelete);
