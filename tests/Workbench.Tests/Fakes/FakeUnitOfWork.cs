using Workbench.Domain.Interfaces;

namespace Workbench.Tests.Fakes;

public sealed class FakeUnitOfWork : IUnitOfWork
{
    private readonly FakeProjectRepository? _repository;

    public FakeUnitOfWork(FakeProjectRepository? repository = null) => _repository = repository;

    public int SaveChangesCallCount { get; private set; }

    public int DiscardCallCount { get; private set; }

    /// <summary>처음 N 번의 저장을 실패시킨다 — 이슈 번호 발급 경합 재시도를 재현하기 위함.</summary>
    public int FailFirstSaves { get; set; }

    public void DiscardChanges() => DiscardCallCount++;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;

        if (SaveChangesCallCount <= FailFirstSaves)
        {
            throw new InvalidOperationException("저장 경합 (테스트용)");
        }

        _repository?.MarkSaved();
        return Task.FromResult(1);
    }
}
