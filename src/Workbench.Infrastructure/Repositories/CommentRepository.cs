using Microsoft.EntityFrameworkCore;
using Workbench.Domain.Entities;
using Workbench.Domain.Interfaces;
using Workbench.Infrastructure.Data;

namespace Workbench.Infrastructure.Repositories;

public class CommentRepository : Repository<Comment>, ICommentRepository
{
    public CommentRepository(WorkbenchDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<IReadOnlyList<Comment>> ListForIssueAsync(
        Guid issueId,
        CancellationToken cancellationToken = default) =>
        await Ordered(c => c.IssueId == issueId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Comment>> ListForPageAsync(
        Guid pageId,
        CancellationToken cancellationToken = default) =>
        await Ordered(c => c.PageId == pageId).ToListAsync(cancellationToken);

    /// <summary>대화는 시간순으로 읽는다. 동시 등록 시 순서가 흔들리지 않게 Id 를 최종 키로 둔다.</summary>
    private IQueryable<Comment> Ordered(
        System.Linq.Expressions.Expression<Func<Comment, bool>> predicate) =>
        Set.AsNoTracking()
            .Include(c => c.Author)
            .Where(predicate)
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id);
}
