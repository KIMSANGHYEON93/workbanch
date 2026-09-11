using Workbench.Application.DTOs;
using Workbench.Application.Interfaces;
using Workbench.Domain.Entities;
using Workbench.Domain.Interfaces;

namespace Workbench.Application.Services;

public class SearchService : ISearchService
{
    private readonly IIssueRepository _issues;
    private readonly IPageRepository _pages;
    private readonly IProjectRepository _projects;

    public SearchService(
        IIssueRepository issues,
        IPageRepository pages,
        IProjectRepository projects)
    {
        _issues = issues;
        _pages = pages;
        _projects = projects;
    }

    public async Task<SearchResults> SearchAsync(
        string? query,
        CancellationToken cancellationToken = default)
    {
        var text = (query ?? string.Empty).Trim();

        // 빈 검색어가 전체를 쏟아내면 안 된다.
        if (text.Length < SearchLimits.MinQueryLength)
        {
            return SearchResults.Empty with { Query = text };
        }

        // 검색은 닫힌 이슈도 찾는다 — 끝낸 일을 다시 찾는 것이 검색의 주된 쓰임이다.
        var issues = await _issues.ListAsync(
            new IssueQuery(SearchText: text, IncludeClosed: true), cancellationToken);
        var pages = await _pages.SearchAsync(text, cancellationToken);
        var projects = await _projects.SearchAsync(text, cancellationToken);

        return new SearchResults(
            text,
            [
                Group(SearchHitKind.Issue, issues, ToHit),
                Group(SearchHitKind.Page, pages, ToHit),
                Group(SearchHitKind.Project, projects, ToHit),
            ]);
    }

    private static SearchResultGroup Group<TEntity>(
        SearchHitKind kind,
        IReadOnlyList<TEntity> matches,
        Func<TEntity, SearchHit> toHit) =>
        new(kind, [.. matches.Take(SearchLimits.MaxHitsPerKind).Select(toHit)], matches.Count > SearchLimits.MaxHitsPerKind);

    private static SearchHit ToHit(Issue issue) =>
        new(
            SearchHitKind.Issue,
            $"{issue.Key} · {issue.Title}",
            issue.Project?.Key,
            $"/issues/{issue.Key}");

    private static SearchHit ToHit(Page page) =>
        new(SearchHitKind.Page, page.Title, page.Project?.Key, $"/pages/{page.Slug}");

    private static SearchHit ToHit(Project project) =>
        new(SearchHitKind.Project, $"{project.Key} · {project.Name}", project.Description, "/projects");
}
