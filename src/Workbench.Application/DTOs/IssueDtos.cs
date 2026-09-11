using System.ComponentModel.DataAnnotations;
using Workbench.Domain;
using Workbench.Domain.Enums;

namespace Workbench.Application.DTOs;

public sealed record IssueListItem(
    Guid Id,
    string Key,
    string Title,
    IssueType Type,
    IssueStatus Status,
    IssuePriority Priority,
    string ProjectKey,
    string? AssigneeName,
    DateOnly? DueDate,
    string? Environment,
    string? Version,
    DateTimeOffset UpdatedAt);

public sealed record IssueDetail(
    Guid Id,
    string Key,
    Guid ProjectId,
    string ProjectKey,
    string ProjectName,
    string Title,
    string? DescriptionMarkdown,
    IssueType Type,
    IssueStatus Status,
    IssuePriority Priority,
    Guid? AssigneeId,
    string? AssigneeName,
    string? ReporterName,
    DateOnly? DueDate,
    string? Environment,
    string? Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>담당자 선택 상자에 쓰는 최소 정보.</summary>
public sealed record UserOption(Guid Id, string DisplayName, string Email);

public class IssueEditModel
{
    [Required(ErrorMessage = "프로젝트를 선택하세요.")]
    public Guid ProjectId { get; set; }

    [Required(ErrorMessage = "제목을 입력하세요.")]
    [StringLength(DomainConstants.Lengths.IssueTitle, ErrorMessage = "제목은 {1}자를 넘을 수 없습니다.")]
    public string Title { get; set; } = string.Empty;

    public string? DescriptionMarkdown { get; set; }

    public IssueType Type { get; set; } = IssueType.Task;

    public IssueStatus Status { get; set; } = IssueStatus.Backlog;

    public IssuePriority Priority { get; set; } = IssuePriority.Medium;

    public Guid? AssigneeId { get; set; }

    public DateOnly? DueDate { get; set; }

    [StringLength(DomainConstants.Lengths.EnvironmentName, ErrorMessage = "환경은 {1}자를 넘을 수 없습니다.")]
    public string? Environment { get; set; }

    [StringLength(DomainConstants.Lengths.VersionLabel, ErrorMessage = "버전은 {1}자를 넘을 수 없습니다.")]
    public string? Version { get; set; }
}
