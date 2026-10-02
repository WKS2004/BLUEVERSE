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
    public void CurrentUserId_FromAuthenticatedClaimsPrincipal_ReturnsParsedGuid()
    {
        var expectedId = Guid.NewGuid();
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", expectedId.ToString()),
            new Claim(ClaimTypes.Role, "tourist"),
            new Claim("permission", "experiences.catalogue.read")
        ], "TestAuth");

        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var userContext = new UserContext(new HttpContextAccessor { HttpContext = httpContext });

        Assert.True(userContext.IsAuthenticated);
        Assert.Equal(expectedId, userContext.CurrentUserId);
        Assert.Contains("tourist", userContext.Roles);
        Assert.Contains("experiences.catalogue.read", userContext.Permissions);
    }

    [Fact]
    [Trait("CaseId", "EXP-UNIT-USR-002")]
    public void CurrentUserId_FromUserHeaders_IsNotTrusted()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Id"] = Guid.NewGuid().ToString();
        httpContext.Request.Headers["X-User-Roles"] = "Admin";
        httpContext.Request.Headers["X-User-Permissions"] = "experiences.catalogue.manage";
        var userContext = new UserContext(new HttpContextAccessor { HttpContext = httpContext });

        Assert.False(userContext.IsAuthenticated);
        Assert.Null(userContext.CurrentUserId);
        Assert.Empty(userContext.Roles);
        Assert.Empty(userContext.Permissions);
    }

    [Fact]
    [Trait("CaseId", "EXP-UNIT-USR-003")]
    public void CurrentUserId_FromUnsignedBearerJwt_IsNotTrusted()
    {
        var payloadJson = JsonSerializer.Serialize(new
        {
            sub = Guid.NewGuid().ToString(),
            role = new[] { "Admin" },
            permission = new[] { "experiences.catalogue.manage", "auth.role.system.manage" }
        });
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {CreateFakeJwt(payloadJson)}";
        var userContext = new UserContext(new HttpContextAccessor { HttpContext = httpContext });

        Assert.False(userContext.IsAuthenticated);
        Assert.Null(userContext.CurrentUserId);
        Assert.Empty(userContext.Roles);
        Assert.Empty(userContext.Permissions);
    }

    [Fact]
    [Trait("CaseId", "EXP-UNIT-USR-004")]
    public void CurrentUserId_FromUnsignedCookieJwt_IsNotTrusted()
    {
        var payloadJson = JsonSerializer.Serialize(new
        {
            sub = Guid.NewGuid().ToString(),
            role = "Admin",
            permission = "auth.role.system.manage"
        });
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Cookie"] = $"blueverse_access_token={CreateFakeJwt(payloadJson)}";
        var userContext = new UserContext(new HttpContextAccessor { HttpContext = httpContext });

        Assert.False(userContext.IsAuthenticated);
        Assert.Null(userContext.CurrentUserId);
        Assert.Empty(userContext.Roles);
        Assert.Empty(userContext.Permissions);
    }

    [Fact]
    [Trait("CaseId", "EXP-UNIT-USR-005")]
    public void UnauthenticatedContext_ReturnsNullIdAndEmptyCollections()
    {
        var userContext = new UserContext(new HttpContextAccessor { HttpContext = new DefaultHttpContext() });

        Assert.False(userContext.IsAuthenticated);
        Assert.Null(userContext.CurrentUserId);
        Assert.Empty(userContext.Roles);
        Assert.Empty(userContext.Permissions);
    }

    [Fact]
    [Trait("CaseId", "EXP-UNIT-USR-006")]
    public void MalformedJwt_DoesNotCreateAnAuthenticatedPrincipal()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = "Bearer not-a-valid-jwt-token";
        var userContext = new UserContext(new HttpContextAccessor { HttpContext = httpContext });

        Assert.False(userContext.IsAuthenticated);
        Assert.Null(userContext.CurrentUserId);
        Assert.Empty(userContext.Roles);
        Assert.Empty(userContext.Permissions);
    }

    [Fact]
    [Trait("CaseId", "EXP-UNIT-USR-007")]
    public void HasPermission_AdminRoleWithoutPermission_DoesNotGrantAccess()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "Admin")
        ], "TestAuth");
        var userContext = new UserContext(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        });

        Assert.False(userContext.HasPermission("experiences.catalogue.manage"));
        Assert.False(userContext.HasAnyPermission("experiences.catalogue.manage", "something.else"));
    }

    [Fact]
    [Trait("CaseId", "EXP-UNIT-USR-008")]
    public void HasPermission_UserHeadersDoNotGrantAccess()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Id"] = Guid.NewGuid().ToString();
        httpContext.Request.Headers["X-User-Roles"] = "tourist";
        httpContext.Request.Headers["X-User-Permissions"] = "experiences.catalogue.read";
        var userContext = new UserContext(new HttpContextAccessor { HttpContext = httpContext });

        Assert.False(userContext.HasPermission("experiences.catalogue.read"));
        Assert.False(userContext.HasPermission("experiences.catalogue.manage"));
        Assert.False(userContext.HasAnyPermission("auth.role.system.manage", "admin.privilege"));
    }

    [Fact]
    [Trait("CaseId", "EXP-UNIT-USR-009")]
    public void HasPermission_UsesSignedPermissionClaimsAndExplicitCatalogueHierarchy()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim("permission", "experiences.catalogue.manage")
        ], "TestAuth");
        var userContext = new UserContext(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        });

        Assert.True(userContext.HasPermission("experiences.catalogue.manage"));
        Assert.True(userContext.HasPermission("experiences.catalogue.read"));
        Assert.False(userContext.HasPermission("auth.role.manage"));
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
