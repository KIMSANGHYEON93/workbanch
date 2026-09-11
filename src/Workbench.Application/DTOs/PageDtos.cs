using System.ComponentModel.DataAnnotations;
using Workbench.Domain;

namespace Workbench.Application.DTOs;

/// <summary>트리 한 줄. <paramref name="Depth"/> 는 화면 들여쓰기에 쓴다.</summary>
public sealed record PageTreeItem(
    Guid Id,
    Guid? ParentPageId,
    string Slug,
    string Title,
    string? ProjectKey,
    bool IsPublished,
    int Depth,
    DateTimeOffset UpdatedAt);

public sealed record PageDetail(
    Guid Id,
    string Slug,
    string Title,
    string ContentMarkdown,
    Guid? ParentPageId,
    string? ParentTitle,
    string? ParentSlug,
    Guid? ProjectId,
    string? ProjectKey,
    string? CreatedByName,
    bool IsPublished,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public class PageEditModel
{
    [Required(ErrorMessage = "제목을 입력하세요.")]
    [StringLength(DomainConstants.Lengths.PageTitle, ErrorMessage = "제목은 {1}자를 넘을 수 없습니다.")]
    public string Title { get; set; } = string.Empty;

    public string? ContentMarkdown { get; set; }

    /// <summary>비우면 제목에서 만든다.</summary>
    [StringLength(DomainConstants.Lengths.Slug, ErrorMessage = "슬러그는 {1}자를 넘을 수 없습니다.")]
    public string? Slug { get; set; }

    public Guid? ParentPageId { get; set; }

    public Guid? ProjectId { get; set; }

    public bool IsPublished { get; set; } = true;
}
