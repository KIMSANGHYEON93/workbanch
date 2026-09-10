using System.Text.RegularExpressions;

namespace Workbench.Domain;

/// <summary>
/// 프로젝트 키 정규화·검증의 단일 출처. 키는 이슈 키(DEV-123)의 접두어로 굳어지므로
/// 저장 전에 반드시 여기를 통과한다.
/// </summary>
public static partial class ProjectKey
{
    /// <summary>앞뒤 공백을 없애고 대문자로 만든다. 'dev' 와 'DEV ' 가 다른 프로젝트가 되면 안 된다.</summary>
    public static string Normalize(string? key) =>
        (key ?? string.Empty).Trim().ToUpperInvariant();

    public static bool IsValid(string? key) => Pattern().IsMatch(Normalize(key));

    [GeneratedRegex(DomainConstants.ProjectKeyPattern, RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
