using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Workbench.Application.DTOs;
using Workbench.Infrastructure.Data;
using Workbench.Infrastructure.Identity;

namespace Workbench.Tests.Infrastructure;

public class UserProvisionerTests
{
    /// <summary>
    /// 즉시 거부되는 주소. 실제 SQL Server 없이 "DB 가 죽었다" 를 재현하며,
    /// 재시도를 끄고 타임아웃을 1초로 둬서 테스트가 기다리지 않게 한다.
    /// </summary>
    private const string UnreachableDatabase =
        "Server=127.0.0.1,1;Database=Workbench;User Id=sa;Password=none;"
        + "Connect Timeout=1;Encrypt=False;TrustServerCertificate=True";

    private static readonly CurrentUserInfo SignedInUser =
        new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "홍길동", "gildong@example.com", true);

    [Fact]
    public async Task DatabaseOutage_DoesNotThrow()
    {
        // DB 장애로 모든 화면이 500 이 되면 안 된다 — 프로비저닝은 렌더의 전제조건이 아니다.
        var provisioner = CreateProvisioner();

        var provisioned = await provisioner.EnsureProvisionedAsync(SignedInUser, TestCancellation);

        Assert.False(provisioned);
    }

    [Fact]
    public async Task AnonymousUser_IsNotProvisioned()
    {
        var provisioner = CreateProvisioner();

        Assert.False(await provisioner.EnsureProvisionedAsync(CurrentUserInfo.Anonymous, TestCancellation));
    }

    [Fact]
    public async Task AuthenticatedUserWithEmptyId_IsNotProvisioned()
    {
        var provisioner = CreateProvisioner();
        var user = SignedInUser with { Id = Guid.Empty };

        Assert.False(await provisioner.EnsureProvisionedAsync(user, TestCancellation));
    }

    private static UserProvisioner CreateProvisioner()
    {
        var options = new DbContextOptionsBuilder<WorkbenchDbContext>()
            .UseSqlServer(UnreachableDatabase)
            .Options;

        return new UserProvisioner(new WorkbenchDbContext(options), NullLogger<UserProvisioner>.Instance);
    }

    private static CancellationToken TestCancellation => CancellationToken.None;
}
