using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Workbench.Application.DTOs;
using Workbench.Application.Interfaces;
using Workbench.Application.Services;
using Workbench.Domain.Entities;
using Workbench.Domain.Enums;
using Workbench.Tests.Fakes;
using Workbench.Web.Components.Pages.Projects;

namespace Workbench.Tests.Components;

public class ProjectMembersTests : TestContext
{
    private readonly FakeProjectRepository _projects = new();
    private readonly FakeProjectMemberRepository _members = new();
    private readonly FakeAppUserRepository _users = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly Project _project;

    public ProjectMembersTests()
    {
        _project = _projects.Seed("DEV", "개발");

        var unitOfWork = new FakeUnitOfWork();
        var access = new ProjectAccessService(_members, _projects, _users, unitOfWork, _currentUser);

        Services.AddSingleton<IProjectAccess>(access);
        Services.AddSingleton<IProjectService>(
            new ProjectService(_projects, _members, access, unitOfWork, _currentUser));
        Services.AddSingleton<IIssueService>(new IssueService(
            new FakeIssueRepository(), _projects, _users, access, unitOfWork, _currentUser,
            NullLogger<IssueService>.Instance));
    }

    [Fact]
    public void OpenProject_ExplainsWhyAnyoneCanWrite()
    {
        // 화면이 그 사실을 말해 주지 않으면 사용자는 "왜 아무나 고칠 수 있지" 를 알 수 없다.
        var page = RenderComponent<ProjectMembers>(p => p.Add(c => c.Id, _project.Id));

        Assert.Contains("열린 프로젝트입니다", page.Markup);
        Assert.Contains("등록된 구성원이 없습니다", page.Markup);
    }

    [Fact]
    public void Administrator_SeesTheAddForm()
    {
        _users.Seed("홍길동", "gildong@example.com");
        _members.Seed(_project.Id, FakeCurrentUser.DefaultUserId, ProjectRole.Admin);

        var page = RenderComponent<ProjectMembers>(p => p.Add(c => c.Id, _project.Id));

        Assert.NotNull(page.Find("#add-user"));
        Assert.DoesNotContain("변경할 권한은 없습니다", page.Markup);
    }

    [Fact]
    public void NonAdministrator_SeesTheListButNoControls()
    {
        var teammate = _users.Seed("동료", "mate@example.com");
        _members.Seed(_project.Id, teammate.Id, ProjectRole.Admin);
        _members.Seed(_project.Id, FakeCurrentUser.DefaultUserId, ProjectRole.Member);

        var page = RenderComponent<ProjectMembers>(p => p.Add(c => c.Id, _project.Id));

        Assert.Contains("변경할 권한은 없습니다", page.Markup);
        Assert.Empty(page.FindAll("#add-user"));

        // 버튼을 감추는 것만으로는 통제가 아니지만, 안내로서는 정확해야 한다.
        Assert.All(page.FindAll("select[aria-label$='역할']"), s => Assert.True(s.HasAttribute("disabled")));
        Assert.All(page.FindAll("button.btn-outline-danger"), b => Assert.True(b.HasAttribute("disabled")));
    }

    [Fact]
    public void DemotingTheLastAdministrator_ShowsTheRefusal()
    {
        _members.Seed(_project.Id, FakeCurrentUser.DefaultUserId, ProjectRole.Admin);

        var page = RenderComponent<ProjectMembers>(p => p.Add(c => c.Id, _project.Id));
        page.Find("select[aria-label$='역할']").Change(ProjectRole.Member.ToString());

        Assert.Contains("마지막 관리자", page.Markup);
    }

    [Fact]
    public void MissingProject_SaysSo()
    {
        var page = RenderComponent<ProjectMembers>(p => p.Add(c => c.Id, Guid.NewGuid()));

        Assert.Contains("프로젝트를 찾을 수 없습니다", page.Markup);
    }
}
