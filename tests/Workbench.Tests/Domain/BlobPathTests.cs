using Workbench.Domain;

namespace Workbench.Tests.Domain;

public class BlobPathTests
{
    private static readonly Guid AttachmentId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    [Theory]
    [InlineData("report.pdf", ".pdf")]
    [InlineData("REPORT.PDF", ".pdf")]
    [InlineData("archive.tar.gz", ".gz")]
    [InlineData("noextension", "")]
    [InlineData("trailingdot.", "")]
    [InlineData("weird.p h p", "")]
    [InlineData("spaced. pdf", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void SafeExtension_KeepsOnlyPlainAlphanumericExtensions(string? fileName, string expected)
    {
        Assert.Equal(expected, BlobPath.SafeExtension(fileName));
    }

    [Theory]
    [InlineData("../../web.config")]
    [InlineData("..\\..\\web.config")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\System32\\drivers\\etc\\hosts")]
    [InlineData("내부 문서.pdf")]
    public void Create_NeverEmbedsTheUsersFileName(string fileName)
    {
        // 사용자 파일명이 경로가 되면 저장소 밖으로 나간다(경로 이탈).
        // 원본 이름은 DB 의 FileName 컬럼에만 남고, 내려줄 때 그 이름을 쓴다.
        var path = BlobPath.Create(AttachmentId, fileName);

        Assert.DoesNotContain("..", path, StringComparison.Ordinal);
        Assert.DoesNotContain("/", path, StringComparison.Ordinal);
        Assert.DoesNotContain("\\", path, StringComparison.Ordinal);
        Assert.DoesNotContain(" ", path, StringComparison.Ordinal);
        Assert.StartsWith(AttachmentId.ToString("N"), path, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_KeepsTheExtensionAsABrowserHint()
    {
        Assert.Equal($"{AttachmentId:N}.png", BlobPath.Create(AttachmentId, "스크린샷.png"));
    }

    [Fact]
    public void Create_IsUniquePerAttachment()
    {
        // 같은 파일명을 두 번 올려도 서로 덮어쓰지 않는다.
        Assert.NotEqual(
            BlobPath.Create(Guid.NewGuid(), "report.pdf"),
            BlobPath.Create(Guid.NewGuid(), "report.pdf"));
    }
}
