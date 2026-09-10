using Workbench.Domain.Entities;

namespace Workbench.Domain.Interfaces;

public interface IAttachmentRepository : IRepository<Attachment>
{
    Task<IReadOnlyList<Attachment>> ListForIssueAsync(
        Guid issueId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Attachment>> ListForPageAsync(
        Guid pageId,
        CancellationToken cancellationToken = default);

    Task<Attachment?> GetWithUploaderAsync(Guid id, CancellationToken cancellationToken = default);
}
