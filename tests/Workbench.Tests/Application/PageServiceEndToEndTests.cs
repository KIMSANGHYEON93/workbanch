using Microsoft.EntityFrameworkCore;
using Workbench.Application.DTOs;
using Workbench.Application.Services;
using Workbench.Domain.Entities;
using Workbench.Infrastructure.Data;
using Workbench.Infrastructure.Repositories;
using Workbench.Tests.Fakes;
using Workbench.Tests.Infrastructure;

namespace Workbench.Tests.Application;

public sealed class PageServiceEndToEndTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly WorkbenchDbContext _dbContext;
    private readonly PageService _pages;
    private readonly ProjectService _projects;

    public PageServiceEndToEndTests()
    {
        _dbContext = _database.Context;
        _projects = _database.Projects;
        _pages = _database.Pages;
    }

    [Fact]
    public async Task CreatedPage_IsReadableByItsSlug()
    {
        var created = await _pages.CreateAsync(new PageEditModel
        {
            Title = "  배포 절차  ",
            ContentMarkdown = "## 순서\n1. 빌드",
        });

        Assert.True(created.Succeeded);
        Assert.Equal("배포-절차", created.Value);

        var detail = await _pages.GetBySlugAsync("배포-절차");

        Assert.NotNull(detail);
        Assert.Equal("배포 절차", detail.Title);
        Assert.Equal("홍길동", detail.CreatedByName);
    }

    [Fact]
    public async Task DuplicateSlug_IsRejectedByTheDatabase()
    {
        // 서비스의 사전 회피와 별개로, 동시 생성 경합은 인덱스가 막아야 한다.
        await _pages.CreateAsync(new PageEditModel { Title = "배포 절차" });

        _dbContext.Pages.Add(new Page
        {
            Title = "중복",
            Slug = "배포-절차",
            CreatedById = FakeCurrentUser.DefaultUserId,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => _dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Hierarchy_SurvivesARoundTrip()
    {
        var root = await _pages.CreateAsync(new PageEditModel { Title = "운영 가이드" });
        var rootId = (await _dbContext.Pages.SingleAsync(p => p.Slug == root.Value)).Id;

        await _pages.CreateAsync(new PageEditModel { Title = "백업", ParentPageId = rootId });
        await _pages.CreateAsync(new PageEditModel { Title = "복구", ParentPageId = rootId });

        var tree = await _pages.ListTreeAsync();

        Assert.Equal(["운영 가이드", "백업", "복구"], tree.Select(p => p.Title));
        Assert.Equal([0, 1, 1], tree.Select(p => p.Depth));
    }

    [Fact]
    public async Task DeletingAProject_KeepsItsPagesAndClearsTheLink()
    {
        // 설계 결정 "프로젝트를 지워도 지식 문서는 남긴다"(Pages.ProjectId = SET NULL)를
        // 문서로만 두지 않고 실제로 실행해 확인한다.
        var project = await _projects.CreateAsync(new ProjectEditModel { Key = "DEV", Name = "개발" });
        var created = await _pages.CreateAsync(new PageEditModel
        {
            Title = "프로젝트 문서",
            ProjectId = project.Value,
        });

        Assert.True((await _projects.DeleteAsync(project.Value)).Succeeded);

        _dbContext.ChangeTracker.Clear();
        var page = await _dbContext.Pages.SingleAsync(p => p.Slug == created.Value);

        Assert.Null(page.ProjectId);
        Assert.Equal("프로젝트 문서", page.Title);
    }

    [Fact]
    public async Task DeletingAParentWithChildren_IsRefusedBeforeReachingTheDatabase()
    {
        // 자기참조 FK 는 NO ACTION 이라 DB 가 거절한다 — 드라이버 오류를 화면에 흘리지 않고
        // 서비스가 먼저 막고 무엇을 해야 하는지 알려준다.
        var root = await _pages.CreateAsync(new PageEditModel { Title = "루트" });
        var rootId = (await _dbContext.Pages.SingleAsync(p => p.Slug == root.Value)).Id;
        await _pages.CreateAsync(new PageEditModel { Title = "자식", ParentPageId = rootId });

        var result = await _pages.DeleteAsync(rootId);

        Assert.False(result.Succeeded);
        Assert.Equal(2, await _dbContext.Pages.CountAsync());
    }

    [Fact]
    public async Task Search_MatchesTitleAndBody()
    {
        await _pages.CreateAsync(new PageEditModel { Title = "배포 절차", ContentMarkdown = "무관한 본문" });
        await _pages.CreateAsync(new PageEditModel { Title = "무관한 제목", ContentMarkdown = "배포 관련 내용" });
        await _pages.CreateAsync(new PageEditModel { Title = "다른 문서", ContentMarkdown = "다른 내용" });

        var results = await _pages.SearchAsync("배포");

        Assert.Equal(2, results.Count);
    }

    public void Dispose() => _database.Dispose();
}
