using Workbench.Domain.Entities;

namespace Workbench.Domain.Interfaces;

public interface IProjectRepository : IRepository<Project>
{
    Task<IReadOnlyList<Project>> ListOrderedByKeyAsync(CancellationToken cancellationToken = default);

    Task<Project?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <param name="excludingProjectId">수정 중인 프로젝트 자신은 중복에서 제외한다.</param>
    Task<bool> KeyExistsAsync(
        string key,
        Guid? excludingProjectId = null,
        CancellationToken cancellationToken = default);

    /// <summary>키·이름·설명 부분 일치.</summary>
    Task<IReadOnlyList<Project>> SearchAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>프로젝트별 이슈 수. 목록 화면이 N+1 쿼리를 내지 않도록 한 번에 집계한다.</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetIssueCountsAsync(CancellationToken cancellationToken = default);
}
