using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Workbench.Infrastructure.Data;

/// <summary>
/// `dotnet ef migrations` 전용. 마이그레이션 생성은 DB 에 접속하지 않으므로
/// 접속 문자열은 프로바이더(SQL Server) 문법을 고르기 위한 자리표시자면 충분하다.
/// 실제 접속 문자열을 여기에 두지 않는 이유이기도 하다 — 시크릿이 소스에 남지 않게.
/// </summary>
public class WorkbenchDbContextFactory : IDesignTimeDbContextFactory<WorkbenchDbContext>
{
    // LocalDB 를 쓰지 않는다 — Linux/macOS 개발자의 `dotnet ef` 가 "LocalDB is not supported
    // on this platform" 으로 죽는다. 접속하지 않는 명령(add/script)에는 어떤 문자열이든 무방하고,
    // 실제 접속이 필요한 명령은 WORKBENCH_DESIGNTIME_CONNECTION 으로 덮어쓴다.
    private const string DesignTimeConnectionString =
        "Server=localhost;Database=Workbench;Trusted_Connection=True;TrustServerCertificate=True";

    public WorkbenchDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<WorkbenchDbContext>()
            .UseSqlServer(
                Environment.GetEnvironmentVariable("WORKBENCH_DESIGNTIME_CONNECTION")
                    ?? DesignTimeConnectionString)
            .Options;

        return new WorkbenchDbContext(options);
    }
}
