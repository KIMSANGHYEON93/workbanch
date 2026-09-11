using Workbench.Web.Services;

namespace Workbench.Tests.Web;

public class MarkdownRendererTests
{
    private readonly MarkdownRenderer _renderer = new();

    [Fact]
    public void RendersCommonMarkdown()
    {
        var html = Render("""
            # 제목

            **굵게** 와 *기울임*.

            - 항목 1
            - 항목 2

            ```csharp
            var x = 1;
            ```
            """);

        Assert.Contains("<h1", html);
        Assert.Contains("<strong>굵게</strong>", html);
        Assert.Contains("<em>기울임</em>", html);
        Assert.Contains("<li>항목 1</li>", html);
        Assert.Contains("<code", html);
    }

    [Fact]
    public void RendersTablesAndTaskLists()
    {
        var html = Render("""
            | 환경 | 버전 |
            |---|---|
            | PRD | 1.2.0 |

            - [x] 완료한 일
            - [ ] 남은 일
            """);

        Assert.Contains("<table", html);
        Assert.Contains("<td>PRD</td>", html);
        Assert.Contains("type=\"checkbox\"", html);
    }

    [Fact]
    public void EmptyInput_RendersNothing()
    {
        Assert.Equal(string.Empty, Render(null));
        Assert.Equal(string.Empty, Render("   "));
    }

    // ── 저장형 XSS 방어선 1: 원시 HTML ────────────────────────────────

    [Theory]
    [InlineData("<script>alert(1)</script>", "<script")]
    [InlineData("<img src=x onerror=alert(1)>", "<img")]
    [InlineData("<iframe src=\"https://evil.example.com\"></iframe>", "<iframe")]
    [InlineData("<svg/onload=alert(1)>", "<svg")]
    // 살아 있는 속성이라면 진짜 따옴표가 붙는다. 이스케이프된 본문에는 &quot; 가 들어가므로
    // 이 리터럴이 태그로 살아남았는지를 정확히 가른다.
    [InlineData("<a href=\"https://x.example.com\" onclick=\"alert(1)\">링크</a>", "onclick=\"")]
    public void RawHtml_IsEscapedNotEmitted(string markdown, string forbiddenLiteral)
    {
        var html = Render(markdown);

        Assert.DoesNotContain(forbiddenLiteral, html, StringComparison.OrdinalIgnoreCase);

        // 탐지기 비공허성: "아무것도 렌더되지 않아서" 통과하는 것이 아니라,
        // 실제로 렌더됐고 사용자의 '<' 가 이스케이프됐다는 뜻이다.
        Assert.Contains("&lt;", html);
    }

    [Fact]
    public void RawHtml_IsShownAsText()
    {
        Assert.Contains("&lt;script&gt;", Render("<script>alert(1)</script>"));
    }

    [Fact]
    public void EveryAngleBracketFromTheAuthor_IsEscaped()
    {
        // 앞의 계약은 알려진 태그 이름만 본다. 이 계약은 형태와 무관하게
        // "작성자가 넣은 '<' 는 하나도 태그가 되지 않는다" 를 센다.
        const string payload = "<script>x</script> <b>y</b> <unknown-tag attr=1>";

        var html = Render(payload);

        Assert.Equal(payload.Count(c => c == '<'), CountOccurrences(html, "&lt;"));
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = haystack.IndexOf(needle, StringComparison.Ordinal);

        while (index >= 0)
        {
            count++;
            index = haystack.IndexOf(needle, index + needle.Length, StringComparison.Ordinal);
        }

        return count;
    }

    // ── 저장형 XSS 방어선 2: 링크 스킴 ────────────────────────────────
    // DisableHtml() 은 태그만 막는다. 마크다운 문법으로 만든 링크의 href 는 그대로 나가므로
    // 문서 트리에서 따로 무해화해야 한다.

    [Theory]
    [InlineData("[클릭](javascript:alert(1))")]
    [InlineData("[클릭](JaVaScRiPt:alert(1))")]
    [InlineData("[클릭](java\tscript:alert(1))")]
    [InlineData("[클릭](data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==)")]
    [InlineData("[클릭](vbscript:msgbox)")]
    [InlineData("![이미지](javascript:alert(1))")]
    public void UnsafeLinkSchemes_AreNeutralized(string markdown)
    {
        var html = Render(markdown);

        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("vbscript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data:text/html", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("[문서](https://intranet.example.com/doc)", "https://intranet.example.com/doc")]
    [InlineData("[문서](http://intranet.example.com)", "http://intranet.example.com")]
    [InlineData("[메일](mailto:ops@example.com)", "mailto:ops@example.com")]
    [InlineData("[이슈](/issues/DEV-1)", "/issues/DEV-1")]
    [InlineData("[앵커](#section)", "#section")]
    public void SafeLinks_SurviveUntouched(string markdown, string expectedUrl)
    {
        Assert.Contains($"href=\"{expectedUrl}\"", Render(markdown));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("https://example.com", true)]
    [InlineData("mailto:a@b.com", true)]
    [InlineData("/relative/path", true)]
    [InlineData("#anchor", true)]
    // 앞뒤 공백·대문자가 섞인 멀쩡한 링크를 잘못 막으면 본문의 링크가 조용히 죽는다.
    [InlineData("  https://example.com  ", true)]
    [InlineData("HTTPS://example.com", true)]
    [InlineData("./a:b", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("  JAVASCRIPT:alert(1)", false)]
    [InlineData("data:text/html,<script>", false)]
    [InlineData("file:///etc/passwd", false)]
    public void IsSafeUrl_ClassifiesSchemes(string? url, bool expected)
    {
        Assert.Equal(expected, MarkdownRenderer.IsSafeUrl(url));
    }

    private string Render(string? markdown) => _renderer.ToHtml(markdown).Value ?? string.Empty;
}
