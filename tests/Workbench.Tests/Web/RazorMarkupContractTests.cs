using System.Text.RegularExpressions;
using Workbench.Domain.Enums;
using Workbench.Web.Services;

namespace Workbench.Tests.Web;

/// <summary>
/// 컴파일도 되고 테스트도 통과하는데 화면에서는 틀리는 종류의 결함을 잡는다.
/// 두 건 다 실제로 배포된 적이 있다 — 앱을 띄워 보기 전에는 아무도 몰랐다.
/// </summary>
public class RazorMarkupContractTests
{
    /// <summary>
    /// <c>Markdown="_issue.DescriptionMarkdown"</c> 처럼 <c>@</c> 를 빠뜨리면 Razor 는 그것을
    /// <b>리터럴 문자열</b>로 넘긴다. 파라미터 타입이 <c>string?</c> 이라 컴파일 오류도 없고,
    /// 화면에는 식의 코드가 그대로 찍힌다. 이 저장소에서 마크다운 렌더 4지점이 전부 그랬다.
    /// </summary>
    [Fact]
    public void Markdown_parameters_are_expressions_not_literal_strings()
    {
        var offenders = new List<string>();
        var usages = 0;

        foreach (var file in RepositoryFiles.RazorFiles())
        {
            foreach (Match match in Regex.Matches(File.ReadAllText(file), "Markdown=\"([^\"]*)\""))
            {
                usages++;

                if (!match.Groups[1].Value.StartsWith('@'))
                {
                    offenders.Add(
                        $"{Path.GetRelativePath(RepositoryFiles.Root, file)}: {match.Value}");
                }
            }
        }

        // 탐지기가 죽어서 조용히 통과하는 것을 막는다 — 실제로 쓰이는 지점이 있어야 한다.
        Assert.True(usages >= 4, $"MarkdownView 사용처를 {usages} 개만 찾았습니다. 스캐너가 망가졌습니다.");
        Assert.Empty(offenders);
    }

    /// <summary>
    /// 번들된 Bootstrap 은 5.1 이라 <c>text-bg-*</c> 유틸리티가 없다. 그 클래스를 쓰면
    /// <c>.badge</c> 의 <c>color:#fff</c> 만 남아 <b>배경 없는 흰 글자</b>가 되고, 배지가
    /// 화면에서 사라진다 — 상태·유형·우선순위 배지와 보드 컬럼 머리글이 전부 그랬다.
    /// </summary>
    [Theory]
    [MemberData(nameof(BadgeClasses))]
    public void Badge_classes_exist_in_the_bundled_stylesheet(string cssClass)
    {
        var css = File.ReadAllText(
            RepositoryFiles.Path("src", "Workbench.Web", "wwwroot", "bootstrap", "bootstrap.min.css"));

        Assert.Matches(new Regex($@"\.{Regex.Escape(cssClass)}[\s,{{:]"), css);
    }

    /// <summary>
    /// 배지 클래스는 <see cref="IssueDisplay"/> 말고 마크업에도 직접 박혀 있다. 번들 CSS 에 없는
    /// 클래스를 쓰면 그 배지는 <b>배경 없는 흰 글자</b>가 되어 화면에서 사라지므로, 실제로 쓰인
    /// 토큰이 스타일시트에 실재하는지 확인한다 — 이 저장소에서 9지점이 그랬다.
    /// </summary>
    [Fact]
    public void Badge_classes_written_in_markup_exist_in_the_stylesheet()
    {
        var css = File.ReadAllText(
            RepositoryFiles.Path("src", "Workbench.Web", "wwwroot", "bootstrap", "bootstrap.min.css"))
            + File.ReadAllText(RepositoryFiles.Path("src", "Workbench.Web", "wwwroot", "app.css"));

        var offenders = new List<string>();
        var badges = 0;

        foreach (var file in RepositoryFiles.RazorFiles())
        {
            foreach (Match match in Regex.Matches(File.ReadAllText(file), "class=\"badge ([^\"]*)\""))
            {
                badges++;

                foreach (var token in match.Groups[1].Value
                             .Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    // Razor 식은 런타임에 정해지므로 여기서 판정하지 않는다.
                    if (token.StartsWith('@'))
                    {
                        continue;
                    }

                    if (!Regex.IsMatch(css, $@"\.{Regex.Escape(token)}[\s,{{:]"))
                    {
                        offenders.Add(
                            $"{Path.GetRelativePath(RepositoryFiles.Root, file)}: .{token}");
                    }
                }
            }
        }

        Assert.True(badges >= 5, $"마크업의 badge 사용처를 {badges} 개만 찾았습니다. 스캐너가 망가졌습니다.");
        Assert.Empty(offenders);
    }

    public static TheoryData<string> BadgeClasses()
    {
        var data = new TheoryData<string>();

        var classes = Enum.GetValues<IssueStatus>().Select(IssueDisplay.BadgeClass)
            .Concat(Enum.GetValues<IssuePriority>().Select(IssueDisplay.BadgeClass))
            .SelectMany(value => value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Distinct();

        foreach (var cssClass in classes)
        {
            data.Add(cssClass);
        }

        return data;
    }
}
