namespace Workbench.Domain.Interfaces;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 추적 중인 변경을 버린다. 저장이 실패한 뒤 같은 스코프에서 다시 시도하려면
    /// 실패한 변경이 남아 있으면 안 된다 — 이슈 번호 발급 경합 재시도가 이것을 쓴다.
    /// </summary>
    void DiscardChanges();
}
