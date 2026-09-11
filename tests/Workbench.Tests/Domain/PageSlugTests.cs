using Workbench.Domain;

namespace Workbench.Tests.Domain;

public class PageSlugTests
{
    [Theory]
    [InlineData("배포 절차", "배포-절차")]
    [InlineData("  Deploy   Runbook  ", "deploy-runbook")]
    [InlineData("AD 계정 / 초기화", "ad-계정-초기화")]
    [InlineData("v1.2.0 릴리스", "v1-2-0-릴리스")]
    [InlineData("---중간---", "중간")]
    [InlineData("", "")]
    [InlineData("!!!", "")]
    public void FromTitle_MakesAUrlFragment(string title, string expected)
    {
        Assert.Equal(expected, PageSlug.FromTitle(title));
    }

    [Fact]
    public void FromTitle_KeepsHangulInsteadOfTransliterating()
    {
        // 음역하면 사람이 URL 을 보고 문서를 알아볼 수 없다.
        Assert.Equal("통합인증-연동", PageSlug.FromTitle("통합인증 연동"));
    }

    [Fact]
    public void MakeUnique_ReturnsTheBaseWhenFree()
    {
        Assert.Equal("배포-절차", PageSlug.MakeUnique("배포 절차", _ => false, Guid.NewGuid()));
    }

    [Fact]
    public void MakeUnique_StepsAsideWhenTaken()
    {
        var taken = new HashSet<string> { "배포-절차", "배포-절차-2" };

        Assert.Equal("배포-절차-3", PageSlug.MakeUnique("배포 절차", taken.Contains, Guid.NewGuid()));
    }

    [Fact]
    public void MakeUnique_FallsBackWhenTheTitleLeavesNothing()
    {
        // 제목이 전부 기호면 슬러그가 비고, 빈 슬러그는 되돌릴 수 없는 링크가 된다.
        var slug = PageSlug.MakeUnique("!!!", _ => false, Guid.NewGuid());

        Assert.StartsWith(PageSlug.FallbackPrefix, slug);
        Assert.True(slug.Length > PageSlug.FallbackPrefix.Length);
    }
}
