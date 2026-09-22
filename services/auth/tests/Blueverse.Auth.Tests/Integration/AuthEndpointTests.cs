using Blueverse.Auth.Tests.Fixtures;

namespace Blueverse.Auth.Tests.Integration;

public sealed class AuthEndpointTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public AuthEndpointTests(AuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        await _factory.SeedAdminAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    [Trait("TestId", "AUTH-ENDPOINT-001")]
    public async Task RegisterAndLoginReturnTokenWithoutExposingPasswordHash()
    {
        var email = UniqueEmail();
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "UserPassword-123!",
            fullName = "Registered User"
        });

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(registerBody.GetProperty("token").GetString() is { Length: > 0 });
        Assert.False(registerBody.ToString().Contains("passwordHash", StringComparison.OrdinalIgnoreCase));

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "UserPassword-123!"
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var invalidLoginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "WrongPassword-123!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, invalidLoginResponse.StatusCode);
        var invalidLoginBody = await invalidLoginResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Authentication Failed", invalidLoginBody.GetProperty("title").GetString());
    }

    [Fact]
    [Trait("TestId", "AUTH-LOGOUT-001")]
    public async Task LogoutRevokesAllIssuedTokensAndAllowsALaterLogin()
    {
        var anonymousLogout = await _client.PostAsync("/api/auth/logout", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousLogout.StatusCode);

        var registered = await RegisterAsync("Logout User");
        var secondToken = await LoginAsync(registered.Email, "UserPassword-123!");

        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logoutRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", registered.Token);
        var logoutResponse = await _client.SendAsync(logoutRequest);

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        Assert.Empty(await logoutResponse.Content.ReadAsStringAsync());

        foreach (var token in new[] { registered.Token, secondToken })
        {
            using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
            meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var revokedResponse = await _client.SendAsync(meRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, revokedResponse.StatusCode);
        }

        var reloginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = registered.Email,
            password = "UserPassword-123!"
        });

        Assert.Equal(HttpStatusCode.OK, reloginResponse.StatusCode);
        var reloginBody = await reloginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var newToken = reloginBody.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(newToken));
        Assert.Equal(registered.Email, reloginBody.GetProperty("user").GetProperty("email").GetString());
    }

    [Fact]
    [Trait("TestId", "AUTH-PASSWORD-002")]
    public async Task ChangePasswordRequiresCurrentPasswordAndRevokesTheOldToken()
    {
        var registered = await RegisterAsync("Password Change User");

        using var invalidRequest = AuthorizedRequest(
            HttpMethod.Post,
            "/api/auth/change-password",
            registered.Token,
            new
            {
                currentPassword = "WrongPassword-123!",
                newPassword = "NewPassword-456!"
            });
        var invalidResponse = await _client.SendAsync(invalidRequest);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        var invalidBody = await invalidResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Password Change Failed", invalidBody.GetProperty("title").GetString());

        using var changeRequest = AuthorizedRequest(
            HttpMethod.Post,
            "/api/auth/change-password",
            registered.Token,
            new
            {
                currentPassword = "UserPassword-123!",
                newPassword = "NewPassword-456!"
            });
        var changeResponse = await _client.SendAsync(changeRequest);
        Assert.Equal(HttpStatusCode.NoContent, changeResponse.StatusCode);
        Assert.Empty(await changeResponse.Content.ReadAsStringAsync());

        using var oldTokenRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        oldTokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", registered.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(oldTokenRequest)).StatusCode);

        var oldPasswordLogin = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = registered.Email,
            password = "UserPassword-123!"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLogin.StatusCode);

        var newPasswordLogin = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = registered.Email,
            password = "NewPassword-456!"
        });
        Assert.Equal(HttpStatusCode.OK, newPasswordLogin.StatusCode);
        var newPasswordBody = await newPasswordLogin.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(registered.Email, newPasswordBody.GetProperty("user").GetProperty("email").GetString());
        Assert.False(string.IsNullOrWhiteSpace(newPasswordBody.GetProperty("token").GetString()));
    }

    [Fact]
    [Trait("TestId", "AUTH-CATALOG-001")]
    public async Task RolesAndPermissionsCanBeReadIndividually()
    {
        var adminToken = await LoginAsync(AuthWebApplicationFactory.AdminEmail, AuthWebApplicationFactory.AdminPassword);
        var regularUser = await RegisterAsync("Catalog Reader");

        using var rolesRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/roles");
        rolesRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var rolesResponse = await _client.SendAsync(rolesRequest);
        Assert.Equal(HttpStatusCode.OK, rolesResponse.StatusCode);
        var rolesBody = await rolesResponse.Content.ReadFromJsonAsync<JsonElement>();
        var adminRole = rolesBody.EnumerateArray()
            .Single(role => role.GetProperty("name").GetString() == "Admin");
        var adminRoleId = adminRole.GetProperty("id").GetGuid();

        using var roleRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/auth/roles/{adminRoleId}");
        roleRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var roleResponse = await _client.SendAsync(roleRequest);
        Assert.Equal(HttpStatusCode.OK, roleResponse.StatusCode);
        var roleBody = await roleResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(adminRoleId, roleBody.GetProperty("id").GetGuid());
        Assert.Equal("Admin", roleBody.GetProperty("name").GetString());
        Assert.Contains("auth.user.read", roleBody.GetProperty("permissions").EnumerateArray().Select(item => item.GetString()));

        using var forbiddenRoleRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/auth/roles/{adminRoleId}");
        forbiddenRoleRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", regularUser.Token);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(forbiddenRoleRequest)).StatusCode);

        using var missingRoleRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/auth/roles/{Guid.NewGuid()}");
        missingRoleRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var missingRoleResponse = await _client.SendAsync(missingRoleRequest);
        Assert.Equal(HttpStatusCode.NotFound, missingRoleResponse.StatusCode);
        var missingRoleBody = await missingRoleResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Role Not Found", missingRoleBody.GetProperty("title").GetString());

        using var permissionsRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/permissions");
        permissionsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var permissionsResponse = await _client.SendAsync(permissionsRequest);
        Assert.Equal(HttpStatusCode.OK, permissionsResponse.StatusCode);
        var permissionsBody = await permissionsResponse.Content.ReadFromJsonAsync<JsonElement>();
        var userReadPermission = permissionsBody.EnumerateArray()
            .Single(permission => permission.GetProperty("code").GetString() == "auth.user.read");
        var permissionId = userReadPermission.GetProperty("id").GetGuid();

        using var permissionRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/auth/permissions/{permissionId}");
        permissionRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var permissionResponse = await _client.SendAsync(permissionRequest);
        Assert.Equal(HttpStatusCode.OK, permissionResponse.StatusCode);
        var permissionBody = await permissionResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(permissionId, permissionBody.GetProperty("id").GetGuid());
        Assert.Equal("auth.user.read", permissionBody.GetProperty("code").GetString());

        using var missingPermissionRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/auth/permissions/{Guid.NewGuid()}");
        missingPermissionRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var missingPermissionResponse = await _client.SendAsync(missingPermissionRequest);
        Assert.Equal(HttpStatusCode.NotFound, missingPermissionResponse.StatusCode);
        var missingPermissionBody = await missingPermissionResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Permission Not Found", missingPermissionBody.GetProperty("title").GetString());
    }

    [Fact]
    [Trait("TestId", "AUTH-MULTI-LOGIN-001")]
    public async Task ADeviceCanHaveFiveActiveAccountsButNotASixth()
    {
        var deviceId = $"shared-device-{Guid.NewGuid():N}";
        var accounts = new List<(string Token, Guid UserId, string Email, string DeviceId)>();

        for (var index = 1; index <= 5; index++)
        {
            accounts.Add(await RegisterWithDeviceAsync($"Device Account {index}", deviceId));
        }

        var sixthEmail = UniqueEmail();
        var sixthResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = sixthEmail,
            password = "UserPassword-123!",
            fullName = "Device Account 6",
            deviceId
        });

        Assert.Equal(HttpStatusCode.Conflict, sixthResponse.StatusCode);
        var sixthBody = await sixthResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Device Account Limit Reached", sixthBody.GetProperty("title").GetString());
        Assert.Contains("at most 5", sixthBody.GetProperty("detail").GetString());

        var sixthAccount = await RegisterWithDeviceAsync(
            "Device Account 6",
            $"different-device-{Guid.NewGuid():N}");
        var sixthLoginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = sixthAccount.Email,
            password = "UserPassword-123!",
            deviceId
        });
        Assert.Equal(HttpStatusCode.Conflict, sixthLoginResponse.StatusCode);
        var sixthLoginBody = await sixthLoginResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Device Account Limit Reached", sixthLoginBody.GetProperty("title").GetString());

        var repeatLoginToken = await LoginWithDeviceAsync(accounts[0].Email, "UserPassword-123!", deviceId);
        using var repeatLoginMeRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        repeatLoginMeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", repeatLoginToken);
        var repeatLoginMeResponse = await _client.SendAsync(repeatLoginMeRequest);
        Assert.Equal(HttpStatusCode.OK, repeatLoginMeResponse.StatusCode);
        var repeatLoginMeBody = await repeatLoginMeResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(accounts[0].UserId, repeatLoginMeBody.GetProperty("id").GetGuid());
    }

    [Fact]
    [Trait("TestId", "AUTH-LOGOUT-DEVICE-001")]
    public async Task LogoutAccountAndLogoutDeviceUseTheCurrentDeviceScope()
    {
        var deviceId = $"logout-device-{Guid.NewGuid():N}";
        var firstAccount = await RegisterWithDeviceAsync("First Device Account", deviceId);
        var secondAccount = await RegisterWithDeviceAsync("Second Device Account", deviceId);
        var thirdAccount = await RegisterWithDeviceAsync("Third Device Account", deviceId);
        var secondAccountOtherDeviceToken = await LoginWithDeviceAsync(
            secondAccount.Email,
            "UserPassword-123!",
            $"other-device-{Guid.NewGuid():N}");

        using var accountLogoutRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/auth/logout/{secondAccount.UserId}");
        accountLogoutRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", firstAccount.Token);
        var accountLogoutResponse = await _client.SendAsync(accountLogoutRequest);
        Assert.Equal(HttpStatusCode.NoContent, accountLogoutResponse.StatusCode);

        using var secondAccountRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        secondAccountRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secondAccount.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(secondAccountRequest)).StatusCode);

        var secondAccountReloginToken = await LoginWithDeviceAsync(
            secondAccount.Email,
            "UserPassword-123!",
            deviceId);
        using var secondAccountReloginRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        secondAccountReloginRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secondAccountReloginToken);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(secondAccountReloginRequest)).StatusCode);

        using var secondAccountOldTokenRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        secondAccountOldTokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secondAccount.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(secondAccountOldTokenRequest)).StatusCode);

        using var secondAccountOtherDeviceRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        secondAccountOtherDeviceRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secondAccountOtherDeviceToken);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(secondAccountOtherDeviceRequest)).StatusCode);

        using var firstAccountRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        firstAccountRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", firstAccount.Token);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(firstAccountRequest)).StatusCode);

        using var deviceLogoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        deviceLogoutRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", firstAccount.Token);
        var deviceLogoutResponse = await _client.SendAsync(deviceLogoutRequest);
        Assert.Equal(HttpStatusCode.NoContent, deviceLogoutResponse.StatusCode);

        using var thirdAccountRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        thirdAccountRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", thirdAccount.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(thirdAccountRequest)).StatusCode);

        using var otherDeviceStillActiveRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        otherDeviceStillActiveRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secondAccountOtherDeviceToken);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(otherDeviceStillActiveRequest)).StatusCode);
    }

    [Fact]
    [Trait("TestId", "AUTH-LOGOUT-ALL-001")]
    public async Task LogoutAllDevicesRevokesTheAccountButNotAnotherAccount()
    {
        var account = await RegisterWithDeviceAsync(
            "Everywhere Logout Account",
            $"primary-device-{Guid.NewGuid():N}");
        var secondDeviceToken = await LoginWithDeviceAsync(
            account.Email,
            "UserPassword-123!",
            $"secondary-device-{Guid.NewGuid():N}");
        var otherAccount = await RegisterWithDeviceAsync(
            "Unaffected Account",
            $"primary-device-{Guid.NewGuid():N}");

        using var logoutAllRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout-all-devices");
        logoutAllRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.Token);
        var logoutAllResponse = await _client.SendAsync(logoutAllRequest);
        Assert.Equal(HttpStatusCode.NoContent, logoutAllResponse.StatusCode);

        foreach (var token in new[] { account.Token, secondDeviceToken })
        {
            using var revokedRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
            revokedRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(revokedRequest)).StatusCode);
        }

        using var otherAccountRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        otherAccountRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", otherAccount.Token);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(otherAccountRequest)).StatusCode);

        var reauthenticatedToken = await LoginWithDeviceAsync(
            account.Email,
            "UserPassword-123!",
            $"recovery-device-{Guid.NewGuid():N}");
        using var reauthenticatedRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        reauthenticatedRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", reauthenticatedToken);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(reauthenticatedRequest)).StatusCode);
    }

    [Fact]
    [Trait("TestId", "AUTH-PROFILE-001")]
    public async Task ProfileUpdateDoesNotChangePassword()
    {
        var registered = await RegisterAsync("Profile User");

        using var updateRequest = AuthorizedRequest(
            HttpMethod.Put,
            "/api/auth/me",
            registered.Token,
            new
            {
                fullName = "Updated Profile User",
                currentPassword = "UserPassword-123!",
                newPassword = "ShouldBeIgnored-456!"
            });
        var updateResponse = await _client.SendAsync(updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updateBody = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated Profile User", updateBody.GetProperty("fullName").GetString());

        var oldPasswordLogin = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = registered.Email,
            password = "UserPassword-123!"
        });
        Assert.Equal(HttpStatusCode.OK, oldPasswordLogin.StatusCode);

        var ignoredPasswordLogin = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = registered.Email,
            password = "ShouldBeIgnored-456!"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, ignoredPasswordLogin.StatusCode);
    }

    [Fact]
    [Trait("TestId", "AUTH-AUTHZ-001")]
    public async Task ProtectedEndpointRequiresPermissionClaim()
    {
        var anonymousResponse = await _client.GetAsync("/api/auth/users");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        var registered = await RegisterAsync("Protected User");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/users");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", registered.Token);

        var forbiddenResponse = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
    }

    [Fact]
    [Trait("TestId", "AUTH-VALIDATION-001")]
    public async Task UnknownRoleAndPermissionNamesAreRejected()
    {
        var adminToken = await LoginAsync(AuthWebApplicationFactory.AdminEmail, AuthWebApplicationFactory.AdminPassword);
        var target = await RegisterAsync("Validation Target");

        using var unknownRoleRequest = AuthorizedRequest(
            HttpMethod.Post,
            $"/api/auth/users/{target.UserId}/roles",
            adminToken,
            new { roleNames = new[] { "role-that-does-not-exist" } });
        var unknownRoleResponse = await _client.SendAsync(unknownRoleRequest);
        Assert.Equal(HttpStatusCode.BadRequest, unknownRoleResponse.StatusCode);
        var unknownRoleBody = await unknownRoleResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("Unknown role names", unknownRoleBody.GetProperty("detail").GetString());

        using var createRoleRequest = AuthorizedRequest(
            HttpMethod.Post,
            "/api/auth/roles",
            adminToken,
            new { name = $"Validation-{Guid.NewGuid():N}", description = "Validation role" });
        var createRoleResponse = await _client.SendAsync(createRoleRequest);
        Assert.Equal(HttpStatusCode.Created, createRoleResponse.StatusCode);
        var roleBody = await createRoleResponse.Content.ReadFromJsonAsync<JsonElement>();
        var roleId = roleBody.GetProperty("id").GetGuid();

        using var unknownPermissionRequest = AuthorizedRequest(
            HttpMethod.Post,
            $"/api/auth/roles/{roleId}/permissions",
            adminToken,
            new { permissionCodes = new[] { "permission-that-does-not-exist" } });
        var unknownPermissionResponse = await _client.SendAsync(unknownPermissionRequest);
        Assert.Equal(HttpStatusCode.BadRequest, unknownPermissionResponse.StatusCode);
        var unknownPermissionBody = await unknownPermissionResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("Unknown permission codes", unknownPermissionBody.GetProperty("detail").GetString());
    }

    [Fact]
    [Trait("TestId", "AUTH-ESCALATION-001")]
    public async Task UserManagerCannotAssignSystemRole()
    {
        var adminToken = await LoginAsync(AuthWebApplicationFactory.AdminEmail, AuthWebApplicationFactory.AdminPassword);
        var roleName = $"Manager-{Guid.NewGuid():N}";

        using var createRoleRequest = AuthorizedRequest(
            HttpMethod.Post,
            "/api/auth/roles",
            adminToken,
            new { name = roleName, description = "Limited user manager" });
        var createRoleResponse = await _client.SendAsync(createRoleRequest);
        Assert.Equal(HttpStatusCode.Created, createRoleResponse.StatusCode);
        var roleBody = await createRoleResponse.Content.ReadFromJsonAsync<JsonElement>();
        var roleId = roleBody.GetProperty("id").GetGuid();

        using var permissionRequest = AuthorizedRequest(
            HttpMethod.Post,
            $"/api/auth/roles/{roleId}/permissions",
            adminToken,
            new { permissionCodes = new[] { PermissionCodes.UserManage } });
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(permissionRequest)).StatusCode);

        using var createManagerRequest = AuthorizedRequest(
            HttpMethod.Post,
            "/api/auth/users",
            adminToken,
            new
            {
                email = UniqueEmail(),
                password = "ManagerPassword-123!",
                fullName = "Limited Manager",
                roleNames = new[] { roleName }
            });
        var createManagerResponse = await _client.SendAsync(createManagerRequest);
        Assert.Equal(HttpStatusCode.Created, createManagerResponse.StatusCode);
        var managerBody = await createManagerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var managerEmail = managerBody.GetProperty("email").GetString()!;
        var managerToken = await LoginAsync(managerEmail, "ManagerPassword-123!");
        var target = await RegisterAsync("Escalation Target");

        using var escalationRequest = AuthorizedRequest(
            HttpMethod.Post,
            $"/api/auth/users/{target.UserId}/roles",
            managerToken,
            new { roleNames = new[] { "Admin" } });
        var escalationResponse = await _client.SendAsync(escalationRequest);
        Assert.Equal(HttpStatusCode.Forbidden, escalationResponse.StatusCode);
    }

    [Fact]
    [Trait("TestId", "AUTH-REVOCATION-001")]
    public async Task DeactivatedAccountTokenIsRejectedImmediately()
    {
        var user = await RegisterAsync("Revocation User");
        var adminToken = await LoginAsync(AuthWebApplicationFactory.AdminEmail, AuthWebApplicationFactory.AdminPassword);

        using var updateRequest = AuthorizedRequest(
            HttpMethod.Put,
            $"/api/auth/users/{user.UserId}",
            adminToken,
            new
            {
                email = user.Email,
                fullName = "Deactivated User",
                isActive = false
            });
        var updateResponse = await _client.SendAsync(updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        var meResponse = await _client.SendAsync(meRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);
    }

    private async Task<(string Token, Guid UserId, string Email)> RegisterAsync(string name)
    {
        var email = UniqueEmail();
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "UserPassword-123!",
            fullName = name
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("token").GetString()!, body.GetProperty("user").GetProperty("id").GetGuid(), email);
    }

    private async Task<string> LoginAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }

    private async Task<(string Token, Guid UserId, string Email, string DeviceId)> RegisterWithDeviceAsync(
        string name,
        string deviceId)
    {
        var email = UniqueEmail();
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "UserPassword-123!",
            fullName = name,
            deviceId
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(deviceId, body.GetProperty("deviceId").GetString());
        return (
            body.GetProperty("token").GetString()!,
            body.GetProperty("user").GetProperty("id").GetGuid(),
            email,
            body.GetProperty("deviceId").GetString()!);
    }

    private async Task<string> LoginWithDeviceAsync(string email, string password, string deviceId)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password,
            deviceId
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(deviceId, body.GetProperty("deviceId").GetString());
        return body.GetProperty("token").GetString()!;
    }

    private static HttpRequestMessage AuthorizedRequest(HttpMethod method, string uri, string token, object body)
    {
        var request = new HttpRequestMessage(method, uri)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@blueverse.local";
}
