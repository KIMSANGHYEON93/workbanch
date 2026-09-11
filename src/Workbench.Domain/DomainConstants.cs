namespace Workbench.Domain;

/// <summary>
/// 저장소 스키마와 UI 검증이 같은 값을 쓰도록 길이/형식 제약을 한곳에 모은다.
/// EF Core 설정과 Blazor 폼 검증이 서로 다른 상수를 들고 갈라지는 것을 막는 것이 목적이다.
/// </summary>
public static class DomainConstants
{
    public static class Lengths
    {
        public const int ProjectName = 200;
        public const int ProjectKey = 10;
        public const int Description = 2000;
        public const int IssueKey = 32;
        public const int IssueTitle = 300;
        public const int EnvironmentName = 50;
        public const int VersionLabel = 50;
        public const int PageTitle = 300;
        public const int Slug = 300;
        public const int FileName = 260;
        public const int ContentType = 128;
        // 512자(1,024바이트). BlobPath 에는 UNIQUE 인덱스가 걸리는데 SQL Server 비클러스터드
        // 인덱스 키 상한이 1,700바이트라, nvarchar(1024)=2,048바이트면 긴 값에서 INSERT 가 터진다.
        public const int BlobPath = 512;
        public const int DisplayName = 200;
        public const int Email = 320;
    }

    /// <summary>프로젝트 키는 이슈 키(DEV-123)의 접두어라 대문자 영숫자로 제한한다.</summary>
    public const string ProjectKeyPattern = "^[A-Z][A-Z0-9]{1,9}$";

    /// <summary>이슈 키 구분자. 표시/파싱 양쪽이 같은 문자를 쓰도록 상수로 고정한다.</summary>
    public const char IssueKeySeparator = '-';
}
