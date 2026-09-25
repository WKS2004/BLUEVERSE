using System.Net;
using System.Text.Json;
using Blueverse.Auth.Tests.Fixtures;

namespace Blueverse.Auth.Tests.Integration;

public sealed class AuthAdministrationContractTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public AuthAdministrationContractTests(AuthWebApplicationFactory factory)
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
    [Trait("TestId", "AUTH-AUTHZ-002")]
    public async Task UserRoleAndPermissionCatalogsRequireTheirDeclaredPermissions()
    {
        var admin = await AuthTestSupport.LoginAsync(
            _client,
            AuthWebApplicationFactory.AdminEmail,
            AuthWebApplicationFactory.AdminPassword);
        var regular = await AuthTestSupport.RegisterAsync(_client, fullName: "Catalog Boundary User");

        var catalogRoutes = new[]
        {
            "/api/auth/users",
            "/api/auth/roles",
            "/api/auth/permissions"
        };

        foreach (var route in catalogRoutes)
        {
            using var anonymousResponse = await _client.GetAsync(route);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

            using var regularRequest = AuthTestSupport.AuthorizedRequest(HttpMethod.Get, route, regular.Token);
            using var regularResponse = await _client.SendAsync(regularRequest);
            Assert.Equal(HttpStatusCode.Forbidden, regularResponse.StatusCode);

            using var adminRequest = AuthTestSupport.AuthorizedRequest(HttpMethod.Get, route, admin.Token);
            using var adminResponse = await _client.SendAsync(adminRequest);
            var body = await AuthTestSupport.ReadJsonAsync(adminResponse);
            Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
            Assert.Equal(JsonValueKind.Array, body.ValueKind);
            Assert.True(body.GetArrayLength() > 0);
        }
    }

    [Fact]
    [Trait("TestId", "AUTH-AUTHZ-004")]
    public async Task RoleActionsRequireReadPlusTheSpecificMutationGrant()
    {
        var admin = await AuthTestSupport.LoginAsync(
            _client,
            AuthWebApplicationFactory.AdminEmail,
            AuthWebApplicationFactory.AdminPassword);
        var member = await AuthTestSupport.RegisterAsync(_client, fullName: "Granular Role Editor");
        var memberRoleName = $"Role-Reader-{Guid.NewGuid():N}";
        var targetRoleName = $"Target-{Guid.NewGuid():N}";

        using var createMemberRoleRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Post,
            "/api/auth/roles",
            admin.Token,
            new { name = memberRoleName, description = "Role with deliberately limited grants" });
        using var createMemberRoleResponse = await _client.SendAsync(createMemberRoleRequest);
        var memberRole = await AuthTestSupport.ReadJsonAsync(createMemberRoleResponse);
        Assert.Equal(HttpStatusCode.Created, createMemberRoleResponse.StatusCode);

        using var assignReaderRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Post,
            $"/api/auth/roles/{memberRole.GetProperty("id").GetGuid()}/permissions",
            admin.Token,
            new { permissionCodes = new[] { PermissionCodes.RoleRead } });
        using var assignReaderResponse = await _client.SendAsync(assignReaderRequest);
        Assert.Equal(HttpStatusCode.OK, assignReaderResponse.StatusCode);

        using var assignMemberRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Post,
            $"/api/auth/users/{member.UserId}/roles",
            admin.Token,
            new { roleNames = new[] { memberRoleName } });
        using var assignMemberResponse = await _client.SendAsync(assignMemberRequest);
        Assert.Equal(HttpStatusCode.OK, assignMemberResponse.StatusCode);
        var readOnly = await AuthTestSupport.LoginAsync(_client, member.Email, "UserPassword-123!");

        using var readRequest = AuthTestSupport.AuthorizedRequest(HttpMethod.Get, "/api/auth/roles", readOnly.Token);
        using var readResponse = await _client.SendAsync(readRequest);
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);

        using var createDeniedRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Post,
            "/api/auth/roles",
            readOnly.Token,
            new { name = $"Denied-{Guid.NewGuid():N}", description = "Read-only users cannot create roles" });
        using var createDeniedResponse = await _client.SendAsync(createDeniedRequest);
        Assert.Equal(HttpStatusCode.Forbidden, createDeniedResponse.StatusCode);

        using var createTargetRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Post,
            "/api/auth/roles",
            admin.Token,
            new { name = targetRoleName, description = "Target to update" });
        using var createTargetResponse = await _client.SendAsync(createTargetRequest);
        var targetRole = await AuthTestSupport.ReadJsonAsync(createTargetResponse);
        Assert.Equal(HttpStatusCode.Created, createTargetResponse.StatusCode);
        var targetRoleId = targetRole.GetProperty("id").GetGuid();

        using var promoteToEditorRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Post,
            $"/api/auth/roles/{memberRole.GetProperty("id").GetGuid()}/permissions",
            admin.Token,
            new { permissionCodes = new[] { PermissionCodes.RoleRead, PermissionCodes.RoleUpdate } });
        using var promoteToEditorResponse = await _client.SendAsync(promoteToEditorRequest);
        Assert.Equal(HttpStatusCode.OK, promoteToEditorResponse.StatusCode);
        var editor = await AuthTestSupport.LoginAsync(_client, member.Email, "UserPassword-123!");

        using var updateAllowedRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Put,
            $"/api/auth/roles/{targetRoleId}",
            editor.Token,
            new { name = targetRoleName, description = "Updated with read and update grants" });
        using var updateAllowedResponse = await _client.SendAsync(updateAllowedRequest);
        var updatedRole = await AuthTestSupport.ReadJsonAsync(updateAllowedResponse);
        Assert.Equal(HttpStatusCode.OK, updateAllowedResponse.StatusCode);
        Assert.Equal("Updated with read and update grants", updatedRole.GetProperty("description").GetString());

        using var deleteDeniedRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Delete,
            $"/api/auth/roles/{targetRoleId}",
            editor.Token);
        using var deleteDeniedResponse = await _client.SendAsync(deleteDeniedRequest);
        Assert.Equal(HttpStatusCode.Forbidden, deleteDeniedResponse.StatusCode);

        using var permissionWriteDeniedRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Post,
            $"/api/auth/roles/{targetRoleId}/permissions",
            editor.Token,
            new { permissionCodes = Array.Empty<string>() });
        using var permissionWriteDeniedResponse = await _client.SendAsync(permissionWriteDeniedRequest);
        Assert.Equal(HttpStatusCode.Forbidden, permissionWriteDeniedResponse.StatusCode);
    }

    [Fact]
    [Trait("TestId", "AUTH-AUTHZ-005")]
    public async Task UserActionsRequireReadPlusTheSpecificMutationGrant()
    {
        var admin = await AuthTestSupport.LoginAsync(
            _client,
            AuthWebApplicationFactory.AdminEmail,
            AuthWebApplicationFactory.AdminPassword);
        var member = await AuthTestSupport.RegisterAsync(_client, fullName: "Granular User Editor");
        var target = await AuthTestSupport.RegisterAsync(_client, fullName: "Managed User Target");
        var roleName = $"User-Editor-{Guid.NewGuid():N}";

        using var createRoleRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Post,
            "/api/auth/roles",
            admin.Token,
            new { name = roleName, description = "User actions with explicit grant combinations" });
        using var createRoleResponse = await _client.SendAsync(createRoleRequest);
        var role = await AuthTestSupport.ReadJsonAsync(createRoleResponse);
        Assert.Equal(HttpStatusCode.Created, createRoleResponse.StatusCode);
        var roleId = role.GetProperty("id").GetGuid();

        using var setCreateOnlyRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Post,
            $"/api/auth/roles/{roleId}/permissions",
            admin.Token,
            new { permissionCodes = new[] { PermissionCodes.UserCreate } });
        using var setCreateOnlyResponse = await _client.SendAsync(setCreateOnlyRequest);
        Assert.Equal(HttpStatusCode.OK, setCreateOnlyResponse.StatusCode);

        using var assignMemberRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Post,
            $"/api/auth/users/{member.UserId}/roles",
            admin.Token,
            new { roleNames = new[] { roleName } });
        using var assignMemberResponse = await _client.SendAsync(assignMemberRequest);
        Assert.Equal(HttpStatusCode.OK, assignMemberResponse.StatusCode);
        var editor = await AuthTestSupport.LoginAsync(_client, member.Email, "UserPassword-123!");

        using var createWithoutReadRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Post,
            "/api/auth/users",
            editor.Token,
            new
            {
                email = AuthTestSupport.UniqueEmail("no-read"),
                password = "ManagedPassword-123!",
                fullName = "Must Not Be Created",
                roleNames = Array.Empty<string>()
            });
        using var createWithoutReadResponse = await _client.SendAsync(createWithoutReadRequest);
        Assert.Equal(HttpStatusCode.Forbidden, createWithoutReadResponse.StatusCode);

        using var grantReadAndCreateRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Post,
            $"/api/auth/roles/{roleId}/permissions",
            admin.Token,
            new { permissionCodes = new[] { PermissionCodes.UserRead, PermissionCodes.UserCreate } });
        using var grantReadAndCreateResponse = await _client.SendAsync(grantReadAndCreateRequest);
        Assert.Equal(HttpStatusCode.OK, grantReadAndCreateResponse.StatusCode);
        editor = await AuthTestSupport.LoginAsync(_client, member.Email, "UserPassword-123!");

        using var readAllowedRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Get,
            "/api/auth/users",
            editor.Token);
        using var readAllowedResponse = await _client.SendAsync(readAllowedRequest);
        Assert.Equal(HttpStatusCode.OK, readAllowedResponse.StatusCode);

        using var createAllowedRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Post,
            "/api/auth/users",
            editor.Token,
            new
            {
                email = AuthTestSupport.UniqueEmail("read-create"),
                password = "ManagedPassword-123!",
                fullName = "Created With Both Grants",
                roleNames = Array.Empty<string>()
            });
        using var createAllowedResponse = await _client.SendAsync(createAllowedRequest);
        Assert.Equal(HttpStatusCode.Created, createAllowedResponse.StatusCode);

        using var updateWithoutGrantRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Put,
            $"/api/auth/users/{target.UserId}",
            editor.Token,
            new { email = target.Email, fullName = "Must Not Update", isActive = true });
        using var updateWithoutGrantResponse = await _client.SendAsync(updateWithoutGrantRequest);
        Assert.Equal(HttpStatusCode.Forbidden, updateWithoutGrantResponse.StatusCode);

        using var deleteWithoutGrantRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Delete,
            $"/api/auth/users/{target.UserId}",
            editor.Token);
        using var deleteWithoutGrantResponse = await _client.SendAsync(deleteWithoutGrantRequest);
        Assert.Equal(HttpStatusCode.Forbidden, deleteWithoutGrantResponse.StatusCode);

        using var assignWithoutGrantsRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Post,
            $"/api/auth/users/{target.UserId}/roles",
            editor.Token,
            new { roleNames = Array.Empty<string>() });
        using var assignWithoutGrantsResponse = await _client.SendAsync(assignWithoutGrantsRequest);
        Assert.Equal(HttpStatusCode.Forbidden, assignWithoutGrantsResponse.StatusCode);

        using var grantUpdateRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Post,
            $"/api/auth/roles/{roleId}/permissions",
            admin.Token,
            new
            {
                permissionCodes = new[]
                {
                    PermissionCodes.UserRead,
                    PermissionCodes.UserCreate,
                    PermissionCodes.UserUpdate,
                    PermissionCodes.RoleRead
                }
            });
        using var grantUpdateResponse = await _client.SendAsync(grantUpdateRequest);
        Assert.Equal(HttpStatusCode.OK, grantUpdateResponse.StatusCode);
        editor = await AuthTestSupport.LoginAsync(_client, member.Email, "UserPassword-123!");

        using var updateAllowedRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Put,
            $"/api/auth/users/{target.UserId}",
            editor.Token,
            new { email = target.Email, fullName = "Updated With All Required Grants", isActive = true });
        using var updateAllowedResponse = await _client.SendAsync(updateAllowedRequest);
        var updatedUser = await AuthTestSupport.ReadJsonAsync(updateAllowedResponse);
        Assert.Equal(HttpStatusCode.OK, updateAllowedResponse.StatusCode);
        Assert.Equal("Updated With All Required Grants", updatedUser.GetProperty("fullName").GetString());

        using var assignAllowedRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Post,
            $"/api/auth/users/{target.UserId}/roles",
            editor.Token,
            new { roleNames = Array.Empty<string>() });
        using var assignAllowedResponse = await _client.SendAsync(assignAllowedRequest);
        Assert.Equal(HttpStatusCode.OK, assignAllowedResponse.StatusCode);

        using var grantDeleteRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Post,
            $"/api/auth/roles/{roleId}/permissions",
            admin.Token,
            new
            {
                permissionCodes = new[]
                {
                    PermissionCodes.UserRead,
                    PermissionCodes.UserCreate,
                    PermissionCodes.UserUpdate,
                    PermissionCodes.UserDelete,
                    PermissionCodes.RoleRead
                }
            });
        using var grantDeleteResponse = await _client.SendAsync(grantDeleteRequest);
        Assert.Equal(HttpStatusCode.OK, grantDeleteResponse.StatusCode);
        editor = await AuthTestSupport.LoginAsync(_client, member.Email, "UserPassword-123!");

        using var deleteAllowedRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Delete,
            $"/api/auth/users/{target.UserId}",
            editor.Token);
        using var deleteAllowedResponse = await _client.SendAsync(deleteAllowedRequest);
        Assert.Equal(HttpStatusCode.NoContent, deleteAllowedResponse.StatusCode);
    }

    [Fact]
    [Trait("TestId", "AUTH-AUTHZ-003")]
    public async Task RegularUsersCannotInvokeAdministrativeMutationRoutes()
    {
        var regular = await AuthTestSupport.RegisterAsync(_client, fullName: "Mutation Boundary User");
        var unknownId = Guid.NewGuid();
        var mutationRoutes = new (HttpMethod Method, string Path, object? Body)[]
        {
            (HttpMethod.Post, "/api/auth/users", new
            {
                email = AuthTestSupport.UniqueEmail("blocked-user"),
                password = "BlockedPassword-123!",
                fullName = "Blocked User",
                roleNames = Array.Empty<string>()
            }),
            (HttpMethod.Put, $"/api/auth/users/{regular.UserId}", new
            {
                email = regular.Email,
                fullName = "Blocked Update",
                isActive = true
            }),
            (HttpMethod.Delete, $"/api/auth/users/{regular.UserId}", null),
            (HttpMethod.Post, $"/api/auth/users/{regular.UserId}/roles", new
            {
                roleNames = Array.Empty<string>()
            }),
            (HttpMethod.Post, "/api/auth/roles", new
            {
                name = $"Blocked-Role-{Guid.NewGuid():N}",
                description = "Should not be created"
            }),
            (HttpMethod.Put, $"/api/auth/roles/{unknownId}", new
            {
                name = "Blocked Role",
                description = "Should not be changed"
            }),
            (HttpMethod.Delete, $"/api/auth/roles/{unknownId}", null),
            (HttpMethod.Post, $"/api/auth/roles/{unknownId}/permissions", new
            {
                permissionCodes = Array.Empty<string>()
            })
        };

        foreach (var (method, path, body) in mutationRoutes)
        {
            using var request = AuthTestSupport.AuthorizedRequest(method, path, regular.Token, body);
            using var response = await _client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    [Trait("TestId", "AUTH-ADMIN-VALIDATION-001")]
    public async Task AdministrativeCreationEndpointsReturnFieldValidationDetails()
    {
        var admin = await AuthTestSupport.LoginAsync(
            _client,
            AuthWebApplicationFactory.AdminEmail,
            AuthWebApplicationFactory.AdminPassword);

        using var userRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Post,
            "/api/auth/users",
            admin.Token,
            new { });
        using var userResponse = await _client.SendAsync(userRequest);
        var userBody = await AuthTestSupport.ReadJsonAsync(userResponse);

        Assert.Equal(HttpStatusCode.BadRequest, userResponse.StatusCode);
        Assert.Equal("application/problem+json", userResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(400, userBody.GetProperty("status").GetInt32());
        var userErrors = userBody.GetProperty("errors");
        foreach (var field in new[] { "email", "password", "fullName" })
        {
            Assert.Contains(
                userErrors.EnumerateObject(),
                property => string.Equals(property.Name, field, StringComparison.OrdinalIgnoreCase));
        }

        using var roleRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Post,
            "/api/auth/roles",
            admin.Token,
            new { });
        using var roleResponse = await _client.SendAsync(roleRequest);
        var roleBody = await AuthTestSupport.ReadJsonAsync(roleResponse);

        Assert.Equal(HttpStatusCode.BadRequest, roleResponse.StatusCode);
        Assert.Equal("application/problem+json", roleResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(400, roleBody.GetProperty("status").GetInt32());
        Assert.Contains(
            roleBody.GetProperty("errors").EnumerateObject(),
            property => string.Equals(property.Name, "name", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("TestId", "AUTH-USER-CRUD-001")]
    public async Task AdminCanCreateUpdateAndDeleteAUserWithoutExposingPasswordData()
    {
        var admin = await AuthTestSupport.LoginAsync(
            _client,
            AuthWebApplicationFactory.AdminEmail,
            AuthWebApplicationFactory.AdminPassword);
        var requestedEmail = $"  managed-{Guid.NewGuid():N}@BlueVerse.Local  ";
        using var createRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Post,
            "/api/auth/users",
            admin.Token,
            new
            {
                email = requestedEmail,
                password = "ManagedPassword-123!",
                fullName = "  Managed User  ",
                roleNames = Array.Empty<string>()
            });
        using var createResponse = await _client.SendAsync(createRequest);
        var createdBody = await AuthTestSupport.ReadJsonAsync(createResponse);
        var managedUserId = createdBody.GetProperty("id").GetGuid();
        var normalizedEmail = requestedEmail.Trim().ToLowerInvariant();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal(normalizedEmail, createdBody.GetProperty("email").GetString());
        Assert.Equal("Managed User", createdBody.GetProperty("fullName").GetString());
        Assert.Empty(createdBody.GetProperty("roles").EnumerateArray());
        Assert.DoesNotContain("password", createdBody.ToString(), StringComparison.OrdinalIgnoreCase);

        using var listRequest = AuthTestSupport.AuthorizedRequest(HttpMethod.Get, "/api/auth/users", admin.Token);
        using var listResponse = await _client.SendAsync(listRequest);
        var listBody = await AuthTestSupport.ReadJsonAsync(listResponse);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Contains(listBody.EnumerateArray(), item => item.GetProperty("id").GetGuid() == managedUserId);

        using var getRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Get,
            $"/api/auth/users/{managedUserId}",
            admin.Token);
        using var getResponse = await _client.SendAsync(getRequest);
        var getBody = await AuthTestSupport.ReadJsonAsync(getResponse);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(managedUserId, getBody.GetProperty("id").GetGuid());

        var managedSession = await AuthTestSupport.LoginAsync(
            _client,
            normalizedEmail,
            "ManagedPassword-123!");

        using var updateRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Put,
            $"/api/auth/users/{managedUserId}",
            admin.Token,
            new
            {
                email = $"managed-updated-{Guid.NewGuid():N}@blueverse.local",
                fullName = "Updated Managed User",
                isActive = true,
                newPassword = "UpdatedManagedPassword-456!"
            });
        using var updateResponse = await _client.SendAsync(updateRequest);
        var updateBody = await AuthTestSupport.ReadJsonAsync(updateResponse);
        var updatedEmail = updateBody.GetProperty("email").GetString()!;

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal("Updated Managed User", updateBody.GetProperty("fullName").GetString());
        Assert.DoesNotContain("password", updateBody.ToString(), StringComparison.OrdinalIgnoreCase);

        using var revokedManagedRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Get,
            "/api/auth/me",
            managedSession.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(revokedManagedRequest)).StatusCode);

        using var oldPasswordResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = updatedEmail,
            password = "ManagedPassword-123!"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordResponse.StatusCode);

        var updatedSession = await AuthTestSupport.LoginAsync(
            _client,
            updatedEmail,
            "UpdatedManagedPassword-456!");
        Assert.Equal(managedUserId, updatedSession.UserId);

        using var deleteRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Delete,
            $"/api/auth/users/{managedUserId}",
            admin.Token);
        using var deleteResponse = await _client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Empty(await deleteResponse.Content.ReadAsStringAsync());

        using var missingRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Get,
            $"/api/auth/users/{managedUserId}",
            admin.Token);
        using var missingResponse = await _client.SendAsync(missingRequest);
        var missingBody = await AuthTestSupport.ReadJsonAsync(missingResponse);
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        Assert.Equal("User Not Found", missingBody.GetProperty("title").GetString());
    }

    [Fact]
    [Trait("TestId", "AUTH-USER-RULES-001")]
    public async Task SystemRoleAccountsCannotBeModifiedToLoseTheRecoveryPath()
    {
        var admin = await AuthTestSupport.LoginAsync(
            _client,
            AuthWebApplicationFactory.AdminEmail,
            AuthWebApplicationFactory.AdminPassword);
        var adminUserId = admin.UserId;

        using var emailUpdateRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Put,
            $"/api/auth/users/{adminUserId}",
            admin.Token,
            new
            {
                email = AuthTestSupport.UniqueEmail("replacement-admin"),
                fullName = "Test Administrator",
                isActive = true
            });
        using var emailUpdateResponse = await _client.SendAsync(emailUpdateRequest);
        var emailUpdateBody = await AuthTestSupport.ReadJsonAsync(emailUpdateResponse);
        Assert.Equal(HttpStatusCode.BadRequest, emailUpdateResponse.StatusCode);
        Assert.Equal("User Update Failed", emailUpdateBody.GetProperty("title").GetString());

        using var deleteRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Delete,
            $"/api/auth/users/{adminUserId}",
            admin.Token);
        using var deleteResponse = await _client.SendAsync(deleteRequest);
        var deleteBody = await AuthTestSupport.ReadJsonAsync(deleteResponse);
        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);
        Assert.Equal("User Deletion Prohibited", deleteBody.GetProperty("title").GetString());

        using var selfDeleteRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Delete,
            "/api/auth/me",
            admin.Token);
        using var selfDeleteResponse = await _client.SendAsync(selfDeleteRequest);
        var selfDeleteBody = await AuthTestSupport.ReadJsonAsync(selfDeleteResponse);
        Assert.Equal(HttpStatusCode.BadRequest, selfDeleteResponse.StatusCode);
        Assert.Equal("Account Deletion Prohibited", selfDeleteBody.GetProperty("title").GetString());
    }

    [Fact]
    [Trait("TestId", "AUTH-ROLE-CRUD-001")]
    public async Task RolesSupportCreateUpdatePermissionAssignmentAndDeletionBoundaries()
    {
        var admin = await AuthTestSupport.LoginAsync(
            _client,
            AuthWebApplicationFactory.AdminEmail,
            AuthWebApplicationFactory.AdminPassword);
        var roleName = $"Managed-Role-{Guid.NewGuid():N}";

        using var createRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Post,
            "/api/auth/roles",
            admin.Token,
            new { name = $"  {roleName}  ", description = "Managed role" });
        using var createResponse = await _client.SendAsync(createRequest);
        var createdBody = await AuthTestSupport.ReadJsonAsync(createResponse);
        var roleId = createdBody.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal(roleName, createdBody.GetProperty("name").GetString());
        Assert.Empty(createdBody.GetProperty("permissions").EnumerateArray());

        using var duplicateRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Post,
            "/api/auth/roles",
            admin.Token,
            new { name = roleName.ToUpperInvariant(), description = "Duplicate" });
        using var duplicateResponse = await _client.SendAsync(duplicateRequest);
        Assert.Equal(HttpStatusCode.BadRequest, duplicateResponse.StatusCode);

        using var updateRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Put,
            $"/api/auth/roles/{roleId}",
            admin.Token,
            new { name = roleName, description = "Updated managed role" });
        using var updateResponse = await _client.SendAsync(updateRequest);
        var updateBody = await AuthTestSupport.ReadJsonAsync(updateResponse);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal("Updated managed role", updateBody.GetProperty("description").GetString());

        using var permissionRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Post,
            $"/api/auth/roles/{roleId}/permissions",
            admin.Token,
            new { permissionCodes = new[] { " AUTH.USER.READ ", "auth.user.read" } });
        using var permissionResponse = await _client.SendAsync(permissionRequest);
        var permissionBody = await AuthTestSupport.ReadJsonAsync(permissionResponse);
        Assert.Equal(HttpStatusCode.OK, permissionResponse.StatusCode);
        Assert.Equal(new[] { "auth.user.read" }, permissionBody.GetProperty("permissions").EnumerateArray().Select(item => item.GetString()).ToArray());

        using var unknownPermissionRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Post,
            $"/api/auth/roles/{roleId}/permissions",
            admin.Token,
            new { permissionCodes = new[] { "does.not.exist" } });
        using var unknownPermissionResponse = await _client.SendAsync(unknownPermissionRequest);
        Assert.Equal(HttpStatusCode.BadRequest, unknownPermissionResponse.StatusCode);

        using var deleteRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Delete,
            $"/api/auth/roles/{roleId}",
            admin.Token);
        using var deleteResponse = await _client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var missingRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Get,
            $"/api/auth/roles/{roleId}",
            admin.Token);
        using var missingResponse = await _client.SendAsync(missingRequest);
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
    }

    [Fact]
    [Trait("TestId", "AUTH-ROLE-RULES-001")]
    public async Task SystemRolesCannotBeRenamedDeletedOrReconfigured()
    {
        var admin = await AuthTestSupport.LoginAsync(
            _client,
            AuthWebApplicationFactory.AdminEmail,
            AuthWebApplicationFactory.AdminPassword);

        using var rolesRequest = AuthTestSupport.AuthorizedRequest(HttpMethod.Get, "/api/auth/roles", admin.Token);
        using var rolesResponse = await _client.SendAsync(rolesRequest);
        var roles = await AuthTestSupport.ReadJsonAsync(rolesResponse);
        var systemRole = roles.EnumerateArray().Single(item => item.GetProperty("name").GetString() == "Admin");
        var systemRoleId = systemRole.GetProperty("id").GetGuid();

        using var renameRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Put,
            $"/api/auth/roles/{systemRoleId}",
            admin.Token,
            new { name = "Renamed Admin", description = "Should fail" });
        using var renameResponse = await _client.SendAsync(renameRequest);
        Assert.Equal(HttpStatusCode.BadRequest, renameResponse.StatusCode);

        using var permissionsRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Post,
            $"/api/auth/roles/{systemRoleId}/permissions",
            admin.Token,
            new { permissionCodes = Array.Empty<string>() });
        using var permissionsResponse = await _client.SendAsync(permissionsRequest);
        Assert.Equal(HttpStatusCode.BadRequest, permissionsResponse.StatusCode);

        using var deleteRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Delete,
            $"/api/auth/roles/{systemRoleId}",
            admin.Token);
        using var deleteResponse = await _client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);
    }

    [Fact]
    [Trait("TestId", "AUTH-ROLE-INVALIDATION-001")]
    public async Task DeletingAnAssignedRoleArchivesTheAffectedUserSession()
    {
        var admin = await AuthTestSupport.LoginAsync(
            _client,
            AuthWebApplicationFactory.AdminEmail,
            AuthWebApplicationFactory.AdminPassword);
        var roleName = $"Revoked-Role-{Guid.NewGuid():N}";

        using var createRoleRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Post,
            "/api/auth/roles",
            admin.Token,
            new { name = roleName, description = "Role invalidation test" });
        using var createRoleResponse = await _client.SendAsync(createRoleRequest);
        Assert.Equal(HttpStatusCode.Created, createRoleResponse.StatusCode);

        using var createUserRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Post,
            "/api/auth/users",
            admin.Token,
            new
            {
                email = AuthTestSupport.UniqueEmail("role-user"),
                password = "RoleUserPassword-123!",
                fullName = "Role Invalidation User",
                roleNames = new[] { roleName }
            });
        using var createUserResponse = await _client.SendAsync(createUserRequest);
        var userBody = await AuthTestSupport.ReadJsonAsync(createUserResponse);
        var userId = userBody.GetProperty("id").GetGuid();
        var email = userBody.GetProperty("email").GetString()!;
        var user = await AuthTestSupport.LoginAsync(_client, email, "RoleUserPassword-123!");

        using var rolesRequest = AuthTestSupport.AuthorizedRequest(HttpMethod.Get, "/api/auth/roles", admin.Token);
        using var rolesResponse = await _client.SendAsync(rolesRequest);
        var roles = await AuthTestSupport.ReadJsonAsync(rolesResponse);
        var roleId = roles.EnumerateArray().Single(item => item.GetProperty("name").GetString() == roleName).GetProperty("id").GetGuid();

        using var deleteRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Delete,
            $"/api/auth/roles/{roleId}",
            admin.Token);
        using var deleteResponse = await _client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var revokedRequest = AuthTestSupport.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", user.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(revokedRequest)).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        Assert.Empty(await db.ActiveSessions.Where(session => session.UserId == userId).ToListAsync());
        Assert.Contains(
            await db.UserSessionLogs.Where(log => log.UserId == userId).ToListAsync(),
            log => log.EndReason == "account-security-change");
    }

    [Fact]
    [Trait("TestId", "AUTH-USER-DELETE-001")]
    public async Task ARegularUserCanDeleteTheirOwnAccountAndTheTokenStopsWorking()
    {
        var user = await AuthTestSupport.RegisterAsync(_client, fullName: "Self Delete User");

        using var deleteRequest = AuthTestSupport.AuthorizedRequest(HttpMethod.Delete, "/api/auth/me", user.Token);
        using var deleteResponse = await _client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Empty(await deleteResponse.Content.ReadAsStringAsync());

        using var meRequest = AuthTestSupport.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", user.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(meRequest)).StatusCode);

        using var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = user.Email,
            password = "UserPassword-123!"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        Assert.False(await db.Users.AnyAsync(item => item.Id == user.UserId));
    }

    [Fact]
    [Trait("TestId", "AUTH-USER-DELETE-002")]
    public async Task SystemRoleAccountCannotDeleteItself()
    {
        var admin = await AuthTestSupport.LoginAsync(
            _client,
            AuthWebApplicationFactory.AdminEmail,
            AuthWebApplicationFactory.AdminPassword);

        using var deleteRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Delete,
            "/api/auth/me",
            admin.Token);
        using var deleteResponse = await _client.SendAsync(deleteRequest);

        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);
        var problem = await deleteResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Account Deletion Prohibited", problem.GetProperty("title").GetString());

        using var meRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Get,
            "/api/auth/me",
            admin.Token);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(meRequest)).StatusCode);
    }
}
