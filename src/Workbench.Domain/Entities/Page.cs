namespace Workbench.Domain.Entities;

public class Page : EntityBase
{
    /// <summary>계층 구조. null 이면 최상위 페이지.</summary>
    public Guid? ParentPageId { get; set; }

    public Page? ParentPage { get; set; }

    public ICollection<Page> Children { get; } = new List<Page>();

    /// <summary>프로젝트에 소속되지 않은 전사 문서도 허용하므로 nullable.</summary>
    public Guid? ProjectId { get; set; }

    public Project? Project { get; set; }

    public required string Title { get; set; }

    public string ContentMarkdown { get; set; } = string.Empty;

    /// <summary>URL 조각. 제목이 바뀌어도 링크가 살아 있도록 별도 보관한다.</summary>
    public required string Slug { get; set; }

    public Guid CreatedById { get; set; }

    public AppUser? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsPublished { get; set; }

    public ICollection<Comment> Comments { get; } = new List<Comment>();

    public ICollection<Attachment> Attachments { get; } = new List<Attachment>();
}
