using Workbench.Domain.Entities;

namespace Workbench.Domain.Interfaces;

public interface ICommentRepository : IRepository<Comment>
{
    Task<IReadOnlyList<Comment>> ListForIssueAsync(
        Guid issueId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Comment>> ListForPageAsync(
        Guid pageId,
        CancellationToken cancellationToken = default);
}
