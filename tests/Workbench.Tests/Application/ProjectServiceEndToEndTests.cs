using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Workbench.Application.DTOs;
using Workbench.Application.Services;
using Workbench.Domain.Entities;
using Workbench.Infrastructure.Data;
using Workbench.Infrastructure.Repositories;
using Workbench.Tests.Fakes;

namespace Workbench.Tests.Application;

/// <summary>
/// 서비스 → 저장소 → 실제 DbContext 를 그대로 통과시킨다. 페이크 조합에서는 통과하지만
/// 실제 배선(예: IUnitOfWork 가 DbContext 라는 사실)에서 깨지는 경우를 잡는 것이 목적이다.
/// </summary>
public sealed class ProjectServiceEndToEndTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly WorkbenchDbContext _dbContext;
    private readonly ProjectService _service;

    public ProjectServiceEndToEndTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _dbContext = new WorkbenchDbContext(
            new DbContextOptionsBuilder<WorkbenchDbContext>().UseSqlite(_connection).Options);
        _dbContext.Database.EnsureCreated();

        _dbContext.Users.Add(new AppUser
        {
            Id = FakeCurrentUser.DefaultUserId,
            DisplayName = "홍길동",
            Email = "gildong@example.com",
        });
        _dbContext.SaveChanges();

        _service = new ProjectService(
            new ProjectRepository(_dbContext),
            _dbContext,
            new FakeCurrentUser());
    }

    [Fact]
    public async Task CreateListUpdateDelete_RoundTripsThroughTheDatabase()
    {
        var created = await _service.CreateAsync(
            new ProjectEditModel { Key = "dev", Name = "개발", Description = "  배포 일정  " });

        Assert.True(created.Succeeded);
        var id = created.Value;

        var afterCreate = Assert.Single(await _service.ListAsync());
        Assert.Equal("DEV", afterCreate.Key);
        Assert.Equal("배포 일정", afterCreate.Description);
        Assert.Equal(0, afterCreate.IssueCount);

        var updated = await _service.UpdateAsync(
            id,
            new ProjectEditModel { Key = "DEV", Name = "개발 플랫폼", Description = null });

        Assert.True(updated.Succeeded);

        var detail = await _service.GetAsync(id);
        Assert.Equal("개발 플랫폼", detail!.Name);
        Assert.Null(detail.Description);

        Assert.True((await _service.DeleteAsync(id)).Succeeded);
        Assert.Empty(await _service.ListAsync());
    }

    [Fact]
    public async Task DuplicateKey_IsRejectedBeforeTouchingTheDatabase()
    {
        await _service.CreateAsync(new ProjectEditModel { Key = "DEV", Name = "개발" });

        var duplicate = await _service.CreateAsync(new ProjectEditModel { Key = " dev ", Name = "중복" });

        Assert.False(duplicate.Succeeded);
        Assert.Equal(1, await _dbContext.Projects.CountAsync());
    }

    [Fact]
    public async Task DeleteIsRefused_WhileTheProjectStillHasIssues()
    {
        var created = await _service.CreateAsync(new ProjectEditModel { Key = "DEV", Name = "개발" });
        var project = await _dbContext.Projects.SingleAsync();

        _dbContext.Issues.Add(new Issue
        {
            ProjectId = project.Id,
            Number = 1,
            Key = Issue.FormatKey(project.Key, 1),
            Title = "첫 이슈",
            ReporterId = FakeCurrentUser.DefaultUserId,
        });
        await _dbContext.SaveChangesAsync();

        var deleted = await _service.DeleteAsync(created.Value);

        Assert.False(deleted.Succeeded);
        Assert.Equal(1, await _dbContext.Projects.CountAsync());
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }
}
