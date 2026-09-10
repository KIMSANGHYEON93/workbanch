using Workbench.Domain.Enums;

namespace Workbench.Web.Services;

/// <summary>
/// Enum 의 화면 표기. 한곳에 모아 두어 목록·상세·보드가 같은 낱말과 같은 색을 쓰게 한다.
/// 새 값을 추가하면 여기서 빠뜨리기 쉬우므로, 계약 테스트가 누락을 잡는다.
/// </summary>
public static class IssueDisplay
{
    public static string Label(IssueStatus status) => status switch
    {
        IssueStatus.Backlog => "백로그",
        IssueStatus.Todo => "할 일",
        IssueStatus.InProgress => "진행 중",
        IssueStatus.InReview => "검토 중",
        IssueStatus.Done => "완료",
        IssueStatus.Cancelled => "취소",
        _ => status.ToString(),
    };

    public static string BadgeClass(IssueStatus status) => status switch
    {
        IssueStatus.Backlog => "text-bg-secondary",
        IssueStatus.Todo => "text-bg-light",
        IssueStatus.InProgress => "text-bg-primary",
        IssueStatus.InReview => "text-bg-info",
        IssueStatus.Done => "text-bg-success",
        IssueStatus.Cancelled => "text-bg-dark",
        _ => "text-bg-secondary",
    };

    public static string Label(IssueType type) => type switch
    {
        IssueType.Task => "작업",
        IssueType.Bug => "버그",
        IssueType.Story => "스토리",
        IssueType.Epic => "에픽",
        _ => type.ToString(),
    };

    public static string Label(IssuePriority priority) => priority switch
    {
        IssuePriority.Lowest => "매우 낮음",
        IssuePriority.Low => "낮음",
        IssuePriority.Medium => "보통",
        IssuePriority.High => "높음",
        IssuePriority.Highest => "매우 높음",
        _ => priority.ToString(),
    };

    public static string BadgeClass(IssuePriority priority) => priority switch
    {
        IssuePriority.Lowest or IssuePriority.Low => "text-bg-light",
        IssuePriority.Medium => "text-bg-secondary",
        IssuePriority.High => "text-bg-warning",
        IssuePriority.Highest => "text-bg-danger",
        _ => "text-bg-secondary",
    };
}
