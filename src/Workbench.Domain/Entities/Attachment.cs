namespace Workbench.Domain.Entities;

/// <summary>
/// Blob 에 올라간 파일의 메타데이터. 실제 바이트는 Azure Blob Storage 에 있고
/// <see cref="BlobPath"/> 만 DB 에 남는다. 소유자는 이슈 또는 페이지 중 하나다(Comment 와 동일 규칙).
/// </summary>
public class Attachment : EntityBase
{
    public Guid? IssueId { get; set; }

    public Issue? Issue { get; set; }

    public Guid? PageId { get; set; }

    public Page? Page { get; set; }

    public required string FileName { get; set; }

    public required string ContentType { get; set; }

    /// <summary>바이트 수.</summary>
    public long Size { get; set; }

    /// <summary>컨테이너 내부 상대 경로. SAS/URL 은 저장하지 않는다(만료되므로).</summary>
    public required string BlobPath { get; set; }

    public Guid UploadedById { get; set; }

    public AppUser? UploadedBy { get; set; }

    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}
