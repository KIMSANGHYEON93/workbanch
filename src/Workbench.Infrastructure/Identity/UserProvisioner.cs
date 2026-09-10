using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Workbench.Application.DTOs;
using Workbench.Application.Interfaces;
using Workbench.Domain;
using Workbench.Domain.Entities;
using Workbench.Infrastructure.Data;

namespace Workbench.Infrastructure.Identity;

public class UserProvisioner : IUserProvisioner
{
    private readonly WorkbenchDbContext _dbContext;
    private readonly ILogger<UserProvisioner> _logger;

    public UserProvisioner(WorkbenchDbContext dbContext, ILogger<UserProvisioner> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<bool> EnsureProvisionedAsync(
        CurrentUserInfo user,
        CancellationToken cancellationToken = default)
    {
        if (!user.IsAuthenticated || user.Id == Guid.Empty)
        {
            return false;
        }

        try
        {
            var existing = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == user.Id, cancellationToken);

            var displayName = Truncate(user.DisplayName, DomainConstants.Lengths.DisplayName);
            var email = Truncate(user.Email, DomainConstants.Lengths.Email);

            if (existing is null)
            {
                _dbContext.Users.Add(new AppUser
                {
                    Id = user.Id,
                    DisplayName = displayName,
                    Email = email,
                    LastSeenAt = DateTimeOffset.UtcNow,
                });
            }
            else
            {
                // 표시 이름·메일은 Entra 쪽이 정본이다. 로컬에서 고친 값은 다음 로그인에 덮인다.
                existing.DisplayName = displayName;
                existing.Email = email;
                existing.LastSeenAt = DateTimeOffset.UtcNow;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception ex) when (IsDatabaseFailure(ex))
        {
            // DB 가 잠깐 죽었다고 모든 화면이 500 이 되면 안 된다. 동시 로그인 두 건이 같은
            // 사용자를 만들어 PK 가 충돌하는 경우도 여기로 온다 — 다음 요청이 기존 행을 찾는다.
            _logger.LogWarning(ex, "사용자 프로비저닝 실패 (UserId={UserId}). 요청은 계속한다.", user.Id);
            _dbContext.ChangeTracker.Clear();
            return false;
        }
    }

    /// <summary>
    /// DB 로 인한 실패만 흡수한다. 그 밖의 예외는 프로그래밍 오류일 수 있으므로 그대로 올린다.
    /// </summary>
    private static bool IsDatabaseFailure(Exception exception) =>
        exception is DbUpdateException or DbException or RetryLimitExceededException
        || (exception.InnerException is not null && IsDatabaseFailure(exception.InnerException));

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
