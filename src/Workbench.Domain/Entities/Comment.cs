namespace Workbench.Domain.Entities;

/// <summary>
/// 이슈 또는 페이지 중 정확히 한쪽에 달린다. 두 FK 를 모두 null 로 두거나 모두 채우는 것은
/// 저장소 제약(CHECK)으로 막는다 — 고아 댓글이 목록에서 조용히 사라지는 것을 방지.
/// </summary>
public class Comment : EntityBase
{
    public Guid? IssueId { get; set; }

    public Issue? Issue { get; set; }

    public Guid? PageId { get; set; }

    public Page? Page { get; set; }

    public required string ContentMarkdown { get; set; }

    public Guid AuthorId { get; set; }

    public AppUser? Author { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
