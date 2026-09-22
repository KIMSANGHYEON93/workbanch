using Microsoft.EntityFrameworkCore;
using Workbench.Domain.Entities;
using Workbench.Domain.Interfaces;
using Workbench.Infrastructure.Data;

namespace Workbench.Infrastructure.Repositories;

public class AttachmentRepository : Repository<Attachment>, IAttachmentRepository
{
    private readonly IDbContextFactory<WorkbenchDbContext> _contextFactory;

    public AttachmentRepository(WorkbenchDbContext dbContext, IDbContextFactory<WorkbenchDbContext> contextFactory)
        : base(dbContext)
    {
        _contextFactory = contextFactory;
    }

    public Task<IReadOnlyList<Attachment>> ListForIssueAsync(
        Guid issueId,
        CancellationToken cancellationToken = default) =>
        ListAsync(a => a.IssueId == issueId, cancellationToken);

    public Task<IReadOnlyList<Attachment>> ListForPageAsync(
        Guid pageId,
        CancellationToken cancellationToken = default) =>
        ListAsync(a => a.PageId == pageId, cancellationToken);

    public Task<Attachment?> GetWithUploaderAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        Set.Include(a => a.UploadedBy).FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    /// <summary>
    /// 이슈 상세 화면에서 이 목록은 <c>CommentThread</c> 와 형제 컴포넌트로 동시에 로드된다.
    /// 회로가 공유하는 <see cref="Repository{TEntity}.DbContext"/> 를 그대로 쓰면 두 쿼리가
    /// 겹칠 때 EF Core 가 "A second operation was started on this context before a previous
    /// operation completed" 로 죽는다(실기동으로 발견) — 그래서 이 읽기 전용 조회만 짧은 수명의
    /// 컨텍스트를 새로 연다. 쓰기(Add/Remove)는 여전히 공유 컨텍스트로 커밋 시점을
    /// IUnitOfWork 에 맡긴다.
    /// </summary>
    private async Task<IReadOnlyList<Attachment>> ListAsync(
        System.Linq.Expressions.Expression<Func<Attachment, bool>> predicate,
        CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Set<Attachment>()
            .AsNoTracking()
            .Include(a => a.UploadedBy)
            .Where(predicate)
            .OrderBy(a => a.UploadedAt)
            .ThenBy(a => a.Id)
            .ToListAsync(cancellationToken);
    }
}
