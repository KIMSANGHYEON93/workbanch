namespace Workbench.Domain.Entities;

/// <summary>
/// Entra ID 사용자의 로컬 투영. <see cref="EntityBase.Id"/> 는 새로 생성하지 않고
/// Entra Object Id(oid 클레임)를 그대로 넣는다 — 토큰만으로 사용자를 식별하기 위함이다.
/// </summary>
public class AppUser : EntityBase
{
    public required string DisplayName { get; set; }

    public required string Email { get; set; }

    /// <summary>퇴사/비활성 계정을 담당자 후보에서 제외하기 위한 플래그. 행은 지우지 않는다(참조 보존).</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastSeenAt { get; set; }
}
