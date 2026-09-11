using Workbench.Domain.Enums;
using Workbench.Web.Services;

namespace Workbench.Tests.Web;

/// <summary>
/// 새 상태·유형·우선순위를 추가하고 표기를 빠뜨리면 화면에 영문 enum 이름이 그대로 뜬다.
/// 그것은 조용히 배포되므로 계약으로 막는다.
/// </summary>
public class IssueDisplayTests
{
    [Fact]
    public void EveryStatus_HasItsOwnLabel() =>
        AssertLabelled(Enum.GetValues<IssueStatus>(), IssueDisplay.Label);

    [Fact]
    public void EveryType_HasItsOwnLabel() =>
        AssertLabelled(Enum.GetValues<IssueType>(), IssueDisplay.Label);

    [Fact]
    public void EveryPriority_HasItsOwnLabel() =>
        AssertLabelled(Enum.GetValues<IssuePriority>(), IssueDisplay.Label);

    [Fact]
    public void EveryStatus_HasABadgeClass() =>
        AssertBadged(Enum.GetValues<IssueStatus>(), IssueDisplay.BadgeClass);

    [Fact]
    public void EveryPriority_HasABadgeClass() =>
        AssertBadged(Enum.GetValues<IssuePriority>(), IssueDisplay.BadgeClass);

    /// <summary>
    /// ⚠ 여기서 <c>text-bg-</c> 접두어를 요구하면 안 된다 — 번들된 Bootstrap 5.1 에 없는
    /// 유틸리티라, 그 규약을 지킬수록 배지가 화면에서 사라진다(실제로 그렇게 배포됐다).
    /// 클래스가 실재하는지는 <see cref="RazorMarkupContractTests"/> 가 번들 CSS 를 직접 읽어 확인한다.
    /// </summary>
    private static void AssertBadged<TEnum>(TEnum[] values, Func<TEnum, string> badgeClass)
        where TEnum : struct, Enum
    {
        Assert.NotEmpty(values);

        foreach (var value in values)
        {
            var css = badgeClass(value);

            Assert.False(string.IsNullOrWhiteSpace(css));

            // 배경색이 없으면 .badge 의 흰 글자만 남아 아무것도 보이지 않는다.
            Assert.Contains(
                css.Split(' ', StringSplitOptions.RemoveEmptyEntries),
                token => token.StartsWith("bg-", StringComparison.Ordinal));
        }
    }

    private static void AssertLabelled<TEnum>(TEnum[] values, Func<TEnum, string> label)
        where TEnum : struct, Enum
    {
        Assert.NotEmpty(values);

        var labels = values.Select(label).ToList();

        // enum 이름이 그대로 나오면 표기를 빠뜨린 것이다.
        Assert.All(labels, l => Assert.False(string.IsNullOrWhiteSpace(l)));
        Assert.DoesNotContain(labels, l => Enum.TryParse<TEnum>(l, out _));

        // 두 값이 같은 낱말이면 화면에서 구분되지 않는다.
        Assert.Equal(labels.Count, labels.Distinct().Count());
    }
}
