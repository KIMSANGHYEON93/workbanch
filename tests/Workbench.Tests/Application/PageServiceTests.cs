using Workbench.Application.DTOs;
using Workbench.Application.Services;
using Workbench.Tests.Fakes;

namespace Workbench.Tests.Application;

public class PageServiceTests
{
    [Fact]
    public async Task Create_DerivesTheSlugFromTheTitle()
    {
        var harness = new Harness();

        var result = await harness.Service.CreateAsync(Model("배포 절차"));

        Assert.True(result.Succeeded);
        Assert.Equal("배포-절차", result.Value);
    }

    [Fact]
    public async Task Create_HonoursAnExplicitSlug()
    {
        var harness = new Harness();

        var model = Model("배포 절차");
        model.Slug = "deploy-runbook";

        Assert.Equal("deploy-runbook", (await harness.Service.CreateAsync(model)).Value);
    }

    [Fact]
    public async Task Create_StepsAsideWhenTheSlugIsTaken()
    {
        var harness = new Harness();
        harness.Pages.Seed("배포 절차", "배포-절차");

        Assert.Equal("배포-절차-2", (await harness.Service.CreateAsync(Model("배포 절차"))).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_RejectsBlankTitle(string title)
    {
        var harness = new Harness();

        Assert.False((await harness.Service.CreateAsync(Model(title))).Succeeded);
        Assert.Empty(harness.Pages.Pages);
    }

    [Fact]
    public async Task Create_RejectsUnknownParent()
    {
        var harness = new Harness();

        var model = Model("하위 문서");
        model.ParentPageId = Guid.NewGuid();

        Assert.False((await harness.Service.CreateAsync(model)).Succeeded);
    }

    [Fact]
    public async Task Create_RejectsUnknownProject()
    {
        var harness = new Harness();

        var model = Model("문서");
        model.ProjectId = Guid.NewGuid();

        Assert.False((await harness.Service.CreateAsync(model)).Succeeded);
    }

    [Fact]
    public async Task Update_KeepsTheSlugWhenLeftBlank()
    {
        // 제목을 고쳤다고 링크가 끊기면 안 된다.
        var harness = new Harness();
        var page = harness.Pages.Seed("배포 절차", "배포-절차");

        var result = await harness.Service.UpdateAsync(page.Id, Model("배포 절차 (개정)"));

        Assert.True(result.Succeeded);
        Assert.Equal("배포-절차", result.Value);
        Assert.Equal("배포 절차 (개정)", page.Title);
    }

    [Fact]
    public async Task Update_RejectsMakingAPageItsOwnParent()
    {
        var harness = new Harness();
        var page = harness.Pages.Seed("문서", "문서");

        var model = Model("문서");
        model.ParentPageId = page.Id;

        var result = await harness.Service.UpdateAsync(page.Id, model);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("자기 자신"));
    }

    [Fact]
    public async Task Update_RejectsMovingAPageUnderItsOwnDescendant()
    {
        // 이 고리가 생기면 그 가지는 트리에서 떨어져 나와 어느 화면에서도 다시 보이지 않는다.
        var harness = new Harness();
        var root = harness.Pages.Seed("루트", "루트");
        var child = harness.Pages.Seed("자식", "자식", root.Id);
        var grandChild = harness.Pages.Seed("손자", "손자", child.Id);

        var model = Model("루트");
        model.ParentPageId = grandChild.Id;

        var result = await harness.Service.UpdateAsync(root.Id, model);

        Assert.False(result.Succeeded);
        Assert.Null(root.ParentPageId);
    }

    [Fact]
    public async Task Update_AllowsMovingToAnUnrelatedPage()
    {
        var harness = new Harness();
        var root = harness.Pages.Seed("루트", "루트");
        var other = harness.Pages.Seed("다른 문서", "다른-문서");

        var model = Model("루트");
        model.ParentPageId = other.Id;

        Assert.True((await harness.Service.UpdateAsync(root.Id, model)).Succeeded);
        Assert.Equal(other.Id, root.ParentPageId);
    }

    [Fact]
    public async Task Delete_IsRefusedWhileChildrenRemain()
    {
        var harness = new Harness();
        var root = harness.Pages.Seed("루트", "루트");
        harness.Pages.Seed("자식", "자식", root.Id);

        var result = await harness.Service.DeleteAsync(root.Id);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("1"));
        Assert.Equal(2, harness.Pages.Pages.Count);
    }

    [Fact]
    public async Task Delete_RemovesALeaf()
    {
        var harness = new Harness();
        var page = harness.Pages.Seed("문서", "문서");

        Assert.True((await harness.Service.DeleteAsync(page.Id)).Succeeded);
        Assert.Empty(harness.Pages.Pages);
    }

    [Fact]
    public async Task ListTree_ReportsDepth()
    {
        var harness = new Harness();
        var root = harness.Pages.Seed("루트", "루트");
        var child = harness.Pages.Seed("자식", "자식", root.Id);
        harness.Pages.Seed("손자", "손자", child.Id);

        var tree = await harness.Service.ListTreeAsync();

        Assert.Equal(["루트", "자식", "손자"], tree.Select(p => p.Title));
        Assert.Equal([0, 1, 2], tree.Select(p => p.Depth));
    }

    [Fact]
    public async Task ListTree_DoesNotLoseAPageWhoseParentIsMissing()
    {
        // 부모가 사라져도 문서 자체는 목록에서 잃지 않는다 — 아니면 조용히 접근 불가가 된다.
        var harness = new Harness();
        harness.Pages.Seed("고아", "고아", Guid.NewGuid());

        Assert.Equal("고아", Assert.Single(await harness.Service.ListTreeAsync()).Title);
    }

    [Fact]
    public async Task ListParentOptions_ExcludesSelfAndDescendants()
    {
        var harness = new Harness();
        var root = harness.Pages.Seed("루트", "루트");
        var child = harness.Pages.Seed("자식", "자식", root.Id);
        harness.Pages.Seed("손자", "손자", child.Id);
        harness.Pages.Seed("무관", "무관");

        var options = await harness.Service.ListParentOptionsAsync(root.Id);

        Assert.Equal(["무관"], options.Select(p => p.Title));
    }

    [Fact]
    public async Task Search_ReturnsAFlatListWithoutFalseIndentation()
    {
        var harness = new Harness();
        var root = harness.Pages.Seed("배포 절차", "배포-절차");
        harness.Pages.Seed("배포 체크리스트", "배포-체크리스트", root.Id);

        var results = await harness.Service.SearchAsync("배포");

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(0, r.Depth));
    }

    private static PageEditModel Model(string title) => new() { Title = title };

    private sealed class Harness
    {
        public Harness()
        {
            UnitOfWork = new FakeUnitOfWork();
            var currentUser = new FakeCurrentUser();
            Access = new ProjectAccessService(Members, Projects, Users, UnitOfWork, currentUser);
            Service = new PageService(Pages, Projects, Access, UnitOfWork, currentUser);
        }

        public FakePageRepository Pages { get; } = new();

        public FakeProjectRepository Projects { get; } = new();

        public FakeProjectMemberRepository Members { get; } = new();

        public FakeAppUserRepository Users { get; } = new();

        public ProjectAccessService Access { get; }

        public FakeUnitOfWork UnitOfWork { get; }

        public PageService Service { get; }
    }
}
