using Workbench.Domain.Enums;
using Workbench.Web.Services;

namespace Workbench.Tests.Web;

public class BoardLayoutTests
{
    [Fact]
    public void EveryStatus_HasExactlyOneColumn()
    {
        // 열이 빠지면 그 상태의 이슈가 보드에서 사라진다 — 목록에는 있는데 보드에는 없는 상태가 된다.
        Assert.Equal(
            Enum.GetValues<IssueStatus>().OrderBy(s => s),
            BoardLayout.Columns.OrderBy(s => s));

        Assert.Equal(BoardLayout.Columns.Count, BoardLayout.Columns.Distinct().Count());
    }

    [Fact]
    public void ColumnsFollowTheWorkflowOrder()
    {
        Assert.Equal(
            [
                IssueStatus.Backlog,
                IssueStatus.Todo,
                IssueStatus.InProgress,
                IssueStatus.InReview,
                IssueStatus.Done,
                IssueStatus.Cancelled,
            ],
            BoardLayout.Columns);
    }

    [Fact]
    public void FirstColumn_HasNoLeftNeighbour()
    {
        Assert.Null(BoardLayout.Previous(BoardLayout.Columns[0]));
    }

    [Fact]
    public void LastColumn_HasNoRightNeighbour()
    {
        Assert.Null(BoardLayout.Next(BoardLayout.Columns[^1]));
    }

    [Fact]
    public void NeighboursAreSymmetric()
    {
        foreach (var status in BoardLayout.Columns)
        {
            if (BoardLayout.Next(status) is { } next)
            {
                Assert.Equal(status, BoardLayout.Previous(next));
            }
        }
    }

    [Fact]
    public void IndexOf_ReportsTheColumnPosition()
    {
        Assert.Equal(0, BoardLayout.IndexOf(BoardLayout.Columns[0]));
        Assert.Equal(BoardLayout.Columns.Count - 1, BoardLayout.IndexOf(BoardLayout.Columns[^1]));
        Assert.Equal(-1, BoardLayout.IndexOf((IssueStatus)99));
    }

    [Fact]
    public void UnknownStatus_HasNoNeighbours()
    {
        Assert.Null(BoardLayout.Next((IssueStatus)99));
        Assert.Null(BoardLayout.Previous((IssueStatus)99));
    }
}
