using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Workbench.Application;
using Workbench.Application.DTOs;
using Workbench.Application.Services;
using Workbench.Infrastructure.BlobStorage;
using Workbench.Infrastructure.Data;
using Workbench.Infrastructure.Repositories;
using Workbench.Tests.Fakes;
using Workbench.Tests.Infrastructure;

namespace Workbench.Tests.Application;

/// <summary>
/// 서비스 → 실제 저장소 구현(LocalFileStorage) → 실제 DbContext 를 그대로 통과시킨다.
/// Blazor 업로드 컴포넌트만 빼면 운영 경로와 같은 조립이다.
/// ⚠ Azure Blob 구현은 이 환경에 접속 정보가 없어 <b>실행된 적이 없다</b>.
/// </summary>
public sealed class AttachmentServiceEndToEndTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly WorkbenchDbContext _dbContext;
    private readonly string _storageRoot =
        Path.Combine(Path.GetTempPath(), $"workbench-e2e-{Guid.NewGuid():N}");

    private readonly AttachmentService _attachments;
    private readonly IssueService _issues;
    private readonly ProjectService _projects;

    public AttachmentServiceEndToEndTests()
    {
        _dbContext = _database.Context;

        var currentUser = new FakeCurrentUser();
        var projectRepository = new ProjectRepository(_dbContext);
        var issueRepository = new IssueRepository(_dbContext);

        _projects = new ProjectService(projectRepository, _dbContext, currentUser);
        _issues = new IssueService(
            issueRepository,
            projectRepository,
            new AppUserRepository(_dbContext),
            _dbContext,
            currentUser,
            NullLogger<IssueService>.Instance);

        _attachments = new AttachmentService(
            new AttachmentRepository(_dbContext),
            issueRepository,
            new PageRepository(_dbContext),
            new LocalFileStorage(Options.Create(new FileStorageOptions { LocalRoot = _storageRoot })),
            _dbContext,
            currentUser,
            Options.Create(new AttachmentOptions()),
            NullLogger<AttachmentService>.Instance);
    }

    [Fact]
    public async Task UploadListDownloadDelete_RoundTripsThroughDatabaseAndDisk()
    {
        var issueId = await CreateIssueAsync();
        var bytes = Encoding.UTF8.GetBytes("배포 로그\n2026-09-10 OK");

        var uploaded = await _attachments.AttachToIssueAsync(
            issueId, new MemoryStream(bytes), "배포 로그.log", "text/plain", bytes.Length);

        Assert.True(uploaded.Succeeded);

        var listed = Assert.Single(await _attachments.ListForIssueAsync(issueId));
        Assert.Equal("배포 로그.log", listed.FileName);
        Assert.Equal(bytes.Length, listed.Size);
        Assert.True(listed.CanDelete);

        var content = await _attachments.OpenAsync(listed.Id);
        Assert.NotNull(content);

        using var buffer = new MemoryStream();
        await content.Content.CopyToAsync(buffer);
        await content.Content.DisposeAsync();

        Assert.Equal(bytes, buffer.ToArray());
        Assert.Equal("배포 로그.log", content.FileName);

        Assert.True((await _attachments.DeleteAsync(listed.Id)).Succeeded);
        Assert.Equal(0, await _dbContext.Attachments.CountAsync());
        Assert.Empty(Directory.GetFiles(_storageRoot));
    }

    [Fact]
    public async Task DeletingAnIssue_RemovesItsAttachmentRows()
    {
        // Issues → Attachments 는 CASCADE 다.
        var issueId = await CreateIssueAsync();
        await _attachments.AttachToIssueAsync(
            issueId, new MemoryStream([1, 2, 3]), "a.bin", "application/octet-stream", 3);

        Assert.True((await _issues.DeleteAsync(issueId)).Succeeded);
        Assert.Equal(0, await _dbContext.Attachments.CountAsync());

        // ⚠ 알려진 성질: 부모를 지우면 바이트는 저장소에 남는다(고아 blob).
        // 조용하고 무해하며, 정리는 별도 관리 작업의 몫이다.
        Assert.Single(Directory.GetFiles(_storageRoot));
    }

    public void Dispose()
    {
        _database.Dispose();

        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }

    private async Task<Guid> CreateIssueAsync()
    {
        var project = await _projects.CreateAsync(new ProjectEditModel { Key = "DEV", Name = "개발" });
        var created = await _issues.CreateAsync(new IssueEditModel
        {
            ProjectId = project.Value,
            Title = "이슈",
        });

        return (await _dbContext.Issues.SingleAsync(i => i.Key == created.Value)).Id;
    }
}
