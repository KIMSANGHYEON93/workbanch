using Workbench.Application.DTOs;
using Workbench.Application.Services;
using Workbench.Domain.Entities;
using Workbench.Tests.Fakes;

namespace Workbench.Tests.Application;

public class CommentServiceTests
{
    private static readonly Guid OtherUserId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    [Fact]
    public async Task AddToIssue_StoresTheCommentAgainstThatIssueOnly()
    {
        // 이슈와 페이지를 동시에 가리키는 댓글은 DB CHECK 제약이 거절한다.
        var harness = new Harness();
        var issue = harness.SeedIssue();

        var result = await harness.Service.AddToIssueAsync(issue.Id, "  확인했습니다  ");

        Assert.True(result.Succeeded);

        var comment = Assert.Single(harness.Comments.Comments);
        Assert.Equal(issue.Id, comment.IssueId);
        Assert.Null(comment.PageId);
        Assert.Equal("확인했습니다", comment.ContentMarkdown);
        Assert.Equal(FakeCurrentUser.DefaultUserId, comment.AuthorId);
    }

    [Fact]
    public async Task AddToPage_StoresTheCommentAgainstThatPageOnly()
    {
        var harness = new Harness();
        var page = harness.Pages.Seed("문서", "문서");

        Assert.True((await harness.Service.AddToPageAsync(page.Id, "메모")).Succeeded);

        var comment = Assert.Single(harness.Comments.Comments);
        Assert.Equal(page.Id, comment.PageId);
        Assert.Null(comment.IssueId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Add_RejectsEmptyContent(string? content)
    {
        var harness = new Harness();
        var issue = harness.SeedIssue();

        Assert.False((await harness.Service.AddToIssueAsync(issue.Id, content)).Succeeded);
        Assert.Empty(harness.Comments.Comments);
    }

    [Fact]
    public async Task Add_RejectsAnUnknownTarget()
    {
        var harness = new Harness();

        Assert.False((await harness.Service.AddToIssueAsync(Guid.NewGuid(), "내용")).Succeeded);
        Assert.False((await harness.Service.AddToPageAsync(Guid.NewGuid(), "내용")).Succeeded);
        Assert.Empty(harness.Comments.Comments);
    }

    [Fact]
    public async Task Delete_RefusesSomeoneElsesComment()
    {
        // 남의 대화 기록을 지울 수 있으면 이력이 신뢰를 잃는다. 화면에서 버튼을 감추는 것만으로는
        // 부족하므로 서버가 판정한다.
        var harness = new Harness();
        var issue = harness.SeedIssue();
        var comment = harness.Comments.Seed(new Comment
        {
            IssueId = issue.Id,
            ContentMarkdown = "남의 댓글",
            AuthorId = OtherUserId,
        });

        var result = await harness.Service.DeleteAsync(comment.Id);

        Assert.False(result.Succeeded);
        Assert.Single(harness.Comments.Comments);
        Assert.Equal(0, harness.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task Delete_RemovesTheAuthorsOwnComment()
    {
        var harness = new Harness();
        var issue = harness.SeedIssue();
        await harness.Service.AddToIssueAsync(issue.Id, "내 댓글");
        var comment = Assert.Single(harness.Comments.Comments);

        Assert.True((await harness.Service.DeleteAsync(comment.Id)).Succeeded);
        Assert.Empty(harness.Comments.Comments);
    }

    [Fact]
    public async Task List_MarksOnlyTheViewersOwnCommentsAsDeletable()
    {
        var harness = new Harness();
        var issue = harness.SeedIssue();
        await harness.Service.AddToIssueAsync(issue.Id, "내 댓글");
        harness.Comments.Seed(new Comment
        {
            IssueId = issue.Id,
            ContentMarkdown = "남의 댓글",
            AuthorId = OtherUserId,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(1),
        });

        var items = await harness.Service.ListForIssueAsync(issue.Id);

        Assert.Collection(
            items,
            mine => Assert.True(mine.CanDelete),
            theirs => Assert.False(theirs.CanDelete));
    }

    [Fact]
    public async Task List_KeepsIssueAndPageThreadsSeparate()
    {
        var harness = new Harness();
        var issue = harness.SeedIssue();
        var page = harness.Pages.Seed("문서", "문서");

        await harness.Service.AddToIssueAsync(issue.Id, "이슈 댓글");
        await harness.Service.AddToPageAsync(page.Id, "페이지 댓글");

        Assert.Equal("이슈 댓글", Assert.Single(await harness.Service.ListForIssueAsync(issue.Id)).ContentMarkdown);
        Assert.Equal("페이지 댓글", Assert.Single(await harness.Service.ListForPageAsync(page.Id)).ContentMarkdown);
    }

    private sealed class Harness
    {
        public Harness()
        {
            UnitOfWork = new FakeUnitOfWork();
            Service = new CommentService(Comments, Issues, Pages, UnitOfWork, new FakeCurrentUser());
        }

        public FakeCommentRepository Comments { get; } = new();

        public FakeIssueRepository Issues { get; } = new();

        public FakePageRepository Pages { get; } = new();

        public FakeUnitOfWork UnitOfWork { get; }

        public CommentService Service { get; }

        public Issue SeedIssue() => Issues.Seed(new Issue
        {
            ProjectId = Guid.NewGuid(),
            Number = 1,
            Key = "DEV-1",
            Title = "이슈",
            ReporterId = FakeCurrentUser.DefaultUserId,
        });
    }
}
