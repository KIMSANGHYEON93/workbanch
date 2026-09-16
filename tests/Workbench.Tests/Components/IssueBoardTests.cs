using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Workbench.Application.Interfaces;
using Workbench.Application.Services;
using Workbench.Domain.Entities;
using Workbench.Domain.Enums;
using Workbench.Tests.Fakes;
using Workbench.Web.Components.Pages.Issues;
using Workbench.Web.Services;

namespace Workbench.Tests.Components;

/// <summary>
/// 화면을 실제로 그려 보는 테스트. 여기까지 오기 전에는 "데이터가 있을 때 표·보드가 제대로
/// 그려지는가" 를 사람이 눈으로만 확인할 수 있었다 — 이 환경에는 DB 가 없어 실기동으로는
/// 오류 분기밖에 태울 수 없기 때문이다.
/// 서비스는 가짜가 아니라 <b>실제 구현</b>을 메모리 저장소 위에 올려 쓴다.
/// </summary>
public class IssueBoardTests : BunitContext
{
    private readonly FakeIssueRepository _issues = new();
    private readonly FakeProjectRepository _projects = new();
    private readonly FakeAppUserRepository _users = new();
    private readonly Project _project;

    public IssueBoardTests()
    {
        _project = _projects.Seed("DEV", "개발");

        var unitOfWork = new FakeUnitOfWork();
        var currentUser = new FakeCurrentUser();

        var members = new FakeProjectMemberRepository();
        var access = new ProjectAccessService(members, _projects, _users, unitOfWork, currentUser);

        Services.AddSingleton<IProjectService>(
            new ProjectService(_projects, members, access, unitOfWork, currentUser));
        Services.AddSingleton<IIssueService>(new IssueService(
            _issues, _projects, _users, access, unitOfWork, currentUser, NullLogger<IssueService>.Instance));
    }

    [Fact]
    public void Board_RendersOneColumnPerStatus()
    {
        var board = Render<IssueBoard>();

        Assert.Equal(BoardLayout.Columns.Count, board.FindAll(".board-column").Count);
    }

    [Fact]
    public void EveryColumn_ShowsItsKoreanLabel()
    {
        var board = Render<IssueBoard>();
        var markup = board.Markup;

        foreach (var status in BoardLayout.Columns)
        {
            Assert.Contains(IssueDisplay.Label(status), markup);
        }
    }

    [Fact]
    public void Cards_LandInTheColumnMatchingTheirStatus()
    {
        SeedIssue(1, "진행 중인 일", IssueStatus.InProgress);
        SeedIssue(2, "완료한 일", IssueStatus.Done);

        var board = Render<IssueBoard>();
        var columns = board.FindAll(".board-column");

        var inProgress = columns[BoardLayout.IndexOf(IssueStatus.InProgress)];
        var done = columns[BoardLayout.IndexOf(IssueStatus.Done)];

        Assert.Contains("진행 중인 일", inProgress.InnerHtml);
        Assert.Contains("DEV-1", inProgress.InnerHtml);
        Assert.Contains("완료한 일", done.InnerHtml);
        Assert.DoesNotContain("완료한 일", inProgress.InnerHtml);
    }

    [Fact]
    public void ClosedIssues_AreVisibleOnTheBoard()
    {
        // 목록은 완료를 감추지만 보드에는 완료 열이 있다 — 감추면 그 열이 영원히 비어 보인다.
        SeedIssue(1, "완료한 일", IssueStatus.Done);

        var board = Render<IssueBoard>();

        Assert.Contains("완료한 일", board.Markup);
    }

    [Fact]
    public void EmptyColumns_SaySo()
    {
        var board = Render<IssueBoard>();

        Assert.Equal(BoardLayout.Columns.Count, board.FindAll(".board-column").Count(c => c.InnerHtml.Contains("비어 있음")));
    }

    [Fact]
    public void MoveButtons_AreDisabledAtTheEnds()
    {
        SeedIssue(1, "첫 열", BoardLayout.Columns[0]);
        SeedIssue(2, "마지막 열", BoardLayout.Columns[^1]);

        var board = Render<IssueBoard>();
        var cards = board.FindAll(".board-card");

        var firstColumnButtons = cards[0].QuerySelectorAll(".board-card-actions button");
        Assert.True(firstColumnButtons[0].HasAttribute("disabled"));
        Assert.False(firstColumnButtons[1].HasAttribute("disabled"));

        var lastColumnButtons = cards[^1].QuerySelectorAll(".board-card-actions button");
        Assert.False(lastColumnButtons[0].HasAttribute("disabled"));
        Assert.True(lastColumnButtons[1].HasAttribute("disabled"));
    }

    [Fact]
    public void ClickingRight_MovesTheIssueToTheNextColumn()
    {
        var issue = SeedIssue(1, "옮길 일", IssueStatus.Todo);

        var board = Render<IssueBoard>();
        board.Find(".board-card .board-card-actions button:last-child").Click();

        Assert.Equal(IssueStatus.InProgress, issue.Status);

        var columns = board.FindAll(".board-column");
        Assert.Contains(
            "옮길 일",
            columns[BoardLayout.IndexOf(IssueStatus.InProgress)].InnerHtml);
    }

    [Fact]
    public void DroppingACard_MovesItToThatColumn()
    {
        // 끌어놓기는 JS interop 없이 Blazor 의 드래그 이벤트만으로 동작한다.
        var issue = SeedIssue(1, "끌어 옮길 일", IssueStatus.Backlog);

        var board = Render<IssueBoard>();
        board.Find(".board-card").DragStart();
        board.FindAll(".board-column")[BoardLayout.IndexOf(IssueStatus.InReview)].Drop();

        Assert.Equal(IssueStatus.InReview, issue.Status);
    }

    private Issue SeedIssue(int number, string title, IssueStatus status) =>
        _issues.Seed(new Issue
        {
            ProjectId = _project.Id,
            Project = _project,
            Number = number,
            Key = Issue.FormatKey(_project.Key, number),
            Title = title,
            Status = status,
            ReporterId = FakeCurrentUser.DefaultUserId,
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-number),
        });
}
