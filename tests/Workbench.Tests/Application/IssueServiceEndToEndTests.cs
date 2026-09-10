using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Workbench.Application.DTOs;
using Workbench.Application.Services;
using Workbench.Domain.Entities;
using Workbench.Domain.Enums;
using Workbench.Domain.Interfaces;
using Workbench.Infrastructure.Data;
using Workbench.Infrastructure.Repositories;
using Workbench.Tests.Fakes;
using Workbench.Tests.Infrastructure;

namespace Workbench.Tests.Application;

public sealed class IssueServiceEndToEndTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly WorkbenchDbContext _dbContext;
    private readonly IssueService _issues;
    private readonly ProjectService _projects;

    public IssueServiceEndToEndTests()
    {
        _dbContext = _database.Context;
        _projects = _database.Projects;
        _issues = _database.Issues;
    }

    [Fact]
    public async Task CreatedIssue_IsReadableByItsKey()
    {
        var projectId = await CreateProjectAsync();

        var created = await _issues.CreateAsync(new IssueEditModel
        {
            ProjectId = projectId,
            Title = "  로그인 실패  ",
            DescriptionMarkdown = "## 재현\n1. 로그인",
            Type = IssueType.Bug,
            Priority = IssuePriority.High,
            Environment = "PRD",
            Version = "1.2.0",
            DueDate = new DateOnly(2026, 12, 31),
        });

        Assert.True(created.Succeeded);
        Assert.Equal("DEV-1", created.Value);

        var detail = await _issues.GetByKeyAsync("dev-1");

        Assert.NotNull(detail);
        Assert.Equal("로그인 실패", detail.Title);
        Assert.Equal(IssueType.Bug, detail.Type);
        Assert.Equal("PRD", detail.Environment);
        Assert.Equal(new DateOnly(2026, 12, 31), detail.DueDate);
        Assert.Equal("DEV", detail.ProjectKey);
        Assert.Equal("홍길동", detail.ReporterName);
    }

    [Fact]
    public async Task DeletedIssueNumber_IsNotReused()
    {
        // 지워진 키가 다른 이슈로 되살아나면 과거 링크·문서가 엉뚱한 이슈를 가리킨다.
        var projectId = await CreateProjectAsync();

        await _issues.CreateAsync(Model(projectId, "첫째"));
        var second = await _issues.CreateAsync(Model(projectId, "둘째"));

        var toDelete = await _dbContext.Issues.SingleAsync(i => i.Key == second.Value);
        Assert.True((await _issues.DeleteAsync(toDelete.Id)).Succeeded);

        var third = await _issues.CreateAsync(Model(projectId, "셋째"));

        Assert.Equal("DEV-3", third.Value);
    }

    [Fact]
    public async Task DuplicateIssueNumber_IsRejectedByTheDatabase()
    {
        var projectId = await CreateProjectAsync();
        await _issues.CreateAsync(Model(projectId, "첫째"));

        var project = await _dbContext.Projects.SingleAsync();
        _dbContext.Issues.Add(new Issue
        {
            ProjectId = project.Id,
            Number = 1,
            Key = Issue.FormatKey(project.Key, 1),
            Title = "번호 중복",
            ReporterId = FakeCurrentUser.DefaultUserId,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => _dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task DeletingAProject_RemovesItsIssues()
    {
        // Projects → Issues 는 CASCADE 다. 서비스가 이슈 남은 프로젝트 삭제를 막는 이유이기도 하다.
        var projectId = await CreateProjectAsync();
        await _issues.CreateAsync(Model(projectId, "이슈"));

        var refused = await _projects.DeleteAsync(projectId);
        Assert.False(refused.Succeeded);

        var issue = await _dbContext.Issues.SingleAsync();
        await _issues.DeleteAsync(issue.Id);

        Assert.True((await _projects.DeleteAsync(projectId)).Succeeded);
        Assert.Equal(0, await _dbContext.Issues.CountAsync());
    }

    [Fact]
    public async Task Filters_NarrowTheList()
    {
        var devId = await CreateProjectAsync();
        var opsId = await CreateProjectAsync("OPS", "운영");

        await _issues.CreateAsync(Model(devId, "로그인 오류"));
        await _issues.CreateAsync(Model(devId, "배포 스크립트"));
        await _issues.CreateAsync(Model(opsId, "서버 점검"));

        Assert.Equal(2, (await _issues.ListAsync(new IssueQuery(ProjectId: devId))).Count);
        Assert.Equal("서버 점검", Assert.Single(await _issues.ListAsync(new IssueQuery(SearchText: "점검"))).Title);
        Assert.Equal("DEV-1", Assert.Single(await _issues.ListAsync(new IssueQuery(SearchText: "DEV-1"))).Key);
    }

    public void Dispose() => _database.Dispose();

    private static IssueEditModel Model(Guid projectId, string title) =>
        new() { ProjectId = projectId, Title = title };

    private async Task<Guid> CreateProjectAsync(string key = "DEV", string name = "개발")
    {
        var created = await _projects.CreateAsync(new ProjectEditModel { Key = key, Name = name });
        Assert.True(created.Succeeded);

        return created.Value;
    }
}
