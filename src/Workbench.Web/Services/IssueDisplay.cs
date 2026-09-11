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

    /// <summary>
    /// ⚠ <c>text-bg-*</c> 를 쓰지 않는다. wwwroot 에 번들된 Bootstrap 은 <b>5.1</b> 이고 그 유틸리티는
    /// 5.2 에서 들어왔다 — 5.1 에서는 클래스가 존재하지 않는데 <c>.badge</c> 가 <c>color:#fff</c> 를
    /// 걸어 두어, 배경 없는 흰 글자가 된다. 즉 <b>모든 배지가 화면에서 사라진다</b>.
    /// 배경과 글자색을 따로 지정하는 이유이고, 계약 테스트가 여기서 나오는 클래스들이
    /// 번들 CSS 에 실재하는지 확인한다.
    /// </summary>
    public static string BadgeClass(IssueStatus status) => status switch
    {
        IssueStatus.Backlog => "bg-secondary",
        IssueStatus.Todo => "bg-light text-dark",
        IssueStatus.InProgress => "bg-primary",
        IssueStatus.InReview => "bg-info text-dark",
        IssueStatus.Done => "bg-success",
        IssueStatus.Cancelled => "bg-dark",
        _ => "bg-secondary",
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
        IssuePriority.Lowest or IssuePriority.Low => "bg-light text-dark",
        IssuePriority.Medium => "bg-secondary",
        IssuePriority.High => "bg-warning text-dark",
        IssuePriority.Highest => "bg-danger",
        _ => "bg-secondary",
    };
}
