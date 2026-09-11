using Workbench.Application;
using Workbench.Application.DTOs;
using Workbench.Application.Services;
using Workbench.Domain.Entities;
using Workbench.Domain.Enums;
using Workbench.Tests.Fakes;

namespace Workbench.Tests.Application;

public class SearchServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("배")]
    public async Task ShortOrEmptyQueries_ReturnNothing(string? query)
    {
        // 빈 검색어가 전체를 쏟아내면 안 된다. 한 글자도 사실상 전수 조회다.
        var harness = new Harness();
        harness.SeedIssue(1, "배포 실패");
        harness.Pages.Seed("배포 절차", "배포-절차");

        var results = await harness.Service.SearchAsync(query);

        Assert.Equal(0, results.TotalHits);
        Assert.False(results.AnyTruncated);
    }

    [Fact]
    public async Task Search_FindsIssuesPagesAndProjects()
    {
        var harness = new Harness();
        harness.SeedIssue(1, "배포 실패");
        harness.Pages.Seed("배포 절차", "배포-절차");
        harness.Projects.Seed("DEPLOY", "배포 관리");

        var results = await harness.Service.SearchAsync("배포");

        Assert.Equal(3, results.TotalHits);
        Assert.Equal(
            [SearchHitKind.Issue, SearchHitKind.Page, SearchHitKind.Project],
            results.Groups.Select(g => g.Kind));
    }

    [Fact]
    public async Task Search_FindsClosedIssues()
    {
        // 끝낸 일을 다시 찾는 것이 검색의 주된 쓰임이다 — 목록의 "완료 숨김" 을 물려받으면 안 된다.
        var harness = new Harness();
        harness.SeedIssue(1, "완료한 배포", IssueStatus.Done);
        harness.SeedIssue(2, "취소한 배포", IssueStatus.Cancelled);

        var issues = Assert.Single(
            (await harness.Service.SearchAsync("배포")).Groups,
            g => g.Kind == SearchHitKind.Issue);

        Assert.Equal(2, issues.Hits.Count);
    }

    [Fact]
    public async Task Search_MatchesIssueKeys()
    {
        var harness = new Harness();
        harness.SeedIssue(42, "무관한 제목");

        var issues = Assert.Single(
            (await harness.Service.SearchAsync("DEV-42")).Groups,
            g => g.Kind == SearchHitKind.Issue);

        Assert.Single(issues.Hits);
    }

    [Fact]
    public async Task Hits_CarryALinkTheScreenCanFollow()
    {
        var harness = new Harness();
        harness.SeedIssue(7, "이슈");
        harness.Pages.Seed("문서", "문서-슬러그");

        var results = await harness.Service.SearchAsync("문서");
        var pageHit = Assert.Single(results.Groups.Single(g => g.Kind == SearchHitKind.Page).Hits);

        Assert.Equal("/pages/문서-슬러그", pageHit.Url);
    }

    [Fact]
    public async Task TooManyMatches_AreCappedAndSaidSo()
    {
        // 조용히 자르면 사용자는 "없음" 과 구분할 수 없다.
        var harness = new Harness();
        for (var i = 1; i <= SearchLimits.MaxHitsPerKind + 5; i++)
        {
            harness.SeedIssue(i, $"배포 {i}");
        }

        var results = await harness.Service.SearchAsync("배포");
        var issues = results.Groups.Single(g => g.Kind == SearchHitKind.Issue);

        Assert.Equal(SearchLimits.MaxHitsPerKind, issues.Hits.Count);
        Assert.True(issues.Truncated);
        Assert.True(results.AnyTruncated);
    }

    [Fact]
    public async Task ExactlyAtTheCap_IsNotReportedAsTruncated()
    {
        var harness = new Harness();
        for (var i = 1; i <= SearchLimits.MaxHitsPerKind; i++)
        {
            harness.SeedIssue(i, $"배포 {i}");
        }

        var issues = (await harness.Service.SearchAsync("배포"))
            .Groups.Single(g => g.Kind == SearchHitKind.Issue);

        Assert.Equal(SearchLimits.MaxHitsPerKind, issues.Hits.Count);
        Assert.False(issues.Truncated);
    }

    [Fact]
    public async Task Query_IsTrimmedBeforeSearching()
    {
        var harness = new Harness();
        harness.Pages.Seed("배포 절차", "배포-절차");

        var results = await harness.Service.SearchAsync("  배포  ");

        Assert.Equal("배포", results.Query);
        Assert.Equal(1, results.TotalHits);
    }

    private sealed class Harness
    {
        private readonly Project _project;

        public Harness()
        {
            _project = Projects.Seed("DEV", "개발");
            Service = new SearchService(Issues, Pages, Projects);
        }

        public FakeIssueRepository Issues { get; } = new();

        public FakePageRepository Pages { get; } = new();

        public FakeProjectRepository Projects { get; } = new();

        public SearchService Service { get; }

        public Issue SeedIssue(int number, string title, IssueStatus status = IssueStatus.Backlog) =>
            Issues.Seed(new Issue
            {
                ProjectId = _project.Id,
                Project = _project,
                Number = number,
                Key = Issue.FormatKey(_project.Key, number),
                Title = title,
                Status = status,
                ReporterId = FakeCurrentUser.DefaultUserId,
            });
    }
}
