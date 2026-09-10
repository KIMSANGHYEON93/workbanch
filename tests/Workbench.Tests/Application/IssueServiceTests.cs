using Microsoft.Extensions.Logging.Abstractions;
using Workbench.Application.DTOs;
using Workbench.Application.Services;
using Workbench.Domain.Entities;
using Workbench.Domain.Enums;
using Workbench.Tests.Fakes;

namespace Workbench.Tests.Application;

public class IssueServiceTests
{
    [Fact]
    public async Task Create_AllocatesSequentialKeys()
    {
        var harness = new Harness();
        var project = harness.Projects.Seed("DEV", "개발");

        var first = await harness.Service.CreateAsync(Model(project.Id, "첫 이슈"));
        var second = await harness.Service.CreateAsync(Model(project.Id, "둘째 이슈"));

        Assert.Equal("DEV-1", first.Value);
        Assert.Equal("DEV-2", second.Value);
        Assert.Equal(2, project.LastIssueNumber);
    }

    [Fact]
    public async Task Create_RecordsTheSignedInUserAsReporter()
    {
        var harness = new Harness();
        var project = harness.Projects.Seed("DEV", "개발");

        await harness.Service.CreateAsync(Model(project.Id, "이슈"));

        Assert.Equal(FakeCurrentUser.DefaultUserId, Assert.Single(harness.Issues.Issues).ReporterId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_RejectsBlankTitle(string title)
    {
        var harness = new Harness();
        var project = harness.Projects.Seed("DEV", "개발");

        var result = await harness.Service.CreateAsync(Model(project.Id, title));

        Assert.False(result.Succeeded);
        Assert.Empty(harness.Issues.Issues);
        Assert.Equal(0, project.LastIssueNumber);
    }

    [Fact]
    public async Task Create_RejectsUnknownProject()
    {
        var harness = new Harness();

        var result = await harness.Service.CreateAsync(Model(Guid.NewGuid(), "이슈"));

        Assert.False(result.Succeeded);
        Assert.Empty(harness.Issues.Issues);
    }

    [Fact]
    public async Task Create_RejectsInactiveAssignee()
    {
        // 비활성 계정에 배정하면 그 이슈는 아무도 보지 않는 곳으로 사라진다.
        var harness = new Harness();
        var project = harness.Projects.Seed("DEV", "개발");
        var retired = harness.Users.Seed("퇴사자", "left@example.com", isActive: false);

        var model = Model(project.Id, "이슈");
        model.AssigneeId = retired.Id;

        var result = await harness.Service.CreateAsync(model);

        Assert.False(result.Succeeded);
        Assert.Empty(harness.Issues.Issues);
    }

    [Fact]
    public async Task Create_AcceptsAnActiveAssignee()
    {
        var harness = new Harness();
        var project = harness.Projects.Seed("DEV", "개발");
        var assignee = harness.Users.Seed("홍길동", "gildong@example.com");

        var model = Model(project.Id, "이슈");
        model.AssigneeId = assignee.Id;

        Assert.True((await harness.Service.CreateAsync(model)).Succeeded);
        Assert.Equal(assignee.Id, Assert.Single(harness.Issues.Issues).AssigneeId);
    }

    [Fact]
    public async Task Create_RetriesWhenTheNumberCollides()
    {
        // 두 사람이 동시에 만들면 UNIQUE(ProjectId, Number) 가 한쪽을 거절한다 — 다시 읽어 다음 번호를 집는다.
        var harness = new Harness();
        harness.UnitOfWork.FailFirstSaves = 1;
        var project = harness.Projects.Seed("DEV", "개발");

        var result = await harness.Service.CreateAsync(Model(project.Id, "이슈"));

        Assert.True(result.Succeeded);
        Assert.Equal(2, harness.UnitOfWork.SaveChangesCallCount);
        Assert.Equal(1, harness.UnitOfWork.DiscardCallCount);
    }

    [Fact]
    public async Task Create_SurfacesTheFailureWhenRetriesAreExhausted()
    {
        // 마지막 시도의 예외는 흡수하지 않는다 — 원인이 경합이 아니면 조용히 삼키는 것이 더 나쁘다.
        var harness = new Harness();
        harness.UnitOfWork.FailFirstSaves = 99;
        var project = harness.Projects.Seed("DEV", "개발");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Service.CreateAsync(Model(project.Id, "이슈")));
    }

    [Fact]
    public async Task Update_RejectsMovingTheIssueToAnotherProject()
    {
        // 키(DEV-12)의 접두어가 프로젝트에 묶여 있어, 옮기면 키가 어긋난다.
        var harness = new Harness();
        var dev = harness.Projects.Seed("DEV", "개발");
        var ops = harness.Projects.Seed("OPS", "운영");
        var issue = harness.SeedIssue(dev, 1, "이슈");

        var result = await harness.Service.UpdateAsync(issue.Id, Model(ops.Id, "이슈"));

        Assert.False(result.Succeeded);
        Assert.Equal(dev.Id, issue.ProjectId);
        Assert.Equal(0, harness.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task Update_TouchesUpdatedAt()
    {
        var harness = new Harness();
        var project = harness.Projects.Seed("DEV", "개발");
        var issue = harness.SeedIssue(project, 1, "이슈");
        var before = issue.UpdatedAt;

        var model = Model(project.Id, "제목 변경");
        var result = await harness.Service.UpdateAsync(issue.Id, model);

        Assert.True(result.Succeeded);
        Assert.Equal("제목 변경", issue.Title);
        Assert.True(issue.UpdatedAt > before);
    }

    [Fact]
    public async Task ChangeStatus_RejectsAnUndefinedStatus()
    {
        var harness = new Harness();
        var project = harness.Projects.Seed("DEV", "개발");
        var issue = harness.SeedIssue(project, 1, "이슈");

        var result = await harness.Service.ChangeStatusAsync(issue.Id, (IssueStatus)99);

        Assert.False(result.Succeeded);
        Assert.Equal(0, harness.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task ChangeStatus_ToTheSameStatusDoesNotTouchTheIssue()
    {
        // UpdatedAt 을 건드리면 목록 정렬이 이유 없이 흔들린다.
        var harness = new Harness();
        var project = harness.Projects.Seed("DEV", "개발");
        var issue = harness.SeedIssue(project, 1, "이슈");
        var before = issue.UpdatedAt;

        var result = await harness.Service.ChangeStatusAsync(issue.Id, issue.Status);

        Assert.True(result.Succeeded);
        Assert.Equal(before, issue.UpdatedAt);
        Assert.Equal(0, harness.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task ChangeStatus_MovesTheIssueAndSaves()
    {
        var harness = new Harness();
        var project = harness.Projects.Seed("DEV", "개발");
        var issue = harness.SeedIssue(project, 1, "이슈");

        var result = await harness.Service.ChangeStatusAsync(issue.Id, IssueStatus.InProgress);

        Assert.True(result.Succeeded);
        Assert.Equal(IssueStatus.InProgress, issue.Status);
        Assert.Equal(1, harness.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task List_HidesClosedIssuesUnlessAsked()
    {
        var harness = new Harness();
        var project = harness.Projects.Seed("DEV", "개발");
        harness.SeedIssue(project, 1, "열린 이슈");
        harness.SeedIssue(project, 2, "닫힌 이슈", IssueStatus.Done);

        var open = await harness.Service.ListAsync(new(ProjectId: project.Id));
        var all = await harness.Service.ListAsync(new(ProjectId: project.Id, IncludeClosed: true));

        Assert.Equal("열린 이슈", Assert.Single(open).Title);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task List_ShowsAClosedStatusWhenItIsExplicitlySelected()
    {
        // 상태를 콕 집어 골랐는데 "닫힘 숨김" 이 그것을 덮으면 필터가 빈손을 돌려준다.
        var harness = new Harness();
        var project = harness.Projects.Seed("DEV", "개발");
        harness.SeedIssue(project, 1, "닫힌 이슈", IssueStatus.Done);

        var result = await harness.Service.ListAsync(new(Status: IssueStatus.Done));

        Assert.Equal("닫힌 이슈", Assert.Single(result).Title);
    }

    [Fact]
    public async Task GetByKey_IsCaseInsensitive()
    {
        // URL 에 소문자로 들어와도 같은 이슈를 찾아야 한다.
        var harness = new Harness();
        var project = harness.Projects.Seed("DEV", "개발");
        harness.SeedIssue(project, 7, "이슈");

        Assert.NotNull(await harness.Service.GetByKeyAsync("dev-7"));
        Assert.NotNull(await harness.Service.GetByKeyAsync(" DEV-7 "));
    }

    [Fact]
    public async Task ListAssigneeOptions_ExcludesInactiveAccounts()
    {
        var harness = new Harness();
        harness.Users.Seed("홍길동", "gildong@example.com");
        harness.Users.Seed("퇴사자", "left@example.com", isActive: false);

        var options = await harness.Service.ListAssigneeOptionsAsync();

        Assert.Equal("홍길동", Assert.Single(options).DisplayName);
    }

    private static IssueEditModel Model(Guid projectId, string title) =>
        new() { ProjectId = projectId, Title = title };

    private sealed class Harness
    {
        public Harness()
        {
            UnitOfWork = new FakeUnitOfWork();
            Service = new IssueService(
                Issues,
                Projects,
                Users,
                UnitOfWork,
                new FakeCurrentUser(),
                NullLogger<IssueService>.Instance);
        }

        public FakeIssueRepository Issues { get; } = new();

        public FakeProjectRepository Projects { get; } = new();

        public FakeAppUserRepository Users { get; } = new();

        public FakeUnitOfWork UnitOfWork { get; }

        public IssueService Service { get; }

        public Issue SeedIssue(
            Project project,
            int number,
            string title,
            IssueStatus status = IssueStatus.Backlog)
        {
            project.LastIssueNumber = Math.Max(project.LastIssueNumber, number);

            return Issues.Seed(new Issue
            {
                ProjectId = project.Id,
                Project = project,
                Number = number,
                Key = Issue.FormatKey(project.Key, number),
                Title = title,
                Status = status,
                ReporterId = FakeCurrentUser.DefaultUserId,
                UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-number),
            });
        }
    }
}
