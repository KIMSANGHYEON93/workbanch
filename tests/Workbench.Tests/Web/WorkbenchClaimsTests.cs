using System.Security.Claims;
using Workbench.Web.Services;

namespace Workbench.Tests.Web;

public class WorkbenchClaimsTests
{
    private static readonly Guid ObjectId = Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff");

    [Fact]
    public void AnonymousPrincipal_MapsToAnonymous()
    {
        var result = WorkbenchClaims.ToCurrentUser(new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.False(result.IsAuthenticated);
        Assert.Equal(Guid.Empty, result.Id);
    }

    [Fact]
    public void NullPrincipal_MapsToAnonymous()
    {
        Assert.False(WorkbenchClaims.ToCurrentUser(null).IsAuthenticated);
    }

    [Theory]
    [InlineData(WorkbenchClaims.ObjectId)]
    [InlineData("oid")]
    public void EntraObjectId_BecomesTheUserId(string idClaimType)
    {
        var principal = Authenticated(
            new Claim(idClaimType, ObjectId.ToString()),
            new Claim("name", "홍길동"),
            new Claim("preferred_username", "gildong@example.com"));

        var result = WorkbenchClaims.ToCurrentUser(principal);

        Assert.True(result.IsAuthenticated);
        Assert.Equal(ObjectId, result.Id);
        Assert.Equal("홍길동", result.DisplayName);
        Assert.Equal("gildong@example.com", result.Email);
    }

    [Fact]
    public void RealisticEntraIdToken_UsesOidNotTheAppScopedSub()
    {
        // Entra id_token 은 앱마다 다른(pairwise) sub 를 함께 싣고, 기본 클레임 매핑이 이것을
        // NameIdentifier 로 바꿔 놓는다. sub 가 GUID 모양이어도 사용자 Id 는 oid 여야 한다 —
        // 그렇지 않으면 앱 등록을 바꿀 때마다 같은 사람이 다른 사용자로 프로비저닝된다.
        var sub = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var principal = Authenticated(
            new Claim(ClaimTypes.NameIdentifier, sub.ToString()),
            new Claim(WorkbenchClaims.ObjectId, ObjectId.ToString()),
            new Claim("http://schemas.microsoft.com/identity/claims/tenantid", Guid.NewGuid().ToString()),
            new Claim("name", "홍길동"),
            new Claim("preferred_username", "gildong@example.com"));

        var result = WorkbenchClaims.ToCurrentUser(principal);

        Assert.Equal(ObjectId, result.Id);
        Assert.NotEqual(sub, result.Id);
    }

    [Fact]
    public void RealisticEntraIdToken_WithOnlyANonGuidSub_FailsClosed()
    {
        // oid 가 빠진 토큰(범위 구성 실수 등)에서 sub 로 추측해 통과시키지 않는다.
        var principal = Authenticated(
            new Claim(ClaimTypes.NameIdentifier, "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"),
            new Claim("name", "홍길동"),
            new Claim("preferred_username", "gildong@example.com"));

        Assert.False(WorkbenchClaims.ToCurrentUser(principal).IsAuthenticated);
    }

    [Fact]
    public void MissingDisplayName_FallsBackToEmail()
    {
        var principal = Authenticated(
            new Claim(WorkbenchClaims.ObjectId, ObjectId.ToString()),
            new Claim("email", "gildong@example.com"));

        Assert.Equal("gildong@example.com", WorkbenchClaims.ToCurrentUser(principal).DisplayName);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("")]
    public void UnusableObjectId_FailsClosed(string idClaimValue)
    {
        // 사용자를 특정할 수 없으면 통과시키지 않는다 — 잘못된 Id 로 프로비저닝하면
        // 담당자/작성자 이력이 다른 사람에게 붙는다.
        var principal = Authenticated(
            new Claim(WorkbenchClaims.ObjectId, idClaimValue),
            new Claim("name", "홍길동"));

        Assert.False(WorkbenchClaims.ToCurrentUser(principal).IsAuthenticated);
    }

    [Fact]
    public void AuthenticatedPrincipalWithNoIdClaim_FailsClosed()
    {
        var principal = Authenticated(new Claim("name", "홍길동"));

        Assert.False(WorkbenchClaims.ToCurrentUser(principal).IsAuthenticated);
    }

    private static ClaimsPrincipal Authenticated(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "TestScheme"));
}
