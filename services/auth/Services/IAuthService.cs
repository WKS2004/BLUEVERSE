using Blueverse.Auth.Dtos;

namespace Blueverse.Auth.Services;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto dto);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto dto);
    Task<AuthResponseDto> RefreshAsync(RefreshTokenRequestDto dto);
    Task<int> ArchiveExpiredSessionsAsync(CancellationToken cancellationToken = default);
    Task<List<SessionDto>> GetSessionsAsync(Guid userId, Guid? currentSessionId);
    Task<bool> RevokeSessionAsync(Guid userId, Guid sessionId, Guid? currentSessionId, string? currentPassword);
    Task<bool> LogoutCurrentDeviceAsync(Guid userId, Guid sessionId);
    Task<bool> LogoutAccountOnCurrentDeviceAsync(Guid actorUserId, Guid sessionId, Guid targetUserId);
    Task<bool> LogoutAllDevicesAsync(Guid userId, string currentPassword);
    Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordDto dto);
    Task<UserDto?> GetUserByIdAsync(Guid userId);
    Task<List<UserDto>> GetAllUsersAsync();
    Task<List<RoleDto>> GetAllRolesAsync();
    Task<RoleDto?> GetRoleByIdAsync(Guid roleId);
    Task<RoleDto> CreateRoleAsync(CreateRoleDto dto);
    Task<RoleDto?> UpdateRoleAsync(Guid roleId, UpdateRoleDto dto);
    Task<bool> DeleteRoleAsync(Guid roleId);
    Task<RoleDto?> AssignPermissionsToRoleAsync(Guid actorUserId, Guid roleId, AssignPermissionsDto dto);
    Task<UserDto?> AssignRolesToUserAsync(Guid actorUserId, Guid userId, AssignRolesDto dto);
    Task<List<PermissionDto>> GetAllPermissionsAsync();
    Task<PermissionDto?> GetPermissionByIdAsync(Guid permissionId);

    // Admin user operations
    Task<UserDto> AdminCreateUserAsync(Guid actorUserId, AdminCreateUserDto dto);
    Task<UserDto?> AdminUpdateUserAsync(Guid targetUserId, Guid currentAdminUserId, AdminUpdateUserDto dto);
    Task<bool> AdminDeleteUserAsync(Guid targetUserId, Guid currentAdminUserId);

    // User self-service operations
    Task<UserDto?> UpdateProfileAsync(Guid userId, UpdateProfileDto dto);
    Task<bool> DeleteSelfAsync(Guid userId);
}
