using Workbench.Domain.Entities;
using Workbench.Domain.Enums;

namespace Workbench.Domain.Interfaces;

/// <summary>목록 화면의 필터. 전부 선택적이며 지정된 것만 좁힌다.</summary>
public sealed record IssueQuery(
    Guid? ProjectId = null,
    IssueStatus? Status = null,
    Guid? AssigneeId = null,
    string? SearchText = null,
    bool IncludeClosed = false);

public interface IIssueRepository : IRepository<Issue>
{
    Task<IReadOnlyList<Issue>> ListAsync(IssueQuery query, CancellationToken cancellationToken = default);

    Task<Issue?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    Task<Issue?> GetWithRelationsAsync(Guid id, CancellationToken cancellationToken = default);
}
