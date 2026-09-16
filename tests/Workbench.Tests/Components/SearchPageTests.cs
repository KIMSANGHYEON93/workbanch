using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using Workbench.Application;
using Workbench.Application.Interfaces;
using Workbench.Application.Services;
using Workbench.Domain.Entities;
using Workbench.Tests.Fakes;
using Workbench.Web.Components.Pages;

namespace Workbench.Tests.Components;

public class SearchPageTests : BunitContext
{
    private readonly FakeIssueRepository _issues = new();
    private readonly FakePageRepository _pages = new();
    private readonly FakeProjectRepository _projects = new();
    private readonly Project _project;

    public SearchPageTests()
    {
        _project = _projects.Seed("DEV", "개발");
        Services.AddSingleton<ISearchService>(new SearchService(_issues, _pages, _projects));
    }

    [Fact]
    public void WithoutAQuery_ItAsksForOne()
    {
        var page = Render<SearchPage>();

        Assert.Contains("검색어를 입력하세요", page.Markup);
        Assert.Contains($"{SearchLimits.MinQueryLength}자 이상", page.Markup);
    }

    [Fact]
    public void NoMatches_SaysSoRatherThanShowingAnEmptyList()
    {
        var page = RenderSearch("없는검색어");

        Assert.Contains("일치하는 항목이 없습니다", page.Markup);
    }

    [Fact]
    public void Matches_AreGroupedByKindWithFollowableLinks()
    {
        SeedIssue(1, "배포 실패");
        _pages.Seed("배포 절차", "배포-절차");
        _projects.Seed("DEPLOY", "배포 관리");

        var page = RenderSearch("배포");
        var markup = page.Markup;

        Assert.Contains("이슈", markup);
        Assert.Contains("페이지", markup);
        Assert.Contains("프로젝트", markup);
        Assert.Contains("href=\"/issues/DEV-1\"", markup);
        Assert.Contains("href=\"/pages/배포-절차\"", markup);
    }

    [Fact]
    public void TruncatedResults_TellTheUserTheyAreTruncated()
    {
        // 잘린 사실을 숨기면 사용자는 "이게 전부" 라고 믿는다.
        for (var i = 1; i <= SearchLimits.MaxHitsPerKind + 1; i++)
        {
            SeedIssue(i, $"배포 {i}");
        }

        var page = RenderSearch("배포");

        Assert.Contains($"종류별 {SearchLimits.MaxHitsPerKind} 건까지만", page.Markup);
        Assert.Equal(SearchLimits.MaxHitsPerKind, page.FindAll("li.list-group-item").Count);
    }

    /// <summary>
    /// 검색어는 쿼리 문자열로 들어온다 — 파라미터를 직접 넣는 대신 실제 경로대로 이동시킨다.
    /// </summary>
    private IRenderedComponent<SearchPage> RenderSearch(string query)
    {
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo(navigation.GetUriWithQueryParameter("q", query));

        return Render<SearchPage>();
    }

    private void SeedIssue(int number, string title) =>
        _issues.Seed(new Issue
        {
            ProjectId = _project.Id,
            Project = _project,
            Number = number,
            Key = Issue.FormatKey(_project.Key, number),
            Title = title,
            ReporterId = FakeCurrentUser.DefaultUserId,
        });
}
