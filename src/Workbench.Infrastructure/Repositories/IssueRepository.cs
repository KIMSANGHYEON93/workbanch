using Microsoft.EntityFrameworkCore;
using Workbench.Domain.Entities;
using Workbench.Domain.Enums;
using Workbench.Domain.Interfaces;
using Workbench.Infrastructure.Data;

namespace Workbench.Infrastructure.Repositories;

public class IssueRepository : Repository<Issue>, IIssueRepository
{
    /// <summary>목록에서 기본적으로 감추는 상태. "닫힌 이슈"의 정의를 한곳에 둔다.</summary>
    private static readonly IssueStatus[] ClosedStatuses = [IssueStatus.Done, IssueStatus.Cancelled];

    public IssueRepository(WorkbenchDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<IReadOnlyList<Issue>> ListAsync(
        IssueQuery query,
        CancellationToken cancellationToken = default)
    {
        var issues = Set.AsNoTracking()
            .Include(i => i.Project)
            .Include(i => i.Assignee)
            .AsQueryable();

        if (query.ProjectId is { } projectId)
        {
            issues = issues.Where(i => i.ProjectId == projectId);
        }

        if (query.Status is { } status)
        {
            issues = issues.Where(i => i.Status == status);
        }
        else if (!query.IncludeClosed)
        {
            // 상태를 콕 집어 고른 경우에는 그 상태만 본다 — 닫힘 필터가 그것을 덮으면 안 된다.
            issues = issues.Where(i => !ClosedStatuses.Contains(i.Status));
        }

        if (query.AssigneeId is { } assigneeId)
        {
            issues = issues.Where(i => i.AssigneeId == assigneeId);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var search = query.SearchText.Trim();
            issues = issues.Where(i => i.Title.Contains(search) || i.Key.Contains(search));
        }

        return await issues
            .OrderByDescending(i => i.UpdatedAt)
            .ThenByDescending(i => i.Number)
            .ToListAsync(cancellationToken);
    }

    public Task<Issue?> GetByKeyAsync(string key, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking()
            .Include(i => i.Project)
            .Include(i => i.Assignee)
            .Include(i => i.Reporter)
            .FirstOrDefaultAsync(i => i.Key == key, cancellationToken);

    public Task<Issue?> GetWithRelationsAsync(Guid id, CancellationToken cancellationToken = default) =>
        Set.Include(i => i.Project)
            .Include(i => i.Assignee)
            .Include(i => i.Reporter)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
}
