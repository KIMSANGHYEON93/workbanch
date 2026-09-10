using System.ComponentModel.DataAnnotations;

namespace Workbench.Web.Services;

public enum AuthenticationMode
{
    /// <summary>Microsoft Entra ID (OpenID Connect). 운영에서 쓰는 유일한 모드.</summary>
    EntraId = 0,

    /// <summary>고정 사용자로 항상 로그인된 상태를 만든다. 로컬 개발 전용.</summary>
    Development = 1,
}

public class WorkbenchAuthenticationOptions
{
    public const string SectionName = "Authentication";

    public AuthenticationMode Mode { get; set; } = AuthenticationMode.EntraId;

    public DevelopmentUserOptions DevelopmentUser { get; set; } = new();
}

public class DevelopmentUserOptions
{
    /// <summary>Entra Object Id 자리. 개발 DB 에서 사용자 행을 안정적으로 재사용하려고 고정한다.</summary>
    public Guid Id { get; set; } = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Required]
    public string DisplayName { get; set; } = "개발자 (로컬)";

    [Required]
    [EmailAddress]
    public string Email { get; set; } = "dev@localhost";
}
