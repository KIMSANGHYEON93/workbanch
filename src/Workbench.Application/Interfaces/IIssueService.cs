using Workbench.Application.DTOs;
using Workbench.Domain.Enums;
using Workbench.Domain.Interfaces;

namespace Workbench.Application.Interfaces;

public interface IIssueService
{
    Task<IReadOnlyList<IssueListItem>> ListAsync(
        IssueQuery query,
        CancellationToken cancellationToken = default);

    Task<IssueDetail?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserOption>> ListAssigneeOptionsAsync(CancellationToken cancellationToken = default);

    /// <returns>성공 시 발급된 이슈 키(예: DEV-12).</returns>
    Task<OperationResult<string>> CreateAsync(
        IssueEditModel model,
        CancellationToken cancellationToken = default);

    Task<OperationResult> UpdateAsync(
        Guid id,
        IssueEditModel model,
        CancellationToken cancellationToken = default);

    Task<OperationResult> ChangeStatusAsync(
        Guid id,
        IssueStatus status,
        CancellationToken cancellationToken = default);

    Task<OperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
