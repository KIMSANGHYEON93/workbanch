namespace Workbench.Application;

/// <summary>
/// 검색의 경계값. 서비스와 화면이 같은 값을 봐야 한다 — 화면이 다른 상한을 안내하면
/// 사용자는 "왜 안 찾아지지" 를 스스로 알아낼 수 없다.
/// </summary>
public static class SearchLimits
{
    /// <summary>
    /// 종류별 표시 상한. 넘으면 잘렸다는 사실을 화면에 알린다 — 조용히 자르면 사용자는
    /// 없는 것과 구분할 수 없다.
    /// </summary>
    public const int MaxHitsPerKind = 50;

    /// <summary>
    /// 이보다 짧은 검색어는 받지 않는다. 한 글자는 사실상 전수 조회라, 결과가 상한에서 잘려
    /// 아무 도움이 되지 않으면서 DB 만 훑는다.
    /// </summary>
    public const int MinQueryLength = 2;
}
