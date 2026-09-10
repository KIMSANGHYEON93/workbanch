using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Workbench.Application.DTOs;
using Workbench.Application.Services;
using Workbench.Domain.Entities;
using Workbench.Infrastructure.Data;
using Workbench.Infrastructure.Repositories;
using Workbench.Tests.Fakes;
using Workbench.Tests.Infrastructure;

namespace Workbench.Tests.Application;

public sealed class CommentServiceEndToEndTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly WorkbenchDbContext _dbContext;
    private readonly CommentService _comments;
    private readonly IssueService _issues;
    private readonly PageService _pages;
    private readonly ProjectService _projects;

    public CommentServiceEndToEndTests()
    {
        _dbContext = _database.Context;

        var currentUser = new FakeCurrentUser();
        var projectRepository = new ProjectRepository(_dbContext);
        var issueRepository = new IssueRepository(_dbContext);
        var pageRepository = new PageRepository(_dbContext);

        _projects = new ProjectService(projectRepository, _dbContext, currentUser);
        _pages = new PageService(pageRepository, projectRepository, _dbContext, currentUser);
        _issues = new IssueService(
            issueRepository,
            projectRepository,
            new AppUserRepository(_dbContext),
            _dbContext,
            currentUser,
            NullLogger<IssueService>.Instance);
        _comments = new CommentService(
            new CommentRepository(_dbContext),
            issueRepository,
            pageRepository,
            _dbContext,
            currentUser);
    }

    [Fact]
    public async Task CommentsRoundTripInChronologicalOrder()
    {
        var issueId = await CreateIssueAsync();

        await _comments.AddToIssueAsync(issueId, "첫 댓글");
        await _comments.AddToIssueAsync(issueId, "둘째 댓글");

        var thread = await _comments.ListForIssueAsync(issueId);

        Assert.Equal(["첫 댓글", "둘째 댓글"], thread.Select(c => c.ContentMarkdown));
        Assert.All(thread, c => Assert.Equal("홍길동", c.AuthorName));
    }

    [Fact]
    public async Task CommentWithBothOwners_IsRejectedByTheCheckConstraint()
    {
        // 서비스 API 는 대상 두 개를 받는 메서드가 없어 이 상태를 만들 수 없다.
        // 저장소를 직접 쓰는 코드가 생겨도 DB 가 막는다는 것을 여기서 확인한다.
        var issueId = await CreateIssueAsync();
        var page = await _pages.CreateAsync(new PageEditModel { Title = "문서" });
        var pageId = (await _dbContext.Pages.SingleAsync(p => p.Slug == page.Value)).Id;

        _dbContext.Comments.Add(new Comment
        {
            IssueId = issueId,
            PageId = pageId,
            ContentMarkdown = "양쪽에 달린 댓글",
            AuthorId = FakeCurrentUser.DefaultUserId,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => _dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task CommentWithNoOwner_IsRejectedByTheCheckConstraint()
    {
        _dbContext.Comments.Add(new Comment
        {
            ContentMarkdown = "고아 댓글",
            AuthorId = FakeCurrentUser.DefaultUserId,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => _dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task DeletingAnIssue_RemovesItsComments()
    {
        // Issues → Comments 는 CASCADE 다. 이슈를 지운 뒤 댓글이 남으면 고아 행이 쌓인다.
        var issueId = await CreateIssueAsync();
        await _comments.AddToIssueAsync(issueId, "댓글");

        Assert.Equal(1, await _dbContext.Comments.CountAsync());
        Assert.True((await _issues.DeleteAsync(issueId)).Succeeded);

        Assert.Equal(0, await _dbContext.Comments.CountAsync());
    }

    [Fact]
    public async Task DeletingAPage_RemovesItsComments()
    {
        var created = await _pages.CreateAsync(new PageEditModel { Title = "문서" });
        var pageId = (await _dbContext.Pages.SingleAsync(p => p.Slug == created.Value)).Id;
        await _comments.AddToPageAsync(pageId, "댓글");

        Assert.True((await _pages.DeleteAsync(pageId)).Succeeded);
        Assert.Equal(0, await _dbContext.Comments.CountAsync());
    }

    public void Dispose() => _database.Dispose();

    private async Task<Guid> CreateIssueAsync()
    {
        var project = await _projects.CreateAsync(new ProjectEditModel { Key = "DEV", Name = "개발" });
        var created = await _issues.CreateAsync(new IssueEditModel
        {
            ProjectId = project.Value,
            Title = "이슈",
        });

        Assert.True(created.Succeeded);

        return (await _dbContext.Issues.SingleAsync(i => i.Key == created.Value)).Id;
    }
}
