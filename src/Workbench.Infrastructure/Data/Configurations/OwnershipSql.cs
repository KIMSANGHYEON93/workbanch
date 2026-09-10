namespace Workbench.Infrastructure.Data.Configurations;

/// <summary>
/// Comment 와 Attachment 는 이슈 또는 페이지 중 <b>정확히 한쪽</b>에 속한다.
/// 두 테이블이 같은 규칙을 쓰므로 SQL 을 복제하지 않고 한곳에서 만든다 —
/// 한쪽만 고쳐 두 테이블의 제약이 갈라지는 것을 막기 위함.
/// </summary>
internal static class OwnershipSql
{
    public static string ExactlyOne(string firstColumn, string secondColumn) =>
        $"(CASE WHEN [{firstColumn}] IS NULL THEN 0 ELSE 1 END + " +
        $"CASE WHEN [{secondColumn}] IS NULL THEN 0 ELSE 1 END) = 1";
}
