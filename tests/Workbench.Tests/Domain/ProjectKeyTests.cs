using Workbench.Domain;

namespace Workbench.Tests.Domain;

public class ProjectKeyTests
{
    [Theory]
    [InlineData("dev", "DEV")]
    [InlineData("  Deploy ", "DEPLOY")]
    [InlineData(null, "")]
    public void Normalize_TrimsAndUppercases(string? input, string expected)
    {
        Assert.Equal(expected, ProjectKey.Normalize(input));
    }

    [Theory]
    [InlineData("DEV")]
    [InlineData("dev")]
    [InlineData("DEPLOY")]
    [InlineData("A1")]
    [InlineData("ABCDEFGHIJ")]
    public void IsValid_AcceptsUppercaseAlphanumericKeys(string key)
    {
        Assert.True(ProjectKey.IsValid(key));
    }

    [Theory]
    [InlineData("A")]
    [InlineData("1DEV")]
    [InlineData("DE-V")]
    [InlineData("DE V")]
    [InlineData("ABCDEFGHIJK")]
    [InlineData("개발")]
    [InlineData("")]
    [InlineData(null)]
    public void IsValid_RejectsEverythingElse(string? key)
    {
        Assert.False(ProjectKey.IsValid(key));
    }
}
