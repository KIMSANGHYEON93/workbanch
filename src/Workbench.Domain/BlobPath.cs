namespace Workbench.Domain;

/// <summary>
/// 첨부 파일의 저장 경로 생성. <b>사용자가 준 파일명을 경로에 쓰지 않는다</b> —
/// <c>../../web.config</c> 같은 이름이 그대로 경로가 되면 저장소 밖으로 나간다(경로 이탈).
/// 원본 파일명은 DB 의 <c>Attachments.FileName</c> 에만 남고, 내려줄 때 그 이름을 쓴다.
/// </summary>
public static class BlobPath
{
    /// <summary>확장자에 허용하는 최대 길이. 이름 전체가 확장자인 입력을 막는다.</summary>
    private const int MaxExtensionLength = 16;

    public static string Create(Guid attachmentId, string? originalFileName) =>
        $"{attachmentId:N}{SafeExtension(originalFileName)}";

    /// <summary>
    /// 확장자만 뽑아 소문자로 남긴다. 영숫자가 아닌 글자가 하나라도 있으면 통째로 버린다 —
    /// 확장자는 편의(브라우저 힌트)일 뿐이라 애매하면 없는 편이 안전하다.
    /// </summary>
    internal static string SafeExtension(string? fileName)
    {
        var name = fileName ?? string.Empty;
        var dotIndex = name.LastIndexOf('.');

        if (dotIndex < 0 || dotIndex == name.Length - 1)
        {
            return string.Empty;
        }

        var extension = name[(dotIndex + 1)..];

        if (extension.Length > MaxExtensionLength || !extension.All(char.IsLetterOrDigit))
        {
            return string.Empty;
        }

        return $".{extension.ToLowerInvariant()}";
    }
}
