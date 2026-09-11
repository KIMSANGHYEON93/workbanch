using System.Globalization;
using System.Text;

namespace Workbench.Domain;

/// <summary>
/// 페이지 슬러그 생성·정규화의 단일 출처.
/// 슬러그는 URL 조각이자 제목이 바뀌어도 링크를 살려 두기 위한 안정 식별자다.
/// </summary>
public static class PageSlug
{
    private const char Separator = '-';

    /// <summary>제목이 전부 기호라 남는 글자가 없을 때 쓰는 접두어.</summary>
    public const string FallbackPrefix = "page";

    /// <summary>
    /// 제목에서 슬러그를 만든다. <b>한글을 로마자로 바꾸지 않는다</b> — 사내 문서 제목은 대부분
    /// 한글이고, 음역하면 사람이 URL 을 보고 문서를 알아볼 수 없다.
    /// </summary>
    public static string FromTitle(string? title)
    {
        var builder = new StringBuilder();
        var lastWasSeparator = true;

        foreach (var character in (title ?? string.Empty).Trim())
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);

            if (char.IsLetterOrDigit(character) || category == UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator)
            {
                // 공백·기호가 잇달아도 구분자는 하나만 남긴다.
                builder.Append(Separator);
                lastWasSeparator = true;
            }
        }

        return builder.ToString().Trim(Separator);
    }

    public static string Normalize(string? slug) => FromTitle(slug);

    /// <summary>
    /// 이미 쓰이는 슬러그면 <c>-2</c>, <c>-3</c> … 을 붙여 비켜 간다.
    /// 슬러그가 비면(제목이 전부 기호) 되돌릴 수 없는 링크가 되므로 대체값을 만든다.
    /// </summary>
    public static string MakeUnique(string? candidate, Func<string, bool> isTaken, Guid fallbackSeed)
    {
        ArgumentNullException.ThrowIfNull(isTaken);

        var baseSlug = Normalize(candidate);
        if (baseSlug.Length == 0)
        {
            baseSlug = $"{FallbackPrefix}-{fallbackSeed:N}"[..12];
        }

        if (!isTaken(baseSlug))
        {
            return baseSlug;
        }

        for (var suffix = 2; suffix < int.MaxValue; suffix++)
        {
            var candidateSlug = $"{baseSlug}{Separator}{suffix}";
            if (!isTaken(candidateSlug))
            {
                return candidateSlug;
            }
        }

        throw new InvalidOperationException($"슬러그 '{baseSlug}' 의 빈 자리를 찾지 못했습니다.");
    }
}
