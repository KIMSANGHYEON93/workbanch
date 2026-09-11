using Workbench.Application.DTOs;
using Workbench.Application.Interfaces;

namespace Workbench.Tests.Fakes;

public sealed class FakeCurrentUser : ICurrentUser
{
    public static readonly Guid DefaultUserId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    public CurrentUserInfo User { get; set; } =
        new(DefaultUserId, "홍길동", "gildong@example.com", IsAuthenticated: true);

    public Task<CurrentUserInfo> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(User);
}
