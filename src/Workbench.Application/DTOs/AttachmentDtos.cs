namespace Workbench.Application.DTOs;

public sealed record AttachmentItem(
    Guid Id,
    string FileName,
    string ContentType,
    long Size,
    string? UploadedByName,
    DateTimeOffset UploadedAt,
    bool CanDelete);

/// <summary>다운로드에 필요한 것만. 스트림은 호출자가 소비 후 닫는다.</summary>
public sealed record AttachmentContent(Stream Content, string FileName, string ContentType);
