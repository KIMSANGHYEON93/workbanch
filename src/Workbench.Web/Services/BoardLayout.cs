using Workbench.Domain.Enums;

namespace Workbench.Web.Services;

/// <summary>
/// 칸반 보드의 열 구성. 순서를 <c>Enum.GetValues</c> 에 맡기지 않고 여기서 못박는다 —
/// enum 값 순서는 저장 형식이고, 보드의 순서는 업무 흐름이다. 둘이 같아 보여도 이유가 다르다.
/// </summary>
public static class BoardLayout
{
    private static readonly IssueStatus[] Order =
    [
        IssueStatus.Backlog,
        IssueStatus.Todo,
        IssueStatus.InProgress,
        IssueStatus.InReview,
        IssueStatus.Done,
        IssueStatus.Cancelled,
    ];

    public static IReadOnlyList<IssueStatus> Columns => Order;

    /// <summary>열 위치. 알 수 없는 상태면 -1.</summary>
    public static int IndexOf(IssueStatus status) => Array.IndexOf(Order, status);

    /// <summary>한 칸 오른쪽. 마지막 열이면 <c>null</c>.</summary>
    public static IssueStatus? Next(IssueStatus status) => Neighbour(status, offset: 1);

    /// <summary>한 칸 왼쪽. 첫 열이면 <c>null</c>.</summary>
    public static IssueStatus? Previous(IssueStatus status) => Neighbour(status, offset: -1);

    private static IssueStatus? Neighbour(IssueStatus status, int offset)
    {
        var index = IndexOf(status);
        if (index < 0)
        {
            return null;
        }

        var target = index + offset;

        return target >= 0 && target < Order.Length ? Order[target] : null;
    }
}
