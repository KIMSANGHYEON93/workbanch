using Workbench.Domain.Entities;
using Workbench.Domain.Interfaces;

namespace Workbench.Tests.Fakes;

public sealed class FakeAttachmentRepository : IAttachmentRepository
{
    private readonly List<Attachment> _attachments = [];

    public IReadOnlyList<Attachment> Attachments => _attachments;

    public Attachment Seed(Attachment attachment)
    {
        _attachments.Add(attachment);
        return attachment;
    }

    public Task<IReadOnlyList<Attachment>> ListForIssueAsync(
        Guid issueId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Attachment>>(
            [.. _attachments.Where(a => a.IssueId == issueId).OrderBy(a => a.UploadedAt)]);

    public Task<IReadOnlyList<Attachment>> ListForPageAsync(
        Guid pageId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Attachment>>(
            [.. _attachments.Where(a => a.PageId == pageId).OrderBy(a => a.UploadedAt)]);

    public Task<Attachment?> GetWithUploaderAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetByIdAsync(id, cancellationToken);

    public Task<Attachment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_attachments.SingleOrDefault(a => a.Id == id));

    public Task<IReadOnlyList<Attachment>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Attachment>>(_attachments);

    public Task AddAsync(Attachment entity, CancellationToken cancellationToken = default)
    {
        _attachments.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(Attachment entity)
    {
    }

    public void Remove(Attachment entity) => _attachments.Remove(entity);
}
