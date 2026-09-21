using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Blueverse.Auth.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Blueverse.Auth.Tests.Unit;

public sealed class AuthInfrastructureTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("61")]
    [InlineData("not-a-number")]
    [Trait("TestId", "AUTH-CONFIG-002")]
    public void InvalidAccessTokenLifetimeSettingsAreRejected(string configuredMinutes)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_SIGNING_KEY"] = "auth-test-only-signing-key-with-at-least-32-bytes",
                ["Jwt:AccessTokenMinutes"] = configuredMinutes
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() => new JwtTokenService(configuration));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(60)]
    [Trait("TestId", "AUTH-JWT-001")]
    public void GeneratedAccessTokensContainTheSessionAndAuthorizationClaims(int configuredMinutes)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_SIGNING_KEY"] = "auth-test-only-signing-key-with-at-least-32-bytes",
                ["Jwt:Issuer"] = "Test.Auth",
                ["Jwt:Audience"] = "Test.Client",
                ["Jwt:AccessTokenMinutes"] = configuredMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture)
            })
            .Build();
        var service = new JwtTokenService(configuration);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "claims@blueverse.local",
            FullName = "Claims User",
            PasswordHash = "not-used",
            TokenVersion = 4
        };
        var sessionId = Guid.NewGuid();

        var issuedAt = DateTime.UtcNow;
        var result = service.GenerateToken(
            user,
            sessionId,
            sessionVersion: 2,
            roles: ["Admin"],
            permissions: ["auth.user.read"]);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);

        Assert.Equal("Test.Auth", token.Issuer);
        Assert.Contains("Test.Client", token.Audiences);
        Assert.Equal(SecurityAlgorithms.HmacSha256, token.Header.Alg);
        Assert.Equal(user.Id.ToString(), token.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("4", token.Claims.Single(claim => claim.Type == "token_version").Value);
        Assert.Equal(sessionId.ToString(), token.Claims.Single(claim => claim.Type == "session_id").Value);
        Assert.Equal("2", token.Claims.Single(claim => claim.Type == "session_version").Value);
        Assert.Contains(token.Claims, claim =>
            (claim.Type == ClaimTypes.Role || claim.Type == "role") && claim.Value == "Admin");
        Assert.Contains(token.Claims, claim => claim.Type == "permission" && claim.Value == "auth.user.read");
        Assert.InRange(
            result.ExpiresAt,
            issuedAt.AddMinutes(configuredMinutes),
            DateTime.UtcNow.AddMinutes(configuredMinutes));
    }

    [Fact]
    [Trait("TestId", "AUTH-REFRESH-UNIT-001")]
    public void OpaqueSecretsAreUrlSafeAndOnlyTheirHashIsDeterministic()
    {
        var service = new RefreshTokenService();
        var token = service.CreateOpaqueToken();
        var secondToken = service.CreateOpaqueToken();
        var deviceId = service.CreateDeviceId();
        var deviceKey = service.CreateDeviceKey();

        Assert.NotEqual(token, secondToken);
        Assert.DoesNotContain("+", token);
        Assert.DoesNotContain("/", token);
        Assert.DoesNotContain("=", token);
        Assert.Equal(43, token.Length);
        Assert.Equal(32, deviceId.Length);
        Assert.True(Guid.TryParseExact(deviceId, "N", out _));
        Assert.NotEqual(token, deviceKey);
        Assert.Equal(43, deviceKey.Length);
        Assert.DoesNotContain("+", deviceKey);
        Assert.DoesNotContain("/", deviceKey);
        Assert.DoesNotContain("=", deviceKey);
        var tokenHash = service.Hash(token);
        Assert.Equal(tokenHash, service.Hash(token));
        Assert.Matches("^[0-9A-F]{64}$", tokenHash);
        Assert.NotEqual(tokenHash, service.Hash(secondToken));
        Assert.Throws<ArgumentNullException>(() => service.Hash(null!));
        Assert.Throws<ArgumentException>(() => service.Hash(string.Empty));
        Assert.Throws<ArgumentException>(() => service.Hash("  "));
    }

    [Fact]
    [Trait("TestId", "AUTH-CONFIG-003")]
    public void SessionLifetimeConfigurationKeepsDefaultAndRememberMeModesWithinTheSupportedRange()
    {
        var options = new AuthSessionOptions
        {
            DefaultLifetimeDays = 1,
            RememberMeLifetimeDays = 30
        };

        Assert.Equal(TimeSpan.FromDays(1), options.GetLifetime(rememberMe: false));
        Assert.Equal(TimeSpan.FromDays(30), options.GetLifetime(rememberMe: true));

        options.DefaultLifetimeDays = 0;
        Assert.Throws<InvalidOperationException>(() => options.GetLifetime(rememberMe: false));
        options.DefaultLifetimeDays = -1;
        Assert.Throws<InvalidOperationException>(() => options.GetLifetime(rememberMe: false));
        options.DefaultLifetimeDays = 1;
        options.RememberMeLifetimeDays = 0;
        Assert.Throws<InvalidOperationException>(() => options.GetLifetime(rememberMe: true));
        options.RememberMeLifetimeDays = -1;
        Assert.Throws<InvalidOperationException>(() => options.GetLifetime(rememberMe: true));
        options.RememberMeLifetimeDays = 30;
        options.RememberMeLifetimeDays = 31;
        Assert.Throws<InvalidOperationException>(() => options.GetLifetime(rememberMe: true));
    }

    [Fact]
    [Trait("TestId", "AUTH-AUTHORIZATION-UNIT-001")]
    public async Task PermissionAuthorizationIsCaseInsensitiveButDoesNotSucceedWithoutTheClaim()
    {
        var requirement = new PermissionRequirement("auth.user.read");
        var handler = new PermissionHandler();
        var allowedContext = new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("permission", "AUTH.USER.READ")])),
            resource: null);
        var deniedContext = new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("permission", "auth.role.read")])),
            resource: null);

        await handler.HandleAsync(allowedContext);
        await handler.HandleAsync(deniedContext);

        Assert.True(allowedContext.HasSucceeded);
        Assert.False(deniedContext.HasSucceeded);
        Assert.False(deniedContext.HasFailed);
    }

    [Fact]
    [Trait("TestId", "AUTH-AUTHORIZATION-UNIT-002")]
    public async Task PermissionPolicyProviderCreatesPermissionPoliciesAndFallsBackForOtherPolicies()
    {
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));
        var permissionPolicy = await provider.GetPolicyAsync("PERMISSION:auth.user.read");
        var fallbackPolicy = await provider.GetPolicyAsync("SomeOtherPolicy");

        Assert.NotNull(permissionPolicy);
        Assert.Contains(permissionPolicy!.Requirements, requirement => requirement is PermissionRequirement);
        Assert.Contains(permissionPolicy.Requirements, requirement => requirement is DenyAnonymousAuthorizationRequirement);
        Assert.Null(fallbackPolicy);
    }

    [Fact]
    [Trait("TestId", "AUTH-DATABASE-CONTRACT-001")]
    public void SessionArchiveModelSeparatesActiveRowsAndEnforcesRefreshTokenOwnership()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase($"auth-model-{Guid.NewGuid():N}")
            .Options;
        using var db = new AuthDbContext(options);

        var active = db.Model.FindEntityType(typeof(UserSession));
        var log = db.Model.FindEntityType(typeof(UserSessionLog));
        var refresh = db.Model.FindEntityType(typeof(RefreshToken));

        Assert.NotNull(active);
        Assert.NotNull(log);
        Assert.NotNull(refresh);
        Assert.Equal("ActiveSessions", active!.GetTableName());
        Assert.Equal("UserSessionLogs", log!.GetTableName());
        Assert.Null(active.FindProperty(nameof(UserSessionLog.EndedAt)));
        Assert.NotNull(log.FindProperty(nameof(UserSessionLog.EndedAt)));
        Assert.True(active.GetIndexes().Single(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(UserSession.UserId), nameof(UserSession.DeviceId)])).IsUnique);
        var designTimeRefresh = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(RefreshToken));
        Assert.NotNull(designTimeRefresh);
        Assert.Contains(
            designTimeRefresh!.GetCheckConstraints(),
            constraint => constraint.Name == "CK_RefreshTokens_ExactlyOneSession" &&
                constraint.Sql?.Contains("UserSessionLogId", StringComparison.Ordinal) == true);
    }

    [Fact]
    [Trait("TestId", "AUTH-HEALTH-002")]
    public async Task HealthReturnsServiceUnavailableWhenTheDatabaseCannotBeReached()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=unavailable;Username=unavailable;Password=unavailable;Timeout=1;Command Timeout=1")
            .Options;
        await using var db = new AuthDbContext(options);
        var controller = new HealthController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var actionResult = await controller.Get();
        var result = Assert.IsType<ObjectResult>(actionResult);
        var body = JsonSerializer.SerializeToElement(result.Value);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Equal("auth", body.GetProperty("service").GetString());
        Assert.Equal("unhealthy", body.GetProperty("status").GetString());
        Assert.Equal("unavailable", body.GetProperty("database").GetString());
    }
}
