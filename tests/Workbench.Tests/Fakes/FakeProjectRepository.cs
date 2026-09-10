using Workbench.Domain.Entities;
using Workbench.Domain.Interfaces;

namespace Workbench.Tests.Fakes;

/// <summary>
/// 서비스의 업무 규칙만 시험하기 위한 메모리 저장소. EF 를 끼우면 규칙이 아니라
/// 쿼리 번역을 시험하게 된다 — 그쪽은 모델 형상 계약이 따로 담당한다.
/// </summary>
public sealed class FakeProjectRepository : IProjectRepository
{
    private readonly List<Project> _projects = [];

    public Dictionary<Guid, int> IssueCounts { get; } = [];

    public IReadOnlyList<Project> Projects => _projects;

    public int SaveCount { get; private set; }

    public Project Seed(string key, string name, int issueCount = 0)
    {
        var project = new Project { Key = key, Name = name, CreatedById = Guid.NewGuid() };
        _projects.Add(project);

        if (issueCount > 0)
        {
            IssueCounts[project.Id] = issueCount;
        }

        return project;
    }

    public void MarkSaved() => SaveCount++;

    public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_projects.SingleOrDefault(p => p.Id == id));

    public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Project>>(_projects);

    public Task<IReadOnlyList<Project>> ListOrderedByKeyAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Project>>([.. _projects.OrderBy(p => p.Key, StringComparer.Ordinal)]);

    public Task<Project?> GetByKeyAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_projects.SingleOrDefault(p => p.Key == key));

    public Task<bool> KeyExistsAsync(
        string key,
        Guid? excludingProjectId = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_projects.Any(p => p.Key == key && p.Id != excludingProjectId));

    public Task<IReadOnlyDictionary<Guid, int>> GetIssueCountsAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, int>>(IssueCounts);

    public Task AddAsync(Project entity, CancellationToken cancellationToken = default)
    {
        _projects.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(Project entity)
    {
    }

    public void Remove(Project entity) => _projects.Remove(entity);
}
