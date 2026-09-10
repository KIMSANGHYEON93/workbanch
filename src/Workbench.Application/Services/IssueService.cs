using Microsoft.Extensions.Logging;
using Workbench.Application.DTOs;
using Workbench.Application.Interfaces;
using Workbench.Domain;
using Workbench.Domain.Entities;
using Workbench.Domain.Enums;
using Workbench.Domain.Interfaces;

namespace Workbench.Application.Services;

public class IssueService : IIssueService
{
    /// <summary>
    /// 이슈 번호 발급 재시도 횟수. 두 사람이 같은 프로젝트에 동시에 이슈를 만들면 같은 번호를
    /// 집어 UNIQUE(ProjectId, Number) 가 한쪽을 거절한다 — 그때 다시 읽어서 다음 번호를 집는다.
    /// </summary>
    private const int KeyAllocationAttempts = 3;

    private readonly IIssueRepository _issues;
    private readonly IProjectRepository _projects;
    private readonly IAppUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<IssueService> _logger;

    public IssueService(
        IIssueRepository issues,
        IProjectRepository projects,
        IAppUserRepository users,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        ILogger<IssueService> logger)
    {
        _issues = issues;
        _projects = projects;
        _users = users;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<IReadOnlyList<IssueListItem>> ListAsync(
        IssueQuery query,
        CancellationToken cancellationToken = default)
    {
        var issues = await _issues.ListAsync(query, cancellationToken);

        return [.. issues.Select(ToListItem)];
    }

    public async Task<IssueDetail?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        var issue = await _issues.GetByKeyAsync(NormalizeKey(key), cancellationToken);

        return issue is null ? null : ToDetail(issue);
    }

    public async Task<IReadOnlyList<UserOption>> ListAssigneeOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var users = await _users.ListActiveAsync(cancellationToken);

