namespace Workbench.Application.DTOs;

public enum SearchHitKind
{
    Issue = 0,
    Page = 1,
    Project = 2,
}

/// <summary>검색 결과 한 줄. 화면이 종류별로 다른 링크를 만들지 않도록 <paramref name="Url"/> 을 함께 준다.</summary>
public sealed record SearchHit(SearchHitKind Kind, string Title, string? Subtitle, string Url);

/// <summary>
/// <paramref name="Truncated"/> 는 상한에 걸려 잘렸다는 뜻이다 — 화면이 "이게 전부" 라고
/// 잘못 말하지 않도록 결과와 함께 전달한다.
/// </summary>
public sealed record SearchResultGroup(SearchHitKind Kind, IReadOnlyList<SearchHit> Hits, bool Truncated);

public sealed record SearchResults(string Query, IReadOnlyList<SearchResultGroup> Groups)
{
    public static readonly SearchResults Empty = new(string.Empty, []);

    public int TotalHits => Groups.Sum(g => g.Hits.Count);

    public bool AnyTruncated => Groups.Any(g => g.Truncated);
}
