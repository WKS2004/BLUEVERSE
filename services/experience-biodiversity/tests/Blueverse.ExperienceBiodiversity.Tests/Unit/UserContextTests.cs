using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Blueverse.ExperienceBiodiversity.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Blueverse.ExperienceBiodiversity.Tests.Unit;

public sealed class UserContextTests
{
    [Fact]
    [Trait("CaseId", "EXP-UNIT-USR-001")]
    public void CurrentUserId_FromClaimsPrincipal_ReturnsParsedGuid()
    {
        var expectedId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim("sub", expectedId.ToString()),
            new Claim(ClaimTypes.Role, "tourist"),
            new Claim("permission", "experiences.read")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var userContext = new UserContext(accessor);

        Assert.True(userContext.IsAuthenticated);
        Assert.Equal(expectedId, userContext.CurrentUserId);
        Assert.Contains("tourist", userContext.Roles);
        Assert.Contains("experiences.read", userContext.Permissions);
    }

    [Fact]
    [Trait("CaseId", "EXP-UNIT-USR-002")]
    public void CurrentUserId_FromHeader_ReturnsParsedGuid()
    {
        var expectedId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Id"] = expectedId.ToString();

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var userContext = new UserContext(accessor);

        Assert.True(userContext.IsAuthenticated);
        Assert.Equal(expectedId, userContext.CurrentUserId);
    }

    [Fact]
    [Trait("CaseId", "EXP-UNIT-USR-003")]
    public void CurrentUserId_FromBearerJwtHeader_ExtractsSubAndRoles()
    {
        var expectedId = Guid.NewGuid();
        var payloadJson = JsonSerializer.Serialize(new
        {
            sub = expectedId.ToString(),
            role = new[] { "community_lead", "contributor" },
            permission = new[] { "destinations.manage", "experiences.publish" }
        });
        var token = CreateFakeJwt(payloadJson);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {token}";

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var userContext = new UserContext(accessor);

        Assert.True(userContext.IsAuthenticated);
        Assert.Equal(expectedId, userContext.CurrentUserId);
        Assert.Contains("community_lead", userContext.Roles);
        Assert.Contains("contributor", userContext.Roles);
        Assert.Contains("destinations.manage", userContext.Permissions);
        Assert.Contains("experiences.publish", userContext.Permissions);
    }

    [Fact]
    [Trait("CaseId", "EXP-UNIT-USR-004")]
    public void CurrentUserId_FromCookieJwt_ExtractsSubAndSingleRole()
    {
        var expectedId = Guid.NewGuid();
        var payloadJson = JsonSerializer.Serialize(new
        {
            sub = expectedId.ToString(),
            role = "admin",
            permission = "system.admin"
        });
        var token = CreateFakeJwt(payloadJson);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Cookie"] = $"blueverse_access_token={token}";

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var userContext = new UserContext(accessor);

        Assert.True(userContext.IsAuthenticated);
        Assert.Equal(expectedId, userContext.CurrentUserId);
        Assert.Contains("admin", userContext.Roles);
        Assert.Contains("system.admin", userContext.Permissions);
    }

    [Fact]
    [Trait("CaseId", "EXP-UNIT-USR-005")]
    public void UnauthenticatedContext_ReturnsNullIdAndEmptyCollections()
    {
        var httpContext = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var userContext = new UserContext(accessor);

        Assert.False(userContext.IsAuthenticated);
        Assert.Null(userContext.CurrentUserId);
        Assert.Empty(userContext.Roles);
        Assert.Empty(userContext.Permissions);
    }

    [Fact]
    [Trait("CaseId", "EXP-UNIT-USR-006")]
    public void MalformedJwt_DoesNotThrowAndReturnsNullId()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = "Bearer not-a-valid-jwt-token";

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var userContext = new UserContext(accessor);

        Assert.False(userContext.IsAuthenticated);
        Assert.Null(userContext.CurrentUserId);
        Assert.Empty(userContext.Roles);
        Assert.Empty(userContext.Permissions);
    }

    [Fact]
    [Trait("CaseId", "EXP-UNIT-USR-007")]
    public void HasPermission_WithAdminRole_ReturnsTrue()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Id"] = Guid.NewGuid().ToString();
        httpContext.Request.Headers["X-User-Roles"] = "Admin";

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var userContext = new UserContext(accessor);

        Assert.True(userContext.HasPermission("experiences.catalogue.manage"));
        Assert.True(userContext.HasAnyPermission("experiences.catalogue.manage", "something.else"));
    }

    [Fact]
    [Trait("CaseId", "EXP-UNIT-USR-008")]
    public void HasPermission_WithoutRequiredPermission_ReturnsFalse()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Id"] = Guid.NewGuid().ToString();
        httpContext.Request.Headers["X-User-Roles"] = "tourist";
        httpContext.Request.Headers["X-User-Permissions"] = "experiences.catalogue.read";

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var userContext = new UserContext(accessor);

        Assert.True(userContext.HasPermission("experiences.catalogue.read"));
        Assert.False(userContext.HasPermission("experiences.catalogue.manage"));
        Assert.False(userContext.HasAnyPermission("auth.role.system.manage", "admin.privilege"));
    }

    private static string CreateFakeJwt(string payloadJson)
    {
        var headerBytes = Encoding.UTF8.GetBytes("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);

        var headerB64 = Convert.ToBase64String(headerBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var payloadB64 = Convert.ToBase64String(payloadBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        return $"{headerB64}.{payloadB64}.fakeSignature";
    }
}