        return [.. users.Select(u => new UserOption(u.Id, u.DisplayName, u.Email))];
    }

    public async Task<OperationResult<string>> CreateAsync(
        IssueEditModel model,
        CancellationToken cancellationToken = default)
    {
        var title = (model.Title ?? string.Empty).Trim();

        var errors = await ValidateAsync(model, title, cancellationToken);
        if (errors.Count > 0)
        {
            return OperationResult<string>.Failure([.. errors]);
        }

        var reporter = await _currentUser.GetAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        for (var attempt = 1; attempt <= KeyAllocationAttempts; attempt++)
        {
            var project = await _projects.GetByIdAsync(model.ProjectId, cancellationToken);
            if (project is null)
            {
                return OperationResult<string>.Failure("프로젝트를 찾을 수 없습니다.");
            }

            var number = project.LastIssueNumber + 1;
            project.LastIssueNumber = number;

            var issue = new Issue
            {
                ProjectId = project.Id,
                Number = number,
                Key = Issue.FormatKey(project.Key, number),
                Title = title,
                DescriptionMarkdown = Trimmed(model.DescriptionMarkdown),
                Type = model.Type,
                Status = model.Status,
                Priority = model.Priority,
                AssigneeId = model.AssigneeId,
                ReporterId = reporter.Id,
                DueDate = model.DueDate,
                Environment = Trimmed(model.Environment),
                Version = Trimmed(model.Version),
                CreatedAt = now,
                UpdatedAt = now,
            };

            await _issues.AddAsync(issue, cancellationToken);

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return OperationResult<string>.Success(issue.Key);
            }
            catch (Exception ex) when (attempt < KeyAllocationAttempts)
            {
                // 번호 경합으로 보고 다시 읽는다. 마지막 시도의 예외는 흡수하지 않는다 —
                // 원인이 경합이 아니라면 조용히 삼키는 것이 더 나쁘다.
                _logger.LogWarning(
                    ex,
                    "이슈 번호 발급 재시도 {Attempt}/{Max} (ProjectId={ProjectId}, Number={Number})",
                    attempt,
                    KeyAllocationAttempts,
                    model.ProjectId,
                    number);

                _unitOfWork.DiscardChanges();
            }
        }

        return OperationResult<string>.Failure("이슈 번호를 발급하지 못했습니다. 잠시 후 다시 시도하세요.");
    }

    public async Task<OperationResult> UpdateAsync(
        Guid id,
        IssueEditModel model,
        CancellationToken cancellationToken = default)
    {
        var issue = await _issues.GetWithRelationsAsync(id, cancellationToken);
        if (issue is null)
        {
            return OperationResult.Failure("이슈를 찾을 수 없습니다.");
        }

        // 이슈를 다른 프로젝트로 옮기면 키(DEV-12)의 접두어가 프로젝트와 어긋난다.
        // 옮기려면 번호를 새로 발급해야 하므로 MVP 범위 밖이다.
        if (model.ProjectId != issue.ProjectId)
        {
            return OperationResult.Failure(
                "이슈를 다른 프로젝트로 옮길 수 없습니다. 이슈 키가 현재 프로젝트에 묶여 있습니다.");
        }

        var title = (model.Title ?? string.Empty).Trim();

        var errors = await ValidateAsync(model, title, cancellationToken);
        if (errors.Count > 0)
        {
            return OperationResult.Failure([.. errors]);
        }

        issue.Title = title;
        issue.DescriptionMarkdown = Trimmed(model.DescriptionMarkdown);
        issue.Type = model.Type;
        issue.Status = model.Status;
        issue.Priority = model.Priority;
        issue.AssigneeId = model.AssigneeId;
        issue.DueDate = model.DueDate;
        issue.Environment = Trimmed(model.Environment);
        issue.Version = Trimmed(model.Version);
        issue.UpdatedAt = DateTimeOffset.UtcNow;

        _issues.Update(issue);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return OperationResult.Success();
    }

    public async Task<OperationResult> ChangeStatusAsync(
        Guid id,
        IssueStatus status,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(status))
        {
            return OperationResult.Failure("알 수 없는 상태입니다.");
        }

        var issue = await _issues.GetWithRelationsAsync(id, cancellationToken);
        if (issue is null)
        {
            return OperationResult.Failure("이슈를 찾을 수 없습니다.");
        }

        if (issue.Status == status)
        {
            // 같은 상태로의 변경은 성공으로 본다. UpdatedAt 을 건드리면 목록 정렬이 흔들린다.
            return OperationResult.Success();
        }

        issue.Status = status;
        issue.UpdatedAt = DateTimeOffset.UtcNow;

        _issues.Update(issue);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return OperationResult.Success();
    }

    public async Task<OperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var issue = await _issues.GetByIdAsync(id, cancellationToken);
        if (issue is null)
        {
            return OperationResult.Failure("이슈를 찾을 수 없습니다.");
        }

        // 댓글·첨부는 FK CASCADE 로 함께 사라진다. 발급된 번호는 재사용하지 않는다
        // (Project.LastIssueNumber 를 되돌리지 않는다) — 지워진 키가 다른 이슈로 되살아나면
        // 과거 링크·문서가 엉뚱한 이슈를 가리킨다.
        _issues.Remove(issue);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return OperationResult.Success();
    }

    private async Task<List<string>> ValidateAsync(
        IssueEditModel model,
        string title,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(title))
        {
            errors.Add("제목을 입력하세요.");
        }
        else if (title.Length > DomainConstants.Lengths.IssueTitle)
        {
            errors.Add($"제목은 {DomainConstants.Lengths.IssueTitle}자를 넘을 수 없습니다.");
        }

        if (model.ProjectId == Guid.Empty)
        {
            errors.Add("프로젝트를 선택하세요.");
        }
        else if (await _projects.GetByIdAsync(model.ProjectId, cancellationToken) is null)
        {
            errors.Add("프로젝트를 찾을 수 없습니다.");
        }

        if (model.AssigneeId is { } assigneeId
            && !await _users.IsActiveAsync(assigneeId, cancellationToken))
        {
            errors.Add("담당자로 지정할 수 없는 계정입니다(존재하지 않거나 비활성).");
        }

        if (!Enum.IsDefined(model.Type) || !Enum.IsDefined(model.Status) || !Enum.IsDefined(model.Priority))
        {
            errors.Add("유형·상태·우선순위 값이 올바르지 않습니다.");
        }

        errors.AddRange(LengthErrors(model));

        return errors;
    }

    private static IEnumerable<string> LengthErrors(IssueEditModel model)
    {
        if (model.Environment?.Trim().Length > DomainConstants.Lengths.EnvironmentName)
        {
            yield return $"환경은 {DomainConstants.Lengths.EnvironmentName}자를 넘을 수 없습니다.";
        }

        if (model.Version?.Trim().Length > DomainConstants.Lengths.VersionLabel)
        {
            yield return $"버전은 {DomainConstants.Lengths.VersionLabel}자를 넘을 수 없습니다.";
        }
    }

    private static IssueListItem ToListItem(Issue issue) =>
        new(
            issue.Id,
            issue.Key,
            issue.Title,
            issue.Type,
            issue.Status,
            issue.Priority,
            issue.Project?.Key ?? string.Empty,
            issue.Assignee?.DisplayName,
            issue.DueDate,
            issue.Environment,
            issue.Version,
            issue.UpdatedAt);

    private static IssueDetail ToDetail(Issue issue) =>
        new(
            issue.Id,
            issue.Key,
            issue.ProjectId,
            issue.Project?.Key ?? string.Empty,
            issue.Project?.Name ?? string.Empty,
            issue.Title,
            issue.DescriptionMarkdown,
            issue.Type,
            issue.Status,
            issue.Priority,
            issue.AssigneeId,
            issue.Assignee?.DisplayName,
            issue.Reporter?.DisplayName,
            issue.DueDate,
            issue.Environment,
            issue.Version,
            issue.CreatedAt,
            issue.UpdatedAt);

    /// <summary>URL 로 들어온 키를 저장된 형태(대문자)로 맞춘다.</summary>
    private static string NormalizeKey(string? key) =>
        (key ?? string.Empty).Trim().ToUpperInvariant();

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
