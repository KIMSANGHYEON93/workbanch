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
    public void EveryStatus_HasABadgeClass()
    {
        foreach (var status in Enum.GetValues<IssueStatus>())
        {
            Assert.StartsWith("text-bg-", IssueDisplay.BadgeClass(status));
        }
    }

    [Fact]
    public void EveryPriority_HasABadgeClass()
    {
        foreach (var priority in Enum.GetValues<IssuePriority>())
        {
            Assert.StartsWith("text-bg-", IssueDisplay.BadgeClass(priority));
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
