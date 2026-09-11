namespace Workbench.Infrastructure.Data;

public enum DatabaseProvider
{
    /// <summary>Azure SQL / SQL Server. 운영에서 쓰는 유일한 프로바이더.</summary>
    SqlServer = 0,

    /// <summary>
    /// 파일 기반 SQLite. <b>로컬 데모 전용</b> — SQL Server 를 설치하지 않은 기계에서
    /// 화면을 띄워 보기 위한 것이다.
    /// </summary>
    Sqlite = 1,
}

public class DatabaseOptions
{
    public const string SectionName = "Database";

    public DatabaseProvider Provider { get; set; } = DatabaseProvider.SqlServer;

    /// <summary>
    /// SQLite 데모 모드에서 기동 시 스키마를 만들고 예시 데이터를 넣을지 여부.
    /// 마이그레이션은 SQL Server 전용이라 이 모드는 <c>EnsureCreated</c> 로 스키마를 세운다.
    /// </summary>
    public bool SeedDemoData { get; set; } = true;
}
