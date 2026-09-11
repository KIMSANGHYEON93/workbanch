using Workbench.Domain.Entities;
using Workbench.Domain.Interfaces;

namespace Workbench.Tests.Fakes;

public sealed class FakeAppUserRepository : IAppUserRepository
{
    private readonly List<AppUser> _users = [];

    public AppUser Seed(string displayName, string email, bool isActive = true)
    {
        var user = new AppUser { DisplayName = displayName, Email = email, IsActive = isActive };
        _users.Add(user);

        return user;
    }

    public Task<IReadOnlyList<AppUser>> ListActiveAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AppUser>>([.. _users.Where(u => u.IsActive).OrderBy(u => u.DisplayName)]);

    public Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.Any(u => u.Id == userId && u.IsActive));

    public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.SingleOrDefault(u => u.Id == id));

    public Task<IReadOnlyList<AppUser>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AppUser>>(_users);

    public Task AddAsync(AppUser entity, CancellationToken cancellationToken = default)
    {
        _users.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(AppUser entity)
    {
    }

    public void Remove(AppUser entity) => _users.Remove(entity);
}
