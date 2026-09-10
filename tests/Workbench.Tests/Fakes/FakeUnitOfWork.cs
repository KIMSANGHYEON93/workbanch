using Workbench.Domain.Interfaces;

namespace Workbench.Tests.Fakes;

public sealed class FakeUnitOfWork : IUnitOfWork
{
    private readonly FakeProjectRepository? _repository;

    public FakeUnitOfWork(FakeProjectRepository? repository = null) => _repository = repository;

    public int SaveChangesCallCount { get; private set; }

    public int DiscardCallCount { get; private set; }

    public void DiscardChanges() => DiscardCallCount++;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        _repository?.MarkSaved();
        return Task.FromResult(1);
    }
}
