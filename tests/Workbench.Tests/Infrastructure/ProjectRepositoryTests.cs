using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Workbench.Domain.Entities;
using Workbench.Infrastructure.Data;
using Workbench.Infrastructure.Repositories;

namespace Workbench.Tests.Infrastructure;

/// <summary>
/// 실제 EF 프로바이더로 도는 저장소 테스트. 페이크는 LINQ 번역 실패를 잡지 못한다 —
/// 예를 들어 <c>excludingProjectId == null || ...</c> 같은 식이 SQL 로 번역되는지는
/// 여기서만 드러난다.
/// ⚠ SQLite 는 SQL Server 가 아니다. 스키마 세부(인덱스 키 크기 등)는 모델 형상 계약이 담당한다.
/// </summary>
public sealed class ProjectRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly WorkbenchDbContext _dbContext;
    private readonly ProjectRepository _repository;

    public ProjectRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<WorkbenchDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new WorkbenchDbContext(options);
        _dbContext.Database.EnsureCreated();
        _repository = new ProjectRepository(_dbContext);
    }

    [Fact]
    public async Task KeyExistsAsync_FindsAnExistingKey()
    {
        await SeedProjectAsync("DEV", "개발");

        Assert.True(await _repository.KeyExistsAsync("DEV"));
        Assert.False(await _repository.KeyExistsAsync("OPS"));
    }

    [Fact]
    public async Task KeyExistsAsync_ExcludesTheProjectBeingEdited()
    {
        var project = await SeedProjectAsync("DEV", "개발");

        // 자기 자신을 중복으로 세면 이름만 바꾸는 수정이 영원히 거부된다.
        Assert.False(await _repository.KeyExistsAsync("DEV", project.Id));
        Assert.True(await _repository.KeyExistsAsync("DEV", Guid.NewGuid()));
    }

    [Fact]
    public async Task GetIssueCountsAsync_GroupsByProject()
    {
        var dev = await SeedProjectAsync("DEV", "개발");
        var ops = await SeedProjectAsync("OPS", "운영");
        await SeedIssuesAsync(dev, 2);
        await SeedIssuesAsync(ops, 1);

        var counts = await _repository.GetIssueCountsAsync();

        Assert.Equal(2, counts[dev.Id]);
        Assert.Equal(1, counts[ops.Id]);
    }

    [Fact]
    public async Task GetIssueCountsAsync_OmitsProjectsWithoutIssues()
    {
        var project = await SeedProjectAsync("DEV", "개발");

        // 서비스가 GetValueOrDefault 로 0 을 채우므로, 키가 없는 것이 정상 동작이다.
        Assert.DoesNotContain(project.Id, (await _repository.GetIssueCountsAsync()).Keys);
    }

    [Fact]
    public async Task ListOrderedByKeyAsync_SortsByKey()
    {
        await SeedProjectAsync("OPS", "운영");
        await SeedProjectAsync("DEV", "개발");

        var projects = await _repository.ListOrderedByKeyAsync();

        Assert.Equal(["DEV", "OPS"], projects.Select(p => p.Key));
    }

    [Fact]
    public async Task DuplicateKey_IsRejectedByTheDatabase()
    {
        // 서비스의 사전 검사와 별개로, 동시 생성 경합은 인덱스가 막아야 한다.
        await SeedProjectAsync("DEV", "개발");

        _dbContext.Projects.Add(new Project { Key = "DEV", Name = "중복", CreatedById = await UserIdAsync() });

        await Assert.ThrowsAsync<DbUpdateException>(() => _dbContext.SaveChangesAsync());
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private async Task<Project> SeedProjectAsync(string key, string name)
    {
        var project = new Project { Key = key, Name = name, CreatedById = await UserIdAsync() };
        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync();

        return project;
    }

    private async Task SeedIssuesAsync(Project project, int count)
    {
        var reporterId = await UserIdAsync();

        for (var i = 1; i <= count; i++)
        {
            project.LastIssueNumber++;
            _dbContext.Issues.Add(new Issue
            {
                ProjectId = project.Id,
                Number = project.LastIssueNumber,
                Key = Issue.FormatKey(project.Key, project.LastIssueNumber),
                Title = $"이슈 {i}",
                ReporterId = reporterId,
            });
        }

        await _dbContext.SaveChangesAsync();
    }

    private async Task<Guid> UserIdAsync()
    {
        var existing = await _dbContext.Users.FirstOrDefaultAsync();
        if (existing is not null)
        {
            return existing.Id;
        }

        var user = new AppUser { DisplayName = "홍길동", Email = "gildong@example.com" };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        return user.Id;
    }
}
