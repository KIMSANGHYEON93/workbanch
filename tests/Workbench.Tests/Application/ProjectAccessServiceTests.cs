using Workbench.Application.DTOs;
using Workbench.Application.Services;
using Workbench.Domain.Entities;
using Workbench.Domain.Enums;
using Workbench.Tests.Fakes;

namespace Workbench.Tests.Application;

public class ProjectAccessServiceTests
{
    private static readonly Guid OtherUserId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    [Fact]
    public async Task ProjectWithNoMembers_IsOpenToEveryone()
    {
        // 이 규칙이 없으면 권한 기능을 붙이는 순간 기존 프로젝트가 전부 손댈 수 없게 된다.
        var harness = new Harness();

        var permissions = await harness.Service.GetAsync(harness.Project.Id);

        Assert.True(permissions.CanWrite);
        Assert.True(permissions.CanAdminister);
        Assert.True(permissions.IsOpenProject);
    }

    [Theory]
    [InlineData(ProjectRole.Viewer, false, false)]
    [InlineData(ProjectRole.Member, true, false)]
    [InlineData(ProjectRole.Admin, true, true)]
    public async Task RoleDecidesWhatTheMemberCanDo(ProjectRole role, bool canWrite, bool canAdminister)
    {
        var harness = new Harness();
        harness.Members.Seed(harness.Project.Id, FakeCurrentUser.DefaultUserId, role);

        var permissions = await harness.Service.GetAsync(harness.Project.Id);

        Assert.Equal(canWrite, permissions.CanWrite);
        Assert.Equal(canAdminister, permissions.CanAdminister);
        Assert.False(permissions.IsOpenProject);
    }

    [Fact]
    public async Task NonMember_CanDoNothingOnceTheProjectHasMembers()
    {
        var harness = new Harness();
        harness.Members.Seed(harness.Project.Id, OtherUserId, ProjectRole.Admin);

        var permissions = await harness.Service.GetAsync(harness.Project.Id);

        Assert.False(permissions.CanWrite);
        Assert.False(permissions.CanAdminister);
        Assert.False(permissions.IsOpenProject);
    }

