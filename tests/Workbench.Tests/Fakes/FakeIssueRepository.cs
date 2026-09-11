using Workbench.Domain.Entities;
using Workbench.Domain.Enums;
using Workbench.Domain.Interfaces;

namespace Workbench.Tests.Fakes;

public sealed class FakeIssueRepository : IIssueRepository
{
    private static readonly IssueStatus[] ClosedStatuses = [IssueStatus.Done, IssueStatus.Cancelled];

    private readonly List<Issue> _issues = [];

    public IReadOnlyList<Issue> Issues => _issues;

    public Issue Seed(Issue issue)
    {
        _issues.Add(issue);
        return issue;
    }

    public Task<IReadOnlyList<Issue>> ListAsync(
        IssueQuery query,
        CancellationToken cancellationToken = default)
    {
        var issues = _issues.AsEnumerable();

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

        return Task.FromResult<IReadOnlyList<Issue>>([.. issues.OrderByDescending(i => i.UpdatedAt)]);
    }

    public Task<Issue?> GetByKeyAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_issues.SingleOrDefault(i => i.Key == key));

    public Task<Issue?> GetWithRelationsAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetByIdAsync(id, cancellationToken);

    public Task<Issue?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_issues.SingleOrDefault(i => i.Id == id));

    public Task<IReadOnlyList<Issue>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Issue>>(_issues);

    public Task AddAsync(Issue entity, CancellationToken cancellationToken = default)
    {
        _issues.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(Issue entity)
    {
    }

    public void Remove(Issue entity) => _issues.Remove(entity);
}
