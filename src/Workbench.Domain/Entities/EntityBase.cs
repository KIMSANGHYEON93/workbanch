namespace Workbench.Domain.Entities;

/// <summary>
/// 모든 영속 엔티티의 식별자만 공유한다. 타임스탬프 이름은 엔티티마다 의미가 달라
/// (Attachment 는 UploadedAt) 여기서 강제하지 않는다.
/// </summary>
public abstract class EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
}
