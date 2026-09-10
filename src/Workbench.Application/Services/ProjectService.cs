using Workbench.Application.DTOs;
using Workbench.Application.Interfaces;
using Workbench.Domain;
using Workbench.Domain.Entities;
using Workbench.Domain.Interfaces;

namespace Workbench.Application.Services;

public class ProjectService : IProjectService
{
    private readonly IProjectRepository _projects;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public ProjectService(
        IProjectRepository projects,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _projects = projects;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ProjectListItem>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var projects = await _projects.ListOrderedByKeyAsync(cancellationToken);
        var issueCounts = await _projects.GetIssueCountsAsync(cancellationToken);

        return projects
            .Select(p => new ProjectListItem(
                p.Id,
                p.Key,
                p.Name,
                p.Description,
                issueCounts.GetValueOrDefault(p.Id),
                p.CreatedAt))
            .ToList();
    }

    public async Task<ProjectDetail?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var project = await _projects.GetByIdAsync(id, cancellationToken);

        return project is null
            ? null
            : new ProjectDetail(project.Id, project.Key, project.Name, project.Description, project.CreatedAt);
    }

    public async Task<OperationResult<Guid>> CreateAsync(
        ProjectEditModel model,
        CancellationToken cancellationToken = default)
    {
        var key = ProjectKey.Normalize(model.Key);
        var name = (model.Name ?? string.Empty).Trim();

        var errors = await ValidateAsync(key, name, excludingProjectId: null, cancellationToken);
        if (errors.Count > 0)
        {
            return OperationResult<Guid>.Failure([.. errors]);
        }

        var user = await _currentUser.GetAsync(cancellationToken);

        var project = new Project
        {
            Key = key,
            Name = name,
            Description = Trimmed(model.Description),
            CreatedById = user.Id,
        };

        await _projects.AddAsync(project, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return OperationResult<Guid>.Success(project.Id);
    }

    public async Task<OperationResult> UpdateAsync(
        Guid id,
        ProjectEditModel model,
        CancellationToken cancellationToken = default)
    {
        var project = await _projects.GetByIdAsync(id, cancellationToken);
        if (project is null)
        {
            return OperationResult.Failure("프로젝트를 찾을 수 없습니다.");
        }

        // 키는 만든 뒤 바꿀 수 없다. Issue.Key 가 "DEV-123" 으로 비정규화돼 있어서,
        // 여기서 키를 바꾸면 이미 발급된 이슈 키 전부가 프로젝트와 어긋난다.
        var key = ProjectKey.Normalize(model.Key);
        if (!string.Equals(key, project.Key, StringComparison.Ordinal))
        {
            return OperationResult.Failure(
                "프로젝트 키는 생성 후 변경할 수 없습니다. 이미 발급된 이슈 키가 이 값을 사용합니다.");
        }

        var name = (model.Name ?? string.Empty).Trim();

        var errors = await ValidateAsync(key, name, excludingProjectId: id, cancellationToken);
        if (errors.Count > 0)
        {
            return OperationResult.Failure([.. errors]);
        }

        project.Name = name;
        project.Description = Trimmed(model.Description);

        _projects.Update(project);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return OperationResult.Success();
    }

    public async Task<OperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var project = await _projects.GetByIdAsync(id, cancellationToken);
        if (project is null)
        {
            return OperationResult.Failure("프로젝트를 찾을 수 없습니다.");
        }

        // 이슈는 CASCADE 로 함께 지워진다(페이지는 연결만 끊긴다). 되돌릴 수 없으므로
        // 이슈가 남아 있으면 거부하고, 지울 의사가 있으면 이슈부터 정리하게 한다.
        var issueCounts = await _projects.GetIssueCountsAsync(cancellationToken);
        var issueCount = issueCounts.GetValueOrDefault(id);
        if (issueCount > 0)
        {
            return OperationResult.Failure(
                $"이슈 {issueCount}건이 남아 있어 삭제할 수 없습니다. 이슈를 먼저 정리하세요.");
        }

        _projects.Remove(project);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return OperationResult.Success();
    }

    private async Task<List<string>> ValidateAsync(
        string key,
        string name,
        Guid? excludingProjectId,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add("프로젝트 이름을 입력하세요.");
        }
        else if (name.Length > DomainConstants.Lengths.ProjectName)
        {
            errors.Add($"이름은 {DomainConstants.Lengths.ProjectName}자를 넘을 수 없습니다.");
        }

        if (!ProjectKey.IsValid(key))
        {
            errors.Add("프로젝트 키는 영문 대문자로 시작하는 2~10자의 영문 대문자·숫자여야 합니다 (예: DEV, DEPLOY).");
        }
        else if (await _projects.KeyExistsAsync(key, excludingProjectId, cancellationToken))
        {
            errors.Add($"프로젝트 키 '{key}' 는 이미 사용 중입니다.");
        }

        return errors;
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
