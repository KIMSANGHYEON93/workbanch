using Workbench.Application.DTOs;
using Workbench.Application.Services;
using Workbench.Tests.Fakes;

namespace Workbench.Tests.Application;

public class ProjectServiceTests
{
    [Theory]
    [InlineData("dev", "DEV")]
    [InlineData("  deploy  ", "DEPLOY")]
    [InlineData("Dev1", "DEV1")]
    public async Task Create_NormalizesTheKey(string input, string expected)
    {
        // 'dev' 와 'DEV ' 가 서로 다른 프로젝트가 되면 이슈 키 접두어가 갈라진다.
        var (service, repository, _) = CreateService();

        var result = await service.CreateAsync(Model(key: input, name: "개발"));

        Assert.True(result.Succeeded);
        Assert.Equal(expected, Assert.Single(repository.Projects).Key);
    }

    [Theory]
    [InlineData("D")]
    [InlineData("1DEV")]
    [InlineData("DE-V")]
    [InlineData("TOOLONGKEY11")]
    [InlineData("")]
    public async Task Create_RejectsInvalidKeys(string key)
    {
        var (service, repository, _) = CreateService();

        var result = await service.CreateAsync(Model(key: key, name: "개발"));

        Assert.False(result.Succeeded);
        Assert.Empty(repository.Projects);
    }

    [Fact]
    public async Task Create_RejectsDuplicateKey()
    {
        var (service, repository, _) = CreateService();
        repository.Seed("DEV", "기존 프로젝트");

        var result = await service.CreateAsync(Model(key: "dev", name: "새 프로젝트"));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("DEV"));
        Assert.Single(repository.Projects);
    }

    [Fact]
    public async Task Create_RejectsBlankName()
    {
        var (service, repository, _) = CreateService();

        var result = await service.CreateAsync(Model(key: "DEV", name: "   "));

        Assert.False(result.Succeeded);
        Assert.Empty(repository.Projects);
    }

    [Fact]
    public async Task Create_RecordsTheSignedInUserAsCreator()
    {
        var (service, repository, _) = CreateService();

        await service.CreateAsync(Model(key: "DEV", name: "개발"));

        Assert.Equal(FakeCurrentUser.DefaultUserId, Assert.Single(repository.Projects).CreatedById);
    }

    [Fact]
    public async Task Create_DoesNotSaveWhenValidationFails()
    {
        var (service, _, unitOfWork) = CreateService();

        await service.CreateAsync(Model(key: "bad key", name: "개발"));

        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task Update_RejectsKeyChange()
    {
        // Issue.Key 가 "DEV-123" 으로 비정규화돼 있어, 키를 바꾸면 발급된 이슈 키가 전부 어긋난다.
        var (service, repository, unitOfWork) = CreateService();
        var project = repository.Seed("DEV", "개발");

        var result = await service.UpdateAsync(project.Id, Model(key: "PLATFORM", name: "개발"));

        Assert.False(result.Succeeded);
        Assert.Equal("DEV", project.Key);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task Update_AcceptsTheSameKeyInDifferentCasing()
    {
        // 화면이 돌려보낸 값이 소문자여도 "키를 바꾸려 한다" 로 오해하면 안 된다.
        var (service, repository, _) = CreateService();
        var project = repository.Seed("DEV", "개발");

        var result = await service.UpdateAsync(project.Id, Model(key: " dev ", name: "개발 플랫폼"));

        Assert.True(result.Succeeded);
        Assert.Equal("개발 플랫폼", project.Name);
    }

    [Fact]
    public async Task Update_ReturnsFailureWhenProjectIsMissing()
    {
        var (service, _, _) = CreateService();

        var result = await service.UpdateAsync(Guid.NewGuid(), Model(key: "DEV", name: "개발"));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Delete_IsRefusedWhileIssuesRemain()
    {
        // 이슈는 CASCADE 로 함께 사라진다 — 되돌릴 수 없으므로 조용히 지우지 않는다.
        var (service, repository, unitOfWork) = CreateService();
        var project = repository.Seed("DEV", "개발", issueCount: 3);

        var result = await service.DeleteAsync(project.Id);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("3"));
        Assert.Single(repository.Projects);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task Delete_RemovesAnEmptyProject()
    {
        var (service, repository, unitOfWork) = CreateService();
        var project = repository.Seed("DEV", "개발");

        var result = await service.DeleteAsync(project.Id);

        Assert.True(result.Succeeded);
        Assert.Empty(repository.Projects);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task List_AttachesIssueCountsAndDefaultsToZero()
    {
        var (service, repository, _) = CreateService();
        repository.Seed("DEV", "개발", issueCount: 7);
        repository.Seed("OPS", "운영");

        var items = await service.ListAsync();

        Assert.Equal(7, items.Single(i => i.Key == "DEV").IssueCount);
        Assert.Equal(0, items.Single(i => i.Key == "OPS").IssueCount);
    }

    private static ProjectEditModel Model(string key, string name, string? description = null) =>
        new() { Key = key, Name = name, Description = description };

    private static (ProjectService Service, FakeProjectRepository Repository, FakeUnitOfWork UnitOfWork)
        CreateService()
    {
        var repository = new FakeProjectRepository();
        var unitOfWork = new FakeUnitOfWork(repository);
        var service = new ProjectService(repository, unitOfWork, new FakeCurrentUser());

        return (service, repository, unitOfWork);
    }
}
