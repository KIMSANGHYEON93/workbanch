using Workbench.Domain.Entities;

namespace Workbench.Domain.Interfaces;

/// <summary>
/// 엔티티 공통 CRUD. 쓰기는 여기서 추적만 하고 실제 커밋은 <see cref="IUnitOfWork"/> 가 한다
/// — 한 요청에서 여러 엔티티를 바꿀 때 부분 저장이 생기지 않게 하기 위함이다.
/// </summary>
public interface IRepository<TEntity>
    where TEntity : EntityBase
{
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken = default);

    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    void Update(TEntity entity);

    void Remove(TEntity entity);
}
