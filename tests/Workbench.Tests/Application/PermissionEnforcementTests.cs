using Microsoft.EntityFrameworkCore;
using Workbench.Application.DTOs;
using Workbench.Domain.Entities;
using Workbench.Domain.Enums;
using Workbench.Tests.Fakes;
using Workbench.Tests.Infrastructure;

namespace Workbench.Tests.Application;

/// <summary>
/// 권한이 <b>서비스에서</b> 실제로 막히는지 본다. 화면에서 버튼을 감추는 것은 안내이지 통제가
/// 아니므로, 화면을 거치지 않고 서비스를 직접 불러 확인한다.
/// </summary>
public sealed class PermissionEnforcementTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly Guid _projectId;
    private readonly Guid _issueId;
    private readonly AppUser _outsider;

    public PermissionEnforcementTests()
    {
        var project = _database.Projects
            .CreateAsync(new ProjectEditModel { Key = "DEV", Name = "개발" })
            .GetAwaiter().GetResult();
        _projectId = project.Value;

        var issue = _database.Issues
            .CreateAsync(new IssueEditModel { ProjectId = _projectId, Title = "이슈" })
            .GetAwaiter().GetResult();
        _issueId = _database.Context.Issues.Single(i => i.Key == issue.Value).Id;

        _outsider = new AppUser { DisplayName = "외부인", Email = "outsider@example.com" };
        _database.Context.Users.Add(_outsider);
        _database.Context.SaveChanges();
    }

    [Fact]
    public async Task CreatingAProject_MakesTheCreatorAnAdmin()
    {
        // 새 프로젝트가 "구성원 없음 = 열림" 상태로 태어나면 책임자가 없다.
        var permissions = await _database.Access.GetAsync(_projectId);

        Assert.True(permissions.CanAdminister);
        Assert.False(permissions.IsOpenProject);

        var member = Assert.Single(await _database.Access.ListMembersAsync(_projectId));
        Assert.Equal(FakeCurrentUser.DefaultUserId, member.UserId);
        Assert.Equal(ProjectRole.Admin, member.Role);
    }

    [Fact]
    public async Task Outsider_CannotCreateAnIssueInTheProject()
    {
        BecomeOutsider();

        var result = await _database.Issues.CreateAsync(
            new IssueEditModel { ProjectId = _projectId, Title = "몰래 만든 이슈" });

        Assert.False(result.Succeeded);
        Assert.Equal(1, await _database.Context.Issues.CountAsync());
    }

    [Fact]
    public async Task Outsider_CannotChangeOrDeleteAnIssue()
    {
        BecomeOutsider();

        Assert.False((await _database.Issues.UpdateAsync(
            _issueId, new IssueEditModel { ProjectId = _projectId, Title = "제목 바꾸기" })).Succeeded);
        Assert.False((await _database.Issues.ChangeStatusAsync(_issueId, IssueStatus.Done)).Succeeded);
        Assert.False((await _database.Issues.DeleteAsync(_issueId)).Succeeded);

        var issue = await _database.Context.Issues.SingleAsync();
        Assert.Equal("이슈", issue.Title);
        Assert.Equal(IssueStatus.Backlog, issue.Status);
    }

    [Fact]
    public async Task Outsider_CannotChangeOrDeleteTheProject()
    {
        BecomeOutsider();

        Assert.False((await _database.Projects.UpdateAsync(
            _projectId, new ProjectEditModel { Key = "DEV", Name = "가로챈 이름" })).Succeeded);
        Assert.False((await _database.Projects.DeleteAsync(_projectId)).Succeeded);

        Assert.Equal("개발", (await _database.Context.Projects.SingleAsync()).Name);
    }

    [Fact]
    public async Task Viewer_CanReadButNotWrite()
    {
        await GrantAsync(ProjectRole.Viewer);
        BecomeOutsider();

        var permissions = await _database.Access.GetAsync(_projectId);
        Assert.False(permissions.CanWrite);

        Assert.False((await _database.Issues.ChangeStatusAsync(_issueId, IssueStatus.Done)).Succeeded);
    }

    [Fact]
    public async Task Member_CanWriteButNotAdminister()
    {
        await GrantAsync(ProjectRole.Member);
        BecomeOutsider();

        Assert.True((await _database.Issues.ChangeStatusAsync(_issueId, IssueStatus.InProgress)).Succeeded);
        Assert.False((await _database.Projects.DeleteAsync(_projectId)).Succeeded);
    }

    [Fact]
    public async Task PagesWithoutAProject_AreWritableByAnyone()
    {
        // 전사 문서는 프로젝트에 묶여 있지 않다.
        BecomeOutsider();

        var created = await _database.Pages.CreateAsync(new PageEditModel { Title = "전사 안내" });

        Assert.True(created.Succeeded);
    }

    [Fact]
    public async Task PagesAttachedToAProject_FollowThatProjectsRules()
    {
        BecomeOutsider();

        var created = await _database.Pages.CreateAsync(
            new PageEditModel { Title = "프로젝트 문서", ProjectId = _projectId });

        Assert.False(created.Succeeded);
        Assert.Equal(0, await _database.Context.Pages.CountAsync());
    }

    [Fact]
    public async Task APageCannotBePushedIntoAProjectTheAuthorCannotWrite()
    {
        // 한쪽만 보면 권한 없는 프로젝트로 문서를 밀어 넣을 수 있다.
        var created = await _database.Pages.CreateAsync(new PageEditModel { Title = "전사 안내" });
        var pageId = _database.Context.Pages.Single(p => p.Slug == created.Value).Id;

        BecomeOutsider();

        var moved = await _database.Pages.UpdateAsync(
            pageId, new PageEditModel { Title = "전사 안내", ProjectId = _projectId });

        Assert.False(moved.Succeeded);
        Assert.Null((await _database.Context.Pages.SingleAsync()).ProjectId);
    }

    public void Dispose() => _database.Dispose();

    private void BecomeOutsider() =>
        _database.CurrentUser.User = new CurrentUserInfo(
            _outsider.Id, _outsider.DisplayName, _outsider.Email, IsAuthenticated: true);

    private async Task GrantAsync(ProjectRole role) =>
        Assert.True((await _database.Access.SetRoleAsync(_projectId, _outsider.Id, role)).Succeeded);
}
