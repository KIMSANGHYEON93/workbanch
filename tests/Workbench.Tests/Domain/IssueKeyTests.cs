using Workbench.Domain.Entities;

namespace Workbench.Tests.Domain;

public class IssueKeyTests
{
    [Theory]
    [InlineData("DEV", 1, "DEV-1")]
    [InlineData("DEPLOY", 123, "DEPLOY-123")]
    public void FormatKey_JoinsProjectKeyAndNumber(string projectKey, int number, string expected)
    {
        Assert.Equal(expected, Issue.FormatKey(projectKey, number));
    }

    [Fact]
    public void FormatKey_RejectsBlankProjectKey()
    {
        Assert.Throws<ArgumentException>(() => Issue.FormatKey("  ", 1));
    }

    [Fact]
    public void FormatKey_RejectsNonPositiveNumber()
    {
        // 0 번은 발급되지 않는다 — LastIssueNumber 가 0 에서 시작해 증가한 뒤 사용되기 때문.
        Assert.Throws<ArgumentOutOfRangeException>(() => Issue.FormatKey("DEV", 0));
    }

    [Fact]
    public void FormatKey_IsCultureInvariant()
    {
        // 숫자 서식이 로캘(자릿수 구분자)에 흔들리면 키가 조용히 달라진다.
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.Equal("DEV-123456", Issue.FormatKey("DEV", 123456));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }
}