    [Fact]
    public async Task UnknownProject_GrantsNothing()
    {
        var harness = new Harness();

        Assert.Equal(ProjectPermissions.None, await harness.Service.GetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task AnonymousUser_GrantsNothing()
    {
        // 미인증이 "구성원 없음 = 열림" 을 타고 관리자가 되면 안 된다(fail-closed).
        var harness = new Harness();
        harness.CurrentUser.User = CurrentUserInfo.Anonymous;

        Assert.Equal(ProjectPermissions.None, await harness.Service.GetAsync(harness.Project.Id));
    }

    [Fact]
    public async Task SetRole_RequiresAdministerRights()
    {
        var harness = new Harness();
        harness.Members.Seed(harness.Project.Id, OtherUserId, ProjectRole.Admin);
        harness.Members.Seed(harness.Project.Id, FakeCurrentUser.DefaultUserId, ProjectRole.Member);
        var newcomer = harness.Users.Seed("새 사람", "new@example.com");

        var result = await harness.Service.SetRoleAsync(
            harness.Project.Id, newcomer.Id, ProjectRole.Member);

        Assert.False(result.Succeeded);
        Assert.Equal(2, harness.Members.Members.Count);
    }

    [Fact]
    public async Task SetRole_RejectsInactiveAccounts()
    {
        var harness = new Harness();
        harness.Members.Seed(harness.Project.Id, FakeCurrentUser.DefaultUserId, ProjectRole.Admin);
        var retired = harness.Users.Seed("퇴사자", "left@example.com", isActive: false);

        Assert.False(
            (await harness.Service.SetRoleAsync(harness.Project.Id, retired.Id, ProjectRole.Member))
            .Succeeded);
    }

    [Fact]
    public async Task AnExistingMembersRole_CanStillBeChangedAfterTheAccountIsDeactivated()
    {
        // 활성 확인이 역할 변경까지 막으면, 계정이 비활성화된 순간 관리자가 그 사람을
        // 내리지도 빼지도 못한다 — 활성 확인은 새로 추가할 때만 한다.
        var harness = new Harness();
        harness.Members.Seed(harness.Project.Id, FakeCurrentUser.DefaultUserId, ProjectRole.Admin);
        var retired = harness.Users.Seed("퇴사자", "left@example.com", isActive: false);
        harness.Members.Seed(harness.Project.Id, retired.Id, ProjectRole.Member);

        Assert.True((await harness.Service.SetRoleAsync(
            harness.Project.Id, retired.Id, ProjectRole.Viewer)).Succeeded);
        Assert.True((await harness.Service.RemoveMemberAsync(harness.Project.Id, retired.Id)).Succeeded);
    }

    [Fact]
    public async Task SetRole_AddsThenUpdatesWithoutDuplicating()
    {
        var harness = new Harness();
        harness.Members.Seed(harness.Project.Id, FakeCurrentUser.DefaultUserId, ProjectRole.Admin);
        var teammate = harness.Users.Seed("동료", "mate@example.com");

        Assert.True((await harness.Service.SetRoleAsync(
            harness.Project.Id, teammate.Id, ProjectRole.Viewer)).Succeeded);
        Assert.True((await harness.Service.SetRoleAsync(
            harness.Project.Id, teammate.Id, ProjectRole.Member)).Succeeded);

        var member = Assert.Single(harness.Members.Members, m => m.UserId == teammate.Id);
        Assert.Equal(ProjectRole.Member, member.Role);
    }

    [Fact]
    public async Task TheLastAdmin_CannotBeDemoted()
    {
        // 내리면 아무도 그 프로젝트를 다시 관리할 수 없다 — 구성원이 남아 있는 한
        // "열린 프로젝트" 로도 돌아가지 않으므로 영구 잠김이다.
        var harness = new Harness();
        harness.Members.Seed(harness.Project.Id, FakeCurrentUser.DefaultUserId, ProjectRole.Admin);
        harness.Members.Seed(harness.Project.Id, OtherUserId, ProjectRole.Member);

        var result = await harness.Service.SetRoleAsync(
            harness.Project.Id, FakeCurrentUser.DefaultUserId, ProjectRole.Member);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("마지막 관리자"));
        Assert.Equal(ProjectRole.Admin, harness.Members.Members[0].Role);
    }

    [Fact]
    public async Task TheLastAdmin_CannotBeRemoved()
    {
        var harness = new Harness();
        harness.Members.Seed(harness.Project.Id, FakeCurrentUser.DefaultUserId, ProjectRole.Admin);

        Assert.False((await harness.Service.RemoveMemberAsync(
            harness.Project.Id, FakeCurrentUser.DefaultUserId)).Succeeded);
        Assert.Single(harness.Members.Members);
    }

    [Fact]
    public async Task AnAdmin_CanBeRemovedWhileAnotherRemains()
    {
        var harness = new Harness();
        harness.Members.Seed(harness.Project.Id, FakeCurrentUser.DefaultUserId, ProjectRole.Admin);
        harness.Members.Seed(harness.Project.Id, OtherUserId, ProjectRole.Admin);

        Assert.True((await harness.Service.RemoveMemberAsync(harness.Project.Id, OtherUserId)).Succeeded);
        Assert.Single(harness.Members.Members);
    }

    [Fact]
    public async Task RemoveMember_RequiresAdministerRights()
    {
        var harness = new Harness();
        harness.Members.Seed(harness.Project.Id, OtherUserId, ProjectRole.Admin);
        harness.Members.Seed(harness.Project.Id, FakeCurrentUser.DefaultUserId, ProjectRole.Member);

        Assert.False((await harness.Service.RemoveMemberAsync(harness.Project.Id, OtherUserId)).Succeeded);
        Assert.Equal(2, harness.Members.Members.Count);
    }

    [Fact]
    public async Task SetRole_RejectsAnUndefinedRole()
    {
        var harness = new Harness();
        harness.Members.Seed(harness.Project.Id, FakeCurrentUser.DefaultUserId, ProjectRole.Admin);

        Assert.False((await harness.Service.SetRoleAsync(
            harness.Project.Id, OtherUserId, (ProjectRole)99)).Succeeded);
    }

    private sealed class Harness
    {
        public Harness()
        {
            Project = Projects.Seed("DEV", "개발");
            Service = new ProjectAccessService(Members, Projects, Users, UnitOfWork, CurrentUser);
        }

        public Project Project { get; }

        public FakeProjectRepository Projects { get; } = new();

        public FakeProjectMemberRepository Members { get; } = new();

        public FakeAppUserRepository Users { get; } = new();

        public FakeUnitOfWork UnitOfWork { get; } = new();

        public FakeCurrentUser CurrentUser { get; } = new();

        public ProjectAccessService Service { get; }
    }
}
