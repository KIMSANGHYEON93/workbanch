using Microsoft.EntityFrameworkCore;
using Workbench.Domain.Entities;
using Workbench.Domain.Interfaces;
using Workbench.Infrastructure.Data;

namespace Workbench.Infrastructure.Repositories;

public class AppUserRepository : Repository<AppUser>, IAppUserRepository
{
    public AppUserRepository(WorkbenchDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<IReadOnlyList<AppUser>> ListActiveAsync(CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.DisplayName)
            .ToListAsync(cancellationToken);

    public Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsActive, cancellationToken);
}
