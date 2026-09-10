using Microsoft.EntityFrameworkCore;
using Workbench.Domain.Entities;
using Workbench.Domain.Interfaces;
using Workbench.Infrastructure.Data;

namespace Workbench.Infrastructure.Repositories;

/// <summary>
/// 엔티티 공통 CRUD 구현. 여기서는 <c>SaveChanges</c> 를 호출하지 않는다 —
/// 커밋 시점은 <see cref="IUnitOfWork"/> 를 가진 호출자가 정한다.
/// </summary>
public class Repository<TEntity> : IRepository<TEntity>
    where TEntity : EntityBase
{
    private readonly WorkbenchDbContext _dbContext;

    public Repository(WorkbenchDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    protected WorkbenchDbContext DbContext => _dbContext;

    protected DbSet<TEntity> Set => _dbContext.Set<TEntity>();

    public Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking().ToListAsync(cancellationToken);

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        await Set.AddAsync(entity, cancellationToken);

    public void Update(TEntity entity) => Set.Update(entity);

    public void Remove(TEntity entity) => Set.Remove(entity);
}
