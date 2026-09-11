using Workbench.Application.DTOs;
using Workbench.Application.Interfaces;
using Workbench.Domain;
using Workbench.Domain.Entities;
using Workbench.Domain.Enums;
using Workbench.Domain.Interfaces;

namespace Workbench.Application.Services;

public class ProjectService : IProjectService
{
    private readonly IProjectRepository _projects;
    private readonly IProjectMemberRepository _members;
    private readonly IProjectAccess _access;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public ProjectService(
        IProjectRepository projects,
        IProjectMemberRepository members,
        IProjectAccess access,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _projects = projects;
        _members = members;
        _access = access;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ProjectListItem>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var projects = await _projects.ListOrderedByKeyAsync(cancellationToken);
        var issueCounts = await _projects.GetIssueCountsAsync(cancellationToken);
        var permissions = await _access.GetManyAsync(
            [.. projects.Select(p => p.Id)], cancellationToken);

        return projects
            .Select(p =>
            {
                var access = permissions.GetValueOrDefault(p.Id, ProjectPermissions.None);

                return new ProjectListItem(
                    p.Id,
                    p.Key,
                    p.Name,
                    p.Description,
                    issueCounts.GetValueOrDefault(p.Id),
                    p.CreatedAt,
                    access.CanAdminister,
                    access.IsOpenProject);
            })
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

        // 만든 사람을 관리자로 함께 등록한다. 이렇게 해야 새 프로젝트가 "구성원 없음 = 열림"
        // 상태로 태어나지 않고, 처음부터 책임자가 분명해진다.
        await _members.AddAsync(
            new ProjectMember { ProjectId = project.Id, UserId = user.Id, Role = ProjectRole.Admin },
            cancellationToken);

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

        if (!(await _access.GetAsync(id, cancellationToken)).CanAdminister)
        {
            return OperationResult.Failure("이 프로젝트를 수정할 권한이 없습니다.");
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

        if (!(await _access.GetAsync(id, cancellationToken)).CanAdminister)
        {
            return OperationResult.Failure("이 프로젝트를 삭제할 권한이 없습니다.");
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
