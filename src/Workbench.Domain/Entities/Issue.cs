using System.Globalization;
using Workbench.Domain.Enums;

namespace Workbench.Domain.Entities;

public class Issue : EntityBase
{
    public Guid ProjectId { get; set; }

    public Project? Project { get; set; }

    /// <summary>프로젝트 안에서의 연속 번호. 키의 숫자 부분.</summary>
    public int Number { get; set; }

    /// <summary>
    /// "DEV-123". <see cref="Project"/> 조인 없이 목록/검색/링크에 쓰려고 비정규화해 둔다.
    /// 값은 언제나 <see cref="FormatKey"/> 로만 만든다.
    /// </summary>
    public required string Key { get; set; }

    public required string Title { get; set; }

    public string? DescriptionMarkdown { get; set; }

    public IssueType Type { get; set; } = IssueType.Task;

    public IssueStatus Status { get; set; } = IssueStatus.Backlog;

    public IssuePriority Priority { get; set; } = IssuePriority.Medium;

    public Guid? AssigneeId { get; set; }

    public AppUser? Assignee { get; set; }

    public Guid ReporterId { get; set; }

    public AppUser? Reporter { get; set; }

    public DateOnly? DueDate { get; set; }

    /// <summary>배포 대상 환경(DEV/STG/PRD 등). 팀마다 어휘가 달라 자유 문자열로 둔다.</summary>
    public string? Environment { get; set; }

    public string? Version { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Comment> Comments { get; } = new List<Comment>();

    public ICollection<Attachment> Attachments { get; } = new List<Attachment>();

    /// <summary>이슈 키 조립의 단일 출처. 표시·저장이 갈라지지 않게 여기만 쓴다.</summary>
    public static string FormatKey(string projectKey, int number)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectKey);
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{projectKey}{DomainConstants.IssueKeySeparator}{number}");
    }
}
