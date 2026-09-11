namespace Workbench.Domain.Entities;

public class Project : EntityBase
{
    public required string Name { get; set; }

    /// <summary>이슈 키 접두어(DEV, DEPLOY). 대문자 영숫자, 전역 유일.</summary>
    public required string Key { get; set; }

    public string? Description { get; set; }

    public Guid CreatedById { get; set; }

    public AppUser? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 마지막으로 발급한 이슈 번호. 이슈 키(DEV-123)는 프로젝트별 연속 번호라
    /// COUNT(*) 로는 삭제 이력 때문에 재사용 충돌이 난다 — 발급 카운터를 따로 보관한다.
    /// </summary>
    public int LastIssueNumber { get; set; }

    public ICollection<Issue> Issues { get; } = new List<Issue>();

    public ICollection<Page> Pages { get; } = new List<Page>();

    public ICollection<ProjectMember> Members { get; } = new List<ProjectMember>();
}
