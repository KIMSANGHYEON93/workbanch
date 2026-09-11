using Workbench.Domain.Enums;

namespace Workbench.Domain.Entities;

/// <summary>프로젝트 단위 권한 부여. (ProjectId, UserId) 조합은 유일하다.</summary>
public class ProjectMember : EntityBase
{
    public Guid ProjectId { get; set; }

    public Project? Project { get; set; }

    public Guid UserId { get; set; }

    public AppUser? User { get; set; }

    public ProjectRole Role { get; set; } = ProjectRole.Member;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
