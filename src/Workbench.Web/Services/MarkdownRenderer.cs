using Markdig;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.AspNetCore.Components;

namespace Workbench.Web.Services;

/// <summary>
/// 마크다운 → HTML 변환의 단일 출처.
/// 이슈 설명·페이지 본문은 <b>쓰는 사람과 읽는 사람이 다르다</b> — 내부 도구라도 저장형 XSS 가
/// 성립하므로, 두 가지 표면을 모두 막는다.
/// </summary>
public class MarkdownRenderer
{
    /// <summary>
    /// 링크·이미지에 허용하는 스킴. 여기 없는 스킴은 <c>javascript:</c>·<c>data:</c> 처럼
    /// 클릭 한 번으로 코드가 실행되는 통로가 된다. 스킴이 없는 상대 경로는 허용한다.
    /// </summary>
    private static readonly string[] AllowedSchemes = ["http", "https", "mailto"];

    /// <summary>차단된 링크가 이동하는 곳. 링크를 지우면 본문 뜻이 바뀌므로 무해화만 한다.</summary>
    private const string BlockedUrl = "#";

    /// <summary>
    /// ★ <c>DisableHtml()</c> 이 첫 번째 방어선이다.
    /// 마크다운은 원시 HTML 을 그대로 통과시키는 것이 표준 동작이라, 끄지 않으면 본문에 적힌
    /// <c>&lt;script&gt;</c> 가 읽는 사람 브라우저에서 실행된다.
    /// </summary>
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAutoLinks()
        .UsePipeTables()
        .UseTaskLists()
        .UseEmphasisExtras()
        .UseAutoIdentifiers()
        .DisableHtml()
        .Build();

    public MarkupString ToHtml(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return default;
        }

        var document = Markdown.Parse(markdown, Pipeline);
        NeutralizeUnsafeLinks(document);

        var output = new StringWriter();
        var renderer = new HtmlRenderer(output);
        Pipeline.Setup(renderer);
        renderer.Render(document);

        return new MarkupString(output.ToString());
    }

    /// <summary>
    /// 두 번째 방어선. <c>DisableHtml()</c> 은 태그를 막을 뿐 링크 <c>href</c> 는 건드리지 않으므로,
    /// 렌더링 전에 문서 트리에서 위험한 URL 을 직접 바꾼다(HTML 문자열을 정규식으로 다루지 않는다).
    /// </summary>
    private static void NeutralizeUnsafeLinks(MarkdownDocument document)
    {
        foreach (var link in document.Descendants<LinkInline>())
        {
            if (!IsSafeUrl(link.Url))
            {
                link.Url = BlockedUrl;
            }
        }

        foreach (var autolink in document.Descendants<AutolinkInline>())
        {
            if (!IsSafeUrl(autolink.Url))
            {
                autolink.Url = BlockedUrl;
            }
        }
    }

    internal static bool IsSafeUrl(string? url)
    {
        var normalized = Normalize(url);

        if (normalized.Length == 0)
        {
            return true;
        }

        var colonIndex = normalized.IndexOf(':');
        if (colonIndex < 0)
        {
            // 스킴이 없으면 상대 경로 또는 앵커다.
            return true;
        }

        // "/path:with:colon" 처럼 경로 구분자가 먼저 오면 스킴이 아니다.
        var separatorIndex = normalized.AsSpan(0, colonIndex).IndexOfAny('/', '?', '#');
        if (separatorIndex >= 0)
        {
            return true;
        }

        return AllowedSchemes.Contains(normalized[..colonIndex]);
    }

    /// <summary>
    /// 앞뒤 공백을 없애고 소문자로 맞춘다.
    /// ⚠ 이것은 회피 방어가 아니다 — 판정이 <b>허용목록</b>이라 <c>"java\tscript"</c> 처럼
    /// 망가진 스킴은 목록에 없어서 그냥 거부된다. 정규화가 실제로 하는 일은 반대쪽이다:
    /// <c>" https://ok"</c>·<c>"HTTPS://ok"</c> 같은 멀쩡한 링크를 잘못 막지 않게 하는 것.
    /// (뮤테이션 실측: 이 정규화를 없애도 위험한 URL 은 여전히 전부 막힌다.)
    /// </summary>
    private static string Normalize(string? url) =>
        (url ?? string.Empty).Trim().ToLowerInvariant();
}
