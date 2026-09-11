using Workbench.Domain.Entities;

namespace Workbench.Domain.Interfaces;

public interface IAppUserRepository : IRepository<AppUser>
{
    /// <summary>담당자 후보. 비활성 계정은 제외한다(기존 담당 이력은 그대로 남는다).</summary>
    Task<IReadOnlyList<AppUser>> ListActiveAsync(CancellationToken cancellationToken = default);

    Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken = default);
}
