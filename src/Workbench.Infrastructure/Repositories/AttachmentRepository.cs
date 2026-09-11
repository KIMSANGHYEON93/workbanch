using Microsoft.EntityFrameworkCore;
using Workbench.Domain.Entities;
using Workbench.Domain.Interfaces;
using Workbench.Infrastructure.Data;

namespace Workbench.Infrastructure.Repositories;

public class AttachmentRepository : Repository<Attachment>, IAttachmentRepository
{
    public AttachmentRepository(WorkbenchDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<IReadOnlyList<Attachment>> ListForIssueAsync(
        Guid issueId,
        CancellationToken cancellationToken = default) =>
        await Ordered(a => a.IssueId == issueId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Attachment>> ListForPageAsync(
        Guid pageId,
        CancellationToken cancellationToken = default) =>
        await Ordered(a => a.PageId == pageId).ToListAsync(cancellationToken);

    public Task<Attachment?> GetWithUploaderAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        Set.Include(a => a.UploadedBy).FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    private IQueryable<Attachment> Ordered(
        System.Linq.Expressions.Expression<Func<Attachment, bool>> predicate) =>
        Set.AsNoTracking()
            .Include(a => a.UploadedBy)
            .Where(predicate)
            .OrderBy(a => a.UploadedAt)
            .ThenBy(a => a.Id);
}
