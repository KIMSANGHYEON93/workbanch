using System.ComponentModel.DataAnnotations;
using Workbench.Domain;

namespace Workbench.Application.DTOs;

public sealed record ProjectListItem(
    Guid Id,
    string Key,
    string Name,
    string? Description,
    int IssueCount,
    DateTimeOffset CreatedAt,
    bool CanAdminister,
    bool IsOpenProject);

public sealed record ProjectDetail(
    Guid Id,
    string Key,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt);

/// <summary>
/// 생성·수정 폼이 함께 쓰는 입력 모델. 길이 제약은 <see cref="DomainConstants.Lengths"/> 를 그대로 쓴다 —
/// 화면 검증과 DB 컬럼 폭이 갈라지면 폼은 통과하는데 저장에서 터진다.
/// </summary>
public class ProjectEditModel
{
    [Required(ErrorMessage = "프로젝트 이름을 입력하세요.")]
    [StringLength(DomainConstants.Lengths.ProjectName, ErrorMessage = "이름은 {1}자를 넘을 수 없습니다.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "프로젝트 키를 입력하세요.")]
    [StringLength(DomainConstants.Lengths.ProjectKey, ErrorMessage = "키는 {1}자를 넘을 수 없습니다.")]
    public string Key { get; set; } = string.Empty;

    [StringLength(DomainConstants.Lengths.Description, ErrorMessage = "설명은 {1}자를 넘을 수 없습니다.")]
    public string? Description { get; set; }
}
