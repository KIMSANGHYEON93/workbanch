using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Workbench.Application;
using Workbench.Application.Services;
using Workbench.Domain.Entities;
using Workbench.Tests.Fakes;

namespace Workbench.Tests.Application;

public class AttachmentServiceTests
{
    private static readonly Guid OtherUserId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    [Fact]
    public async Task AttachToIssue_StoresMetadataAndBytes()
    {
        var harness = new Harness();
        var issue = harness.SeedIssue();

        var result = await harness.Service.AttachToIssueAsync(
            issue.Id, Content("로그"), "배포.log", "text/plain", size: 6);

        Assert.True(result.Succeeded);

        var attachment = Assert.Single(harness.Attachments.Attachments);
        Assert.Equal(issue.Id, attachment.IssueId);
        Assert.Null(attachment.PageId);
        Assert.Equal("배포.log", attachment.FileName);
        Assert.Equal(FakeCurrentUser.DefaultUserId, attachment.UploadedById);
        Assert.Single(harness.Storage.Files);
    }

    [Fact]
    public async Task StoredPath_NeverContainsTheUsersFileName()
    {
        // 원본 이름은 DB 컬럼에만 남는다 — 경로에 들어가면 저장소 밖으로 나갈 수 있다.
        var harness = new Harness();
        var issue = harness.SeedIssue();

        await harness.Service.AttachToIssueAsync(
            issue.Id, Content("x"), "../../web.config", "text/plain", size: 1);

        var attachment = Assert.Single(harness.Attachments.Attachments);

        Assert.DoesNotContain("..", attachment.BlobPath, StringComparison.Ordinal);
        Assert.DoesNotContain("web", attachment.BlobPath, StringComparison.Ordinal);
        Assert.Equal("../../web.config", attachment.FileName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Attach_RejectsEmptyFiles(long size)
    {
        var harness = new Harness();
        var issue = harness.SeedIssue();

        Assert.False(
            (await harness.Service.AttachToIssueAsync(issue.Id, Content(""), "a.txt", "text/plain", size))
            .Succeeded);
        Assert.Empty(harness.Attachments.Attachments);
        Assert.Empty(harness.Storage.Files);
    }

    [Fact]
    public async Task Attach_RejectsFilesOverTheLimit()
    {
        var harness = new Harness(maxFileSizeBytes: 10);
        var issue = harness.SeedIssue();

        var result = await harness.Service.AttachToIssueAsync(
            issue.Id, Content("x"), "big.bin", "application/octet-stream", size: 11);

        Assert.False(result.Succeeded);

        // 상한 검사는 저장 전에 끝나야 한다 — 통과시키고 나서 지우면 그 사이 디스크를 다 쓴다.
        Assert.Empty(harness.Storage.Files);
    }

    [Fact]
    public async Task Attach_RejectsABlankFileName()
    {
        var harness = new Harness();
        var issue = harness.SeedIssue();

        Assert.False(
            (await harness.Service.AttachToIssueAsync(issue.Id, Content("x"), "   ", "text/plain", 1))
            .Succeeded);
    }

    [Fact]
    public async Task Attach_RejectsAnUnknownTarget()
    {
        var harness = new Harness();

        Assert.False((await harness.Service.AttachToIssueAsync(
            Guid.NewGuid(), Content("x"), "a.txt", "text/plain", 1)).Succeeded);
        Assert.False((await harness.Service.AttachToPageAsync(
            Guid.NewGuid(), Content("x"), "a.txt", "text/plain", 1)).Succeeded);
        Assert.Empty(harness.Storage.Files);
    }

    [Fact]
    public async Task Attach_FallsBackToASafeContentTypeWhenTheBrowserSaysNothing()
    {
        var harness = new Harness();
        var issue = harness.SeedIssue();

        await harness.Service.AttachToIssueAsync(issue.Id, Content("x"), "a.bin", null, size: 1);

        Assert.Equal("application/octet-stream", Assert.Single(harness.Attachments.Attachments).ContentType);
    }

    [Fact]
    public async Task Attach_RemovesTheBytesWhenMetadataCannotBeSaved()
    {
        // 메타데이터가 없으면 아무도 그 파일을 찾을 수 없다 — 조용히 쌓이게 두지 않는다.
        var harness = new Harness();
        harness.UnitOfWork.FailFirstSaves = 1;
        var issue = harness.SeedIssue();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Service.AttachToIssueAsync(issue.Id, Content("x"), "a.txt", "text/plain", 1));

        Assert.Empty(harness.Storage.Files);
        Assert.Single(harness.Storage.Deleted);
    }

    [Fact]
    public async Task Delete_RefusesSomeoneElsesFile()
    {
        var harness = new Harness();
        var issue = harness.SeedIssue();
        var attachment = harness.Attachments.Seed(new Attachment
        {
            IssueId = issue.Id,
            FileName = "남의 파일.txt",
            ContentType = "text/plain",
            Size = 1,
            BlobPath = "someone-else",
            UploadedById = OtherUserId,
        });

        Assert.False((await harness.Service.DeleteAsync(attachment.Id)).Succeeded);
        Assert.Single(harness.Attachments.Attachments);
        Assert.Empty(harness.Storage.Deleted);
    }

    [Fact]
    public async Task Delete_RemovesBothTheRowAndTheBytes()
    {
        var harness = new Harness();
        var issue = harness.SeedIssue();
        await harness.Service.AttachToIssueAsync(issue.Id, Content("x"), "a.txt", "text/plain", 1);
        var attachment = Assert.Single(harness.Attachments.Attachments);

        Assert.True((await harness.Service.DeleteAsync(attachment.Id)).Succeeded);

        Assert.Empty(harness.Attachments.Attachments);
        Assert.Contains(attachment.BlobPath, harness.Storage.Deleted);
    }

    [Fact]
    public async Task Open_ReturnsTheOriginalNameNotTheStoredPath()
    {
        var harness = new Harness();
        var issue = harness.SeedIssue();
        await harness.Service.AttachToIssueAsync(issue.Id, Content("본문"), "보고서.pdf", "application/pdf", 6);
        var attachment = Assert.Single(harness.Attachments.Attachments);

        var content = await harness.Service.OpenAsync(attachment.Id);

        Assert.NotNull(content);
        Assert.Equal("보고서.pdf", content.FileName);
        Assert.Equal("application/pdf", content.ContentType);
        await content.Content.DisposeAsync();
    }

    [Fact]
    public async Task Open_ReturnsNothingWhenTheBytesAreGone()
    {
        var harness = new Harness();
        var issue = harness.SeedIssue();
        var attachment = harness.Attachments.Seed(new Attachment
        {
            IssueId = issue.Id,
            FileName = "사라진 파일.txt",
            ContentType = "text/plain",
            Size = 1,
            BlobPath = "missing",
            UploadedById = FakeCurrentUser.DefaultUserId,
        });

        Assert.Null(await harness.Service.OpenAsync(attachment.Id));
    }

    [Fact]
    public async Task List_MarksOnlyTheViewersOwnFilesAsDeletable()
    {
        var harness = new Harness();
        var issue = harness.SeedIssue();
        await harness.Service.AttachToIssueAsync(issue.Id, Content("x"), "내 파일.txt", "text/plain", 1);
        harness.Attachments.Seed(new Attachment
        {
            IssueId = issue.Id,
            FileName = "남의 파일.txt",
            ContentType = "text/plain",
            Size = 1,
            BlobPath = "other",
            UploadedById = OtherUserId,
            UploadedAt = DateTimeOffset.UtcNow.AddMinutes(1),
        });

        var items = await harness.Service.ListForIssueAsync(issue.Id);

        Assert.Collection(
            items,
            mine => Assert.True(mine.CanDelete),
            theirs => Assert.False(theirs.CanDelete));
    }

    private static MemoryStream Content(string text) => new(Encoding.UTF8.GetBytes(text));

    private sealed class Harness
    {
        public Harness(long maxFileSizeBytes = 25 * 1024 * 1024)
        {
            UnitOfWork = new FakeUnitOfWork();
            Service = new AttachmentService(
                Attachments,
                Issues,
                Pages,
                Storage,
                UnitOfWork,
                new FakeCurrentUser(),
                Options.Create(new AttachmentOptions { MaxFileSizeBytes = maxFileSizeBytes }),
                NullLogger<AttachmentService>.Instance);
        }

        public FakeAttachmentRepository Attachments { get; } = new();

        public FakeIssueRepository Issues { get; } = new();

        public FakePageRepository Pages { get; } = new();

        public FakeFileStorage Storage { get; } = new();

        public FakeUnitOfWork UnitOfWork { get; }

        public AttachmentService Service { get; }

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
