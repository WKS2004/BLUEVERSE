using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Blueverse.Auth.Authorization;
using Blueverse.Auth.Data;
using Blueverse.Auth.Dtos;
using Blueverse.Auth.Models;

namespace Blueverse.Auth.Services;

public class AuthService : IAuthService
{
    private const int MaxAccountsPerDevice = 5;
    private static readonly SemaphoreSlim SessionMutationLock = new(1, 1);

    private readonly AuthDbContext _dbContext;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly AuthSessionOptions _sessionOptions;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AuthDbContext dbContext,
        IPasswordHasherService passwordHasher,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService,
        IOptions<AuthSessionOptions> sessionOptions,
        ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        _sessionOptions = sessionOptions.Value;
        _logger = logger;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto dto)
    {
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        var existingUser = await _dbContext.Users
            .AnyAsync(u => u.Email.ToLower() == normalizedEmail);

        if (existingUser)
        {
            throw new InvalidOperationException($"User with email '{dto.Email}' already exists.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            FullName = dto.FullName.Trim(),
            PasswordHash = _passwordHasher.HashPassword(dto.Password),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        var session = await CreateOrReactivateSessionAsync(
            user.Id,
            dto.DeviceId,
            dto.DeviceKey,
            dto.RememberMe);

        _logger.LogInformation("Registered new user {UserId} with email {Email}", user.Id, user.Email);

        return await CreateAuthResponseAsync(user.Id, session);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto dto)
    {
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

        if (user == null || !_passwordHasher.VerifyPassword(dto.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("This account is inactive.");
        }

        _logger.LogInformation("User {UserId} logged in successfully", user.Id);

        var session = await CreateOrReactivateSessionAsync(
            user.Id,
            dto.DeviceId,
            dto.DeviceKey,
            dto.RememberMe);
        return await CreateAuthResponseAsync(user.Id, session);
    }

    public async Task<int> ArchiveExpiredSessionsAsync(CancellationToken cancellationToken = default)
    {
        await SessionMutationLock.WaitAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            var sessions = await _dbContext.ActiveSessions
                .Where(session => session.ExpiresAt <= now)
                .ToListAsync(cancellationToken);
            if (sessions.Count == 0)
            {
                return 0;
            }

            await ArchiveSessionsAsync(sessions, now, "session-expired");
            await _dbContext.SaveChangesAsync(cancellationToken);
            return sessions.Count;
        }
        finally
        {
            SessionMutationLock.Release();
        }
    }

    public async Task<AuthResponseDto> RefreshAsync(RefreshTokenRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.RefreshToken))
        {
            throw new UnauthorizedAccessException("A refresh token is required.");
        }

        var tokenHash = _refreshTokenService.Hash(dto.RefreshToken);

        await SessionMutationLock.WaitAsync();
        try
        {
            var executionStrategy = _dbContext.Database.CreateExecutionStrategy();
            return await executionStrategy.ExecuteAsync(async () =>
            {
                IDbContextTransaction? transaction = null;
                var committed = false;

                try
                {
                    if (_dbContext.Database.IsRelational())
                    {
                        transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                    }

                    await AcquireRefreshLockAsync(tokenHash);

                    var storedToken = await _dbContext.RefreshTokens
                        .Include(token => token.UserSession)
                        .FirstOrDefaultAsync(token => token.TokenHash == tokenHash);

                    if (storedToken is null || storedToken.UserSession is null)
                    {
                        throw new UnauthorizedAccessException("The refresh token is invalid.");
                    }

                    var now = DateTime.UtcNow;
                    var session = storedToken.UserSession;

                    if (storedToken.ConsumedAt != null)
                    {
                        // A consumed token being presented again is a replay. Revoke
                        // the complete session family so a stolen token cannot race
                        // the legitimate client indefinitely.
                        await ArchiveSessionsAsync([session], now, "refresh-token-reuse");
                        await _dbContext.SaveChangesAsync();

                        if (transaction != null)
                        {
                            await transaction.CommitAsync();
                        }

                        committed = true;
                        throw new UnauthorizedAccessException("The refresh token has already been used.");
                    }

                    if (storedToken.RevokedAt != null ||
                        storedToken.ExpiresAt <= now ||
                        session.ExpiresAt <= now)
                    {
                        throw new UnauthorizedAccessException("The refresh token or session has expired.");
                    }

                    if (!string.IsNullOrWhiteSpace(dto.DeviceId) &&
                        !string.Equals(
                            NormalizeRequestedDeviceId(dto.DeviceId),
                            session.DeviceId,
                            StringComparison.Ordinal))
                    {
                        throw new UnauthorizedAccessException("The refresh token does not belong to this device.");
                    }

                    if (!await HasValidDeviceProofAsync(session.DeviceId, dto.DeviceKey))
                    {
                        throw new UnauthorizedAccessException("The device proof is invalid.");
                    }

                    storedToken.ConsumedAt = now;
                    var replacementToken = _refreshTokenService.CreateOpaqueToken();
                    var replacement = new RefreshToken
                    {
                        UserSessionId = session.Id,
                        FamilyId = storedToken.FamilyId,
                        TokenHash = _refreshTokenService.Hash(replacementToken),
                        IssuedAt = now,
                        ExpiresAt = session.ExpiresAt
                    };
                    storedToken.ReplacedByTokenId = replacement.Id;
                    session.LastSeenAt = now;
                    _dbContext.RefreshTokens.Add(replacement);

                    await _dbContext.SaveChangesAsync();
                    if (transaction != null)
                    {
                        await transaction.CommitAsync();
                    }

                    committed = true;
                    return await CreateAuthResponseAsync(
                        session.UserId,
                        new SessionIssueResult(session, null, replacementToken));
                }
                catch
                {
                    if (transaction != null && !committed)
                    {
                        await transaction.RollbackAsync();
                    }

                    throw;
                }
                finally
                {
                    if (transaction != null)
                    {
                        await transaction.DisposeAsync();
                    }
                }
            });
        }
        finally
        {
            SessionMutationLock.Release();
        }
    }

    public async Task<List<SessionDto>> GetSessionsAsync(Guid userId, Guid? currentSessionId)
    {
        var now = DateTime.UtcNow;
        var sessions = await _dbContext.ActiveSessions
            .AsNoTracking()
            .Where(session =>
                session.UserId == userId &&
                session.ExpiresAt > now)
            .OrderByDescending(session => session.LastSeenAt)
            .ToListAsync();

        return sessions.Select(session => new SessionDto
        {
            Id = session.Id,
            DeviceId = session.DeviceId,
            CreatedAt = session.CreatedAt,
            LastSeenAt = session.LastSeenAt,
            ExpiresAt = session.ExpiresAt,
            RememberMe = session.RememberMe,
            IsCurrent = currentSessionId.HasValue && session.Id == currentSessionId.Value
        }).ToList();
    }

    public async Task<bool> RevokeSessionAsync(Guid userId, Guid sessionId)
    {
        var session = await _dbContext.ActiveSessions
            .FirstOrDefaultAsync(item => item.Id == sessionId && item.UserId == userId);
        if (session == null)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        await ArchiveSessionsAsync([session], now, "manual-revocation");
        await _dbContext.SaveChangesAsync();

        return true;
    }

    public async Task<bool> LogoutCurrentDeviceAsync(Guid userId, Guid sessionId)
    {
        var currentSession = await _dbContext.ActiveSessions
            .FirstOrDefaultAsync(session =>
                session.Id == sessionId &&
                session.UserId == userId);
        if (currentSession == null)
        {
            return false;
        }

        var sessions = await _dbContext.ActiveSessions
            .Where(session => session.DeviceId == currentSession.DeviceId)
            .ToListAsync();
        var revokedAt = DateTime.UtcNow;
        await ArchiveSessionsAsync(sessions, revokedAt, "device-logout");

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Logged out all accounts on device {DeviceId}; revoked {SessionCount} sessions",
            currentSession.DeviceId,
            sessions.Count);
        return true;
    }

    public async Task<bool> LogoutAccountOnCurrentDeviceAsync(Guid actorUserId, Guid sessionId, Guid targetUserId)
    {
        var actorSession = await _dbContext.ActiveSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(session =>
                session.Id == sessionId &&
                session.UserId == actorUserId);
        if (actorSession == null)
        {
            return false;
        }

        var targetSession = await _dbContext.ActiveSessions
            .FirstOrDefaultAsync(session =>
                session.UserId == targetUserId &&
                session.DeviceId == actorSession.DeviceId);
        if (targetSession != null)
        {
            await ArchiveSessionsAsync([targetSession], DateTime.UtcNow, "account-device-logout");
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation(
                "Logged out account {UserId} from device {DeviceId}",
                targetUserId,
                actorSession.DeviceId);
        }

        // Account-specific logout is intentionally idempotent for accounts that
        // are not currently signed in on this device.
        return true;
    }

    public async Task<bool> LogoutAllDevicesAsync(Guid userId)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(currentUser => currentUser.Id == userId);
        if (user == null)
        {
            return false;
        }

        var sessions = await _dbContext.ActiveSessions
            .Where(session => session.UserId == userId)
            .ToListAsync();
        var revokedAt = DateTime.UtcNow;
        await ArchiveSessionsAsync(sessions, revokedAt, "account-logout-all-devices");

        // Keep token-version revocation as the account-wide backstop for tokens
        // issued before session persistence was introduced and for other Auth
        // protected endpoints that do not rely on a device session.
        user.TokenVersion++;
        user.UpdatedAt = revokedAt;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Logged out user {UserId} from every device; revoked {SessionCount} sessions",
            userId,
            sessions.Count);
        return true;
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordDto dto)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return false;
        }

        if (!_passwordHasher.VerifyPassword(dto.CurrentPassword, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Current password is required and must be correct to change password.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(dto.NewPassword);
        user.TokenVersion++;
        user.UpdatedAt = DateTime.UtcNow;
        await RevokeSessionsForUsersAsync([user]);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("User {UserId} changed their password; all issued tokens were revoked", user.Id);
        return true;
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid userId)
    {
        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Id == userId);

        return user == null ? null : MapToUserDto(user);
    }

    public async Task<List<UserDto>> GetAllUsersAsync()
    {
        var users = await _dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .OrderBy(u => u.CreatedAt)
            .ToListAsync();

        return users.Select(MapToUserDto).ToList();
    }

    public async Task<List<RoleDto>> GetAllRolesAsync()
    {
        var roles = await _dbContext.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .OrderBy(r => r.Name)
            .ToListAsync();

        return roles.Select(MapToRoleDto).ToList();
    }

    public async Task<RoleDto?> GetRoleByIdAsync(Guid roleId)
    {
        var role = await _dbContext.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        return role == null ? null : MapToRoleDto(role);
    }

    public async Task<RoleDto> CreateRoleAsync(CreateRoleDto dto)
    {
        var trimmedName = dto.Name.Trim();

        var existing = await _dbContext.Roles.AnyAsync(r => r.Name.ToLower() == trimmedName.ToLower());
        if (existing)
        {
            throw new InvalidOperationException($"Role '{trimmedName}' already exists.");
        }

        // Rule: New roles begin with zero permissions
        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            Description = dto.Description.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Created new role '{RoleName}' with zero permissions", role.Name);

        return MapToRoleDto(role);
    }

    public async Task<RoleDto?> UpdateRoleAsync(Guid roleId, UpdateRoleDto dto)
    {
        var role = await _dbContext.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .Include(r => r.UserRoles)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role == null)
        {
            return null;
        }

        var trimmedName = dto.Name.Trim();

        // System role names are stable identifiers used by bootstrap and operations.
        if (role.IsSystemRole && !role.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("System role names cannot be modified.");
        }

        // Check if new name conflicts with another role
        if (!role.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase))
        {
            var exists = await _dbContext.Roles.AnyAsync(r => r.Name.ToLower() == trimmedName.ToLower());
            if (exists)
            {
                throw new InvalidOperationException($"Role '{trimmedName}' already exists.");
            }
        }

        role.Name = trimmedName;
        role.Description = dto.Description.Trim();
        role.UpdatedAt = DateTime.UtcNow;

        if (role.UserRoles.Count > 0)
        {
            await InvalidateUsersAsync(role.UserRoles.Select(userRole => userRole.UserId));
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Updated role '{RoleName}' ({RoleId})", role.Name, role.Id);

        return MapToRoleDto(role);
    }

    public async Task<bool> DeleteRoleAsync(Guid roleId)
    {
        var role = await _dbContext.Roles
            .Include(r => r.UserRoles)
            .FirstOrDefaultAsync(r => r.Id == roleId);
        if (role == null)
        {
            return false;
        }

        if (role.IsSystemRole)
        {
            throw new InvalidOperationException("System roles cannot be deleted.");
        }

        await InvalidateUsersAsync(role.UserRoles.Select(userRole => userRole.UserId));

        _dbContext.Roles.Remove(role);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Deleted role '{RoleName}' ({RoleId})", role.Name, role.Id);

        return true;
    }

    public async Task<RoleDto?> AssignPermissionsToRoleAsync(Guid actorUserId, Guid roleId, AssignPermissionsDto dto)
    {
        var role = await _dbContext.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role == null)
        {
            return null;
        }

        if (role.IsSystemRole)
        {
            throw new InvalidOperationException("System role permissions are managed by the service deployment.");
        }

        var targetCodes = dto.PermissionCodes.Select(c => c.Trim().ToLowerInvariant()).Distinct().ToList();

        if (targetCodes.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("Permission codes cannot be empty.");
        }

        var permissions = await _dbContext.Permissions
            .Where(p => targetCodes.Contains(p.Code.ToLower()))
            .ToListAsync();

        var unknownCodes = targetCodes
            .Except(permissions.Select(permission => permission.Code.ToLowerInvariant()))
            .OrderBy(code => code)
            .ToList();

        if (unknownCodes.Count > 0)
        {
            throw new InvalidOperationException($"Unknown permission codes: {string.Join(", ", unknownCodes)}.");
        }

        if (targetCodes.Contains(PermissionCodes.SystemRoleManage) &&
            !await HasPermissionAsync(actorUserId, PermissionCodes.SystemRoleManage))
        {
            throw new UnauthorizedAccessException("The caller is not allowed to grant system-role management permission.");
        }

        var affectedUserIds = await _dbContext.UserRoles
            .Where(userRole => userRole.RoleId == role.Id)
            .Select(userRole => userRole.UserId)
            .ToListAsync();

        // Clear existing permissions and assign new ones
        _dbContext.RolePermissions.RemoveRange(role.RolePermissions);

        foreach (var perm in permissions)
        {
            role.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                PermissionId = perm.Id,
                AssignedAt = DateTime.UtcNow
            });
        }

        role.UpdatedAt = DateTime.UtcNow;
        await InvalidateUsersAsync(affectedUserIds);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Updated permissions for role '{RoleName}'. Count: {Count}", role.Name, permissions.Count);

        return MapToRoleDto(role);
    }

    public async Task<UserDto?> AssignRolesToUserAsync(Guid actorUserId, Guid userId, AssignRolesDto dto)
    {
        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return null;
        }

        var targetNames = dto.RoleNames.Select(n => n.Trim().ToLowerInvariant()).Distinct().ToList();

        var roles = await _dbContext.Roles
            .Where(r => targetNames.Contains(r.Name.ToLower()))
            .ToListAsync();

        var unknownRoles = targetNames
            .Except(roles.Select(role => role.Name.ToLowerInvariant()))
            .OrderBy(name => name)
            .ToList();

        if (unknownRoles.Count > 0)
        {
            throw new InvalidOperationException($"Unknown role names: {string.Join(", ", unknownRoles)}.");
        }

        var changesSystemRole = user.UserRoles.Any(userRole => userRole.Role.IsSystemRole) ||
            roles.Any(role => role.IsSystemRole);
        if (changesSystemRole && !await HasPermissionAsync(actorUserId, PermissionCodes.SystemRoleManage))
        {
            throw new UnauthorizedAccessException("The caller is not allowed to assign or remove system roles.");
        }

        _dbContext.UserRoles.RemoveRange(user.UserRoles);

        foreach (var role in roles)
        {
            user.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = role.Id,
                AssignedAt = DateTime.UtcNow
            });
        }

        user.UpdatedAt = DateTime.UtcNow;
        await InvalidateUsersAsync([user.Id]);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Assigned roles {Roles} to user {UserId}", string.Join(", ", roles.Select(r => r.Name)), user.Id);

        return MapToUserDto(user);
    }

    public async Task<List<PermissionDto>> GetAllPermissionsAsync()
    {
        var permissions = await _dbContext.Permissions
            .OrderBy(p => p.Code)
            .ToListAsync();

        return permissions.Select(MapToPermissionDto).ToList();
    }

    public async Task<PermissionDto?> GetPermissionByIdAsync(Guid permissionId)
    {
        var permission = await _dbContext.Permissions
            .FirstOrDefaultAsync(p => p.Id == permissionId);

        return permission == null ? null : MapToPermissionDto(permission);
    }

    public async Task<UserDto> AdminCreateUserAsync(Guid actorUserId, AdminCreateUserDto dto)
    {
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        var existingUser = await _dbContext.Users
            .AnyAsync(u => u.Email.ToLower() == normalizedEmail);

        if (existingUser)
        {
            throw new InvalidOperationException($"User with email '{dto.Email}' already exists.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            FullName = dto.FullName.Trim(),
            PasswordHash = _passwordHasher.HashPassword(dto.Password),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        if (dto.RoleNames.Any())
        {
            var targetNames = dto.RoleNames.Select(n => n.Trim().ToLowerInvariant()).Distinct().ToList();
            var roles = await _dbContext.Roles
                .Where(r => targetNames.Contains(r.Name.ToLower()))
                .ToListAsync();

            var unknownRoles = targetNames
                .Except(roles.Select(role => role.Name.ToLowerInvariant()))
                .OrderBy(name => name)
                .ToList();

            if (unknownRoles.Count > 0)
            {
                throw new InvalidOperationException($"Unknown role names: {string.Join(", ", unknownRoles)}.");
            }

            if (roles.Any(role => role.IsSystemRole) &&
                !await HasPermissionAsync(actorUserId, PermissionCodes.SystemRoleManage))
            {
                throw new UnauthorizedAccessException("The caller is not allowed to assign system roles.");
            }

            foreach (var role in roles)
            {
                user.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = role.Id,
                    AssignedAt = DateTime.UtcNow
                });
            }
        }

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Admin created user {UserId} with email {Email}", user.Id, user.Email);

        return (await GetUserByIdAsync(user.Id))!;
    }

    public async Task<UserDto?> AdminUpdateUserAsync(Guid targetUserId, Guid currentAdminUserId, AdminUpdateUserDto dto)
    {
        var targetUser = await _dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == targetUserId);

        if (targetUser == null)
        {
            return null;
        }

        // Rule: Admins can change emails for any account except themselves; server timestamps remain immutable
        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            var normalizedNewEmail = dto.Email.Trim().ToLowerInvariant();
            if (!targetUser.Email.Equals(normalizedNewEmail, StringComparison.OrdinalIgnoreCase))
            {
                if (targetUserId == currentAdminUserId)
                {
                    throw new InvalidOperationException("Admins cannot change their own email through this endpoint.");
                }

                var exists = await _dbContext.Users.AnyAsync(u => u.Id != targetUserId && u.Email.ToLower() == normalizedNewEmail);
                if (exists)
                {
                    throw new InvalidOperationException($"Email '{dto.Email}' is already in use.");
                }

                targetUser.Email = normalizedNewEmail;
            }
        }

        targetUser.FullName = dto.FullName.Trim();
        targetUser.IsActive = dto.IsActive;

        if (!string.IsNullOrWhiteSpace(dto.NewPassword))
        {
            targetUser.PasswordHash = _passwordHasher.HashPassword(dto.NewPassword);
        }

        targetUser.UpdatedAt = DateTime.UtcNow;
        await InvalidateUsersAsync([targetUser.Id]);

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Admin updated user {UserId}", targetUser.Id);

        return (await GetUserByIdAsync(targetUser.Id))!;
    }

    public async Task<bool> AdminDeleteUserAsync(Guid targetUserId, Guid currentAdminUserId)
    {
        var targetUser = await _dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == targetUserId);

        if (targetUser == null)
        {
            return false;
        }

        // System-role accounts are the last-resort administrative recovery path.
        var isAdmin = targetUser.UserRoles.Any(ur => ur.Role.IsSystemRole);
        if (isAdmin)
        {
            throw new InvalidOperationException("System-role accounts cannot be deleted.");
        }

        _dbContext.Users.Remove(targetUser);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Admin deleted user {UserId}", targetUser.Id);

        return true;
    }

    public async Task<UserDto?> UpdateProfileAsync(Guid userId, UpdateProfileDto dto)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return null;
        }

        // Ordinary users only update their allowed profile fields (FullName).
        // Password changes are handled exclusively by /change-password.
        // Unique server data like email and timestamps (CreatedAt) are never changed
        user.FullName = dto.FullName.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("User {UserId} updated their profile", user.Id);

        return (await GetUserByIdAsync(user.Id))!;
    }

    public async Task<bool> DeleteSelfAsync(Guid userId)
    {
        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return false;
        }

        // System-role accounts cannot self-delete and remove the recovery path.
        var isAdmin = user.UserRoles.Any(ur => ur.Role.IsSystemRole);
        if (isAdmin)
        {
            throw new InvalidOperationException("System-role accounts cannot be deleted.");
        }

        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("User {UserId} self-deleted account", user.Id);

        return true;
    }

    private async Task<SessionIssueResult> CreateOrReactivateSessionAsync(
        Guid userId,
        string? requestedDeviceId,
        string? requestedDeviceKey,
        bool rememberMe)
    {
        await SessionMutationLock.WaitAsync();

        try
        {
            // Npgsql enables a retrying execution strategy. EF Core does not
            // allow a user-created transaction outside that strategy, so the
            // complete serializable session mutation must be one retriable
            // unit.
            var executionStrategy = _dbContext.Database.CreateExecutionStrategy();
            return await executionStrategy.ExecuteAsync(async () =>
            {
                IDbContextTransaction? transaction = null;

                try
                {
                    if (_dbContext.Database.IsRelational())
                    {
                        transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                    }

                    var now = DateTime.UtcNow;
                    var device = await ResolveDeviceInstallationAsync(requestedDeviceId, requestedDeviceKey, now);
                    await AcquireCapacityLocksAsync(userId, device.Installation.DeviceId);
                    var lifetime = _sessionOptions.GetLifetime(rememberMe);
                    var session = await _dbContext.ActiveSessions
                        .FirstOrDefaultAsync(item => item.UserId == userId && item.DeviceId == device.Installation.DeviceId);
                    var startsNewActiveSession = session == null ||
                        session.ExpiresAt <= now;

                    if (session != null && session.ExpiresAt <= now)
                    {
                        await ArchiveSessionsAsync([session], now, "session-expired");
                        session = null;
                    }

                    if (startsNewActiveSession)
                    {
                        var activeAccountCount = await _dbContext.ActiveSessions
                            .Where(item =>
                                item.DeviceId == device.Installation.DeviceId &&
                                item.ExpiresAt > now)
                            .Select(item => item.UserId)
                            .Distinct()
                            .CountAsync();

                        if (activeAccountCount >= MaxAccountsPerDevice)
                        {
                            throw new DeviceAccountLimitExceededException(MaxAccountsPerDevice);
                        }

                        var activeSessionsForUser = await _dbContext.ActiveSessions
                            .Where(item =>
                                item.UserId == userId &&
                                item.ExpiresAt > now)
                            .OrderBy(item => item.CreatedAt)
                            .ThenBy(item => item.Id)
                            .ToListAsync();

                        var sessionsToEvict = activeSessionsForUser
                            .Take(Math.Max(0, activeSessionsForUser.Count - (MaxAccountsPerDevice - 1)))
                            .ToList();
                        await ArchiveSessionsAsync(sessionsToEvict, now, "capacity-eviction");

                        if (session == null)
                        {
                            session = new UserSession
                            {
                                UserId = userId,
                                DeviceId = device.Installation.DeviceId,
                                SessionVersion = 0,
                                CreatedAt = now,
                                LastSeenAt = now,
                                ExpiresAt = now.Add(lifetime),
                                RememberMe = rememberMe
                            };
                            _dbContext.ActiveSessions.Add(session);
                        }
                        else
                        {
                            // Prevent a pre-logout JWT from becoming valid again when
                            // this account is signed in on the same device.
                            session.SessionVersion++;
                            session.CreatedAt = now;
                        }
                    }

                    if (session == null)
                    {
                        throw new InvalidOperationException("Authentication session creation failed.");
                    }

                    session.LastSeenAt = now;
                    session.ExpiresAt = now.Add(lifetime);
                    session.RememberMe = rememberMe;

                    await RevokeRefreshTokensAsync([session.Id], now, "new-login");

                    var refreshToken = _refreshTokenService.CreateOpaqueToken();
                    _dbContext.RefreshTokens.Add(new RefreshToken
                    {
                        UserSessionId = session.Id,
                        FamilyId = Guid.NewGuid(),
                        TokenHash = _refreshTokenService.Hash(refreshToken),
                        IssuedAt = now,
                        ExpiresAt = session.ExpiresAt
                    });

                    await _dbContext.SaveChangesAsync();
                    if (transaction != null)
                    {
                        await transaction.CommitAsync();
                    }

                    return new SessionIssueResult(session, device.DeviceKey, refreshToken);
                }
                catch
                {
                    if (transaction != null)
                    {
                        await transaction.RollbackAsync();
                    }

                    throw;
                }
                finally
                {
                    if (transaction != null)
                    {
                        await transaction.DisposeAsync();
                    }
                }
            });
        }
        finally
        {
            SessionMutationLock.Release();
        }
    }

    private async Task<AuthResponseDto> CreateAuthResponseAsync(Guid userId, SessionIssueResult issue)
    {
        var session = issue.Session;
        var fullUser = await _dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstAsync(u => u.Id == userId);

        var roles = fullUser.UserRoles
            .Select(ur => ur.Role.Name)
            .OrderBy(r => r)
            .ToList();

        var permissions = fullUser.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        var (token, expiresAt) = _jwtTokenService.GenerateToken(
            fullUser,
            session.Id,
            session.SessionVersion,
            roles,
            permissions);

        return new AuthResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            DeviceId = session.DeviceId,
            DeviceKey = issue.DeviceKey,
            RefreshToken = issue.RefreshToken,
            SessionExpiresAt = session.ExpiresAt,
            RememberMe = session.RememberMe,
            User = MapToUserDto(fullUser),
            Roles = roles,
            Permissions = permissions
        };
    }

    private async Task<DeviceResolution> ResolveDeviceInstallationAsync(
        string? requestedDeviceId,
        string? requestedDeviceKey,
        DateTime now)
    {
        var normalizedRequestedId = NormalizeRequestedDeviceId(requestedDeviceId);
        if (normalizedRequestedId != null)
        {
            var existing = await _dbContext.DeviceInstallations
                .FirstOrDefaultAsync(device => device.DeviceId == normalizedRequestedId);

            if (existing != null && existing.RevokedAt == null)
            {
                if (existing.IsLegacy)
                {
                    // Legacy clients were allowed to supply an opaque grouping
                    // value. If they later provide a key, retain the installation
                    // while upgrading it to proof-of-possession for future calls.
                    if (!string.IsNullOrWhiteSpace(requestedDeviceKey))
                    {
                        existing.DeviceKeyHash = _refreshTokenService.Hash(requestedDeviceKey);
                        existing.IsLegacy = false;
                    }

                    existing.LastSeenAt = now;
                    return new DeviceResolution(existing, null);
                }

                if (HasMatchingDeviceKey(existing, requestedDeviceKey))
                {
                    existing.LastSeenAt = now;
                    return new DeviceResolution(existing, null);
                }

                // A server-issued installation ID without its key is not
                // authoritative. Treat it like a new installation so a cleared
                // cookie/storage set can recover without consuming a permanent slot.
            }
            else if (existing == null)
            {
                // Compatibility path for the previous client contract. New clients
                // omit DeviceId and receive a server-generated installation instead.
                var legacyInstallation = new DeviceInstallation
                {
                    DeviceId = normalizedRequestedId,
                    IsLegacy = true,
                    CreatedAt = now,
                    LastSeenAt = now
                };
                _dbContext.DeviceInstallations.Add(legacyInstallation);
                return new DeviceResolution(legacyInstallation, null);
            }
        }

        var generatedDeviceId = _refreshTokenService.CreateDeviceId();
        var generatedDeviceKey = _refreshTokenService.CreateDeviceKey();
        var installation = new DeviceInstallation
        {
            DeviceId = generatedDeviceId,
            DeviceKeyHash = _refreshTokenService.Hash(generatedDeviceKey),
            IsLegacy = false,
            CreatedAt = now,
            LastSeenAt = now
        };
        _dbContext.DeviceInstallations.Add(installation);
        return new DeviceResolution(installation, generatedDeviceKey);
    }

    private async Task<bool> HasValidDeviceProofAsync(string deviceId, string? deviceKey)
    {
        var installation = await _dbContext.DeviceInstallations
            .AsNoTracking()
            .FirstOrDefaultAsync(device => device.DeviceId == deviceId);

        if (installation == null || installation.RevokedAt != null)
        {
            return false;
        }

        return installation.IsLegacy || HasMatchingDeviceKey(installation, deviceKey);
    }

    private async Task AcquireCapacityLocksAsync(Guid userId, string deviceId)
    {
        if (!IsPostgreSql())
        {
            return;
        }

        // All capacity mutations acquire the device and account advisory locks
        // in the same order. Serializable isolation remains the correctness
        // boundary, while these transaction-scoped locks make the five-account
        // and five-session decisions deterministic across Auth replicas.
        var lockKeys = new[]
        {
            $"device:{deviceId}",
            $"user:{userId:N}"
        }.OrderBy(key => key, StringComparer.Ordinal);

        foreach (var lockKey in lockKeys)
        {
            await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0));");
        }
    }

    private async Task AcquireRefreshLockAsync(string tokenHash)
    {
        if (!IsPostgreSql())
        {
            return;
        }

        var lockKey = $"refresh:{tokenHash}";
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0));");
    }

    private bool IsPostgreSql()
    {
        return _dbContext.Database.ProviderName?.Contains(
            "Npgsql",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private bool HasMatchingDeviceKey(DeviceInstallation installation, string? deviceKey)
    {
        return !string.IsNullOrWhiteSpace(deviceKey) &&
            !string.IsNullOrWhiteSpace(installation.DeviceKeyHash) &&
            string.Equals(
                installation.DeviceKeyHash,
                _refreshTokenService.Hash(deviceKey),
                StringComparison.Ordinal);
    }

    private async Task RevokeRefreshTokensAsync(
        IEnumerable<Guid> sessionIds,
        DateTime revokedAt,
        string reason)
    {
        var ids = sessionIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var tokens = await _dbContext.RefreshTokens
            .Where(token => token.UserSessionId.HasValue &&
                ids.Contains(token.UserSessionId.Value) &&
                token.RevokedAt == null)
            .ToListAsync();
        foreach (var token in tokens)
        {
            token.RevokedAt = revokedAt;
            token.RevocationReason = reason;
        }
    }

    private async Task ArchiveSessionsAsync(
        IEnumerable<UserSession> sessions,
        DateTime endedAt,
        string endReason)
    {
        var activeSessions = sessions
            .DistinctBy(session => session.Id)
            .ToList();
        if (activeSessions.Count == 0)
        {
            return;
        }

        var sessionIds = activeSessions.Select(session => session.Id).ToList();
        var refreshTokens = await _dbContext.RefreshTokens
            .Where(token => token.UserSessionId.HasValue && sessionIds.Contains(token.UserSessionId.Value))
            .ToListAsync();

        foreach (var refreshToken in refreshTokens)
        {
            refreshToken.RevokedAt ??= endedAt;
            refreshToken.RevocationReason ??= endReason;
            refreshToken.UserSessionLogId = refreshToken.UserSessionId;
            refreshToken.UserSessionId = null;
        }

        _dbContext.UserSessionLogs.AddRange(activeSessions.Select(session => new UserSessionLog
        {
            Id = session.Id,
            UserId = session.UserId,
            DeviceId = session.DeviceId,
            SessionVersion = session.SessionVersion,
            CreatedAt = session.CreatedAt,
            LastSeenAt = session.LastSeenAt,
            ExpiresAt = session.ExpiresAt,
            RememberMe = session.RememberMe,
            EndedAt = endedAt,
            EndReason = endReason
        }));
        _dbContext.ActiveSessions.RemoveRange(activeSessions);
    }

    private static UserDto MapToUserDto(User user)
    {
        var roles = user.UserRoles
            .Select(ur => ur.Role.Name)
            .OrderBy(r => r)
            .ToList();

        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            Roles = roles,
            Permissions = permissions
        };
    }

    private static RoleDto MapToRoleDto(Role role)
    {
        return new RoleDto
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            IsSystemRole = role.IsSystemRole,
            CreatedAt = role.CreatedAt,
            Permissions = role.RolePermissions
                .Select(rp => rp.Permission.Code)
                .OrderBy(c => c)
            .ToList()
        };
    }

    private static PermissionDto MapToPermissionDto(Permission permission)
    {
        return new PermissionDto
        {
            Id = permission.Id,
            Code = permission.Code,
            Description = permission.Description,
            CreatedAt = permission.CreatedAt
        };
    }

    private static string? NormalizeRequestedDeviceId(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        var normalized = deviceId.Trim().ToLowerInvariant();
        return normalized.Length <= 128 ? normalized : null;
    }

    private async Task<bool> HasPermissionAsync(Guid userId, string permissionCode)
    {
        return await _dbContext.Users
            .Where(user => user.Id == userId && user.IsActive)
            .SelectMany(user => user.UserRoles)
            .SelectMany(userRole => userRole.Role.RolePermissions)
            .AnyAsync(rolePermission => rolePermission.Permission.Code == permissionCode);
    }

    private async Task InvalidateUsersAsync(IEnumerable<Guid> userIds)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var users = await _dbContext.Users
            .Where(user => ids.Contains(user.Id))
            .ToListAsync();

        foreach (var user in users)
        {
            user.TokenVersion++;
        }

        await RevokeSessionsForUsersAsync(users);
    }

    private async Task RevokeSessionsForUsersAsync(IEnumerable<User> users)
    {
        var userIds = users
            .Select(user => user.Id)
            .Distinct()
            .ToList();
        if (userIds.Count == 0)
        {
            return;
        }

        var sessions = await _dbContext.ActiveSessions
            .Where(session => userIds.Contains(session.UserId))
            .ToListAsync();
        var revokedAt = DateTime.UtcNow;
        await ArchiveSessionsAsync(sessions, revokedAt, "account-security-change");
    }

    private sealed record DeviceResolution(DeviceInstallation Installation, string? DeviceKey);

    private sealed record SessionIssueResult(UserSession Session, string? DeviceKey, string RefreshToken);
}
