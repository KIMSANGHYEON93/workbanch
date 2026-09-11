namespace Workbench.Tests;

/// <summary>
/// 소스를 텍스트로 읽어 검사하는 계약들이 쓰는 저장소 루트 탐색기.
/// 컴파일된 산출물이 아니라 <b>원본 파일</b>을 봐야 하는 계약이 있다 — Razor 속성이
/// 식(expression)인지 리터럴인지, 번들된 CSS 에 클래스가 실재하는지 같은 것들.
/// </summary>
public static class RepositoryFiles
{
    private const string SolutionFileName = "Workbench.sln";

    public static string Root { get; } = FindRoot();

    public static string Path(params string[] segments) =>
        System.IO.Path.Combine(new[] { Root }.Concat(segments).ToArray());

    public static IReadOnlyList<string> RazorFiles() =>
        Directory.GetFiles(Path("src"), "*.razor", SearchOption.AllDirectories);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(System.IO.Path.Combine(directory.FullName, SolutionFileName)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"'{SolutionFileName}' 을(를) 찾지 못했습니다. 테스트가 저장소 밖에서 실행되고 있습니다.");
    }
}
