using Workbench.Domain.Entities;
using Workbench.Domain.Interfaces;

namespace Workbench.Tests.Fakes;

public sealed class FakeCommentRepository : ICommentRepository
{
    private readonly List<Comment> _comments = [];

    public IReadOnlyList<Comment> Comments => _comments;

    public Comment Seed(Comment comment)
    {
        _comments.Add(comment);
        return comment;
    }

    public Task<IReadOnlyList<Comment>> ListForIssueAsync(
        Guid issueId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Comment>>(
            [.. _comments.Where(c => c.IssueId == issueId).OrderBy(c => c.CreatedAt)]);

    public Task<IReadOnlyList<Comment>> ListForPageAsync(
        Guid pageId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Comment>>(
            [.. _comments.Where(c => c.PageId == pageId).OrderBy(c => c.CreatedAt)]);

    public Task<Comment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_comments.SingleOrDefault(c => c.Id == id));

    public Task<IReadOnlyList<Comment>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Comment>>(_comments);

    public Task AddAsync(Comment entity, CancellationToken cancellationToken = default)
    {
        _comments.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(Comment entity)
    {
    }

    public void Remove(Comment entity) => _comments.Remove(entity);
}
