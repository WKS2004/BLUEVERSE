using Microsoft.EntityFrameworkCore;
using Blueverse.Auth.Models;

namespace Blueverse.Auth.Data;

public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<DeviceInstallation> DeviceInstallations => Set<DeviceInstallation>();
    public DbSet<UserSession> ActiveSessions => Set<UserSession>();
    public DbSet<UserSessionLog> UserSessionLogs => Set<UserSessionLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Email).IsRequired().HasMaxLength(256);
            entity.Property(u => u.FullName).IsRequired().HasMaxLength(128);
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.Property(u => u.TokenVersion).IsRequired();
            entity.Property(u => u.CreatedAt).IsRequired();
        });

        // User sessions
        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.HasKey(session => session.Id);
            entity.HasIndex(session => new { session.UserId, session.DeviceId }).IsUnique();
            entity.HasIndex(session => session.DeviceId);
            entity.HasIndex(session => new { session.UserId, session.ExpiresAt });
            entity.Property(session => session.DeviceId).IsRequired().HasMaxLength(128);
            entity.Property(session => session.SessionVersion).IsRequired();
            entity.Property(session => session.CreatedAt).IsRequired();
            entity.Property(session => session.LastSeenAt).IsRequired();
            entity.Property(session => session.ExpiresAt).IsRequired();
            entity.Property(session => session.RememberMe).IsRequired();
            entity.ToTable("ActiveSessions");
            entity.HasOne(session => session.User)
                .WithMany(user => user.Sessions)
                .HasForeignKey(session => session.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(session => session.DeviceInstallation)
                .WithMany(device => device.Sessions)
                .HasForeignKey(session => session.DeviceId)
                .HasPrincipalKey(device => device.DeviceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Archived session lifecycle records. These rows are never used for
        // authentication decisions; they exist for audit and support history.
        modelBuilder.Entity<UserSessionLog>(entity =>
        {
            entity.HasKey(session => session.Id);
            entity.HasIndex(session => new { session.UserId, session.EndedAt });
            entity.HasIndex(session => new { session.DeviceId, session.EndedAt });
            entity.Property(session => session.DeviceId).IsRequired().HasMaxLength(128);
            entity.Property(session => session.SessionVersion).IsRequired();
            entity.Property(session => session.CreatedAt).IsRequired();
            entity.Property(session => session.LastSeenAt).IsRequired();
            entity.Property(session => session.ExpiresAt).IsRequired();
            entity.Property(session => session.RememberMe).IsRequired();
            entity.Property(session => session.EndedAt).IsRequired();
            entity.Property(session => session.EndReason).IsRequired().HasMaxLength(64);
            entity.ToTable("UserSessionLogs");
            entity.HasOne(session => session.User)
                .WithMany(user => user.SessionLogs)
                .HasForeignKey(session => session.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(session => session.DeviceInstallation)
                .WithMany(device => device.SessionLogs)
                .HasForeignKey(session => session.DeviceId)
                .HasPrincipalKey(device => device.DeviceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Server-issued device installations
        modelBuilder.Entity<DeviceInstallation>(entity =>
        {
            entity.HasKey(device => device.DeviceId);
            entity.Property(device => device.DeviceId).IsRequired().HasMaxLength(128);
            entity.HasIndex(device => device.DeviceKeyHash).IsUnique();
            entity.Property(device => device.DeviceKeyHash).HasMaxLength(128);
            entity.Property(device => device.IsLegacy).IsRequired();
            entity.Property(device => device.CreatedAt).IsRequired();
            entity.Property(device => device.LastSeenAt).IsRequired();
        });

        // Rotating, hashed refresh tokens
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(token => token.Id);
            entity.HasIndex(token => token.TokenHash).IsUnique();
            entity.HasIndex(token => new { token.UserSessionId, token.ExpiresAt });
            entity.HasIndex(token => new { token.UserSessionLogId, token.ExpiresAt });
            entity.HasIndex(token => token.FamilyId);
            entity.Property(token => token.TokenHash).IsRequired().HasMaxLength(128);
            entity.Property(token => token.IssuedAt).IsRequired();
            entity.Property(token => token.ExpiresAt).IsRequired();
            entity.Property(token => token.RevocationReason).HasMaxLength(64);
            entity.HasOne(token => token.UserSession)
                .WithMany(session => session.RefreshTokens)
                .HasForeignKey(token => token.UserSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(token => token.UserSessionLog)
                .WithMany(session => session.RefreshTokens)
                .HasForeignKey(token => token.UserSessionLogId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(
                "RefreshTokens",
                table => table.HasCheckConstraint(
                    "CK_RefreshTokens_ExactlyOneSession",
                    "(\"UserSessionId\" IS NOT NULL AND \"UserSessionLogId\" IS NULL) OR (\"UserSessionId\" IS NULL AND \"UserSessionLogId\" IS NOT NULL)"));
        });

        // Role
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => r.Name).IsUnique();
            entity.Property(r => r.Name).IsRequired().HasMaxLength(64);
            entity.Property(r => r.Description).HasMaxLength(256);
            entity.Property(r => r.IsSystemRole).IsRequired();
        });

        // Permission
        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.Code).IsUnique();
            entity.Property(p => p.Code).IsRequired().HasMaxLength(64);
            entity.Property(p => p.Description).HasMaxLength(256);
        });

        // UserRole (many-to-many)
        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(ur => new { ur.UserId, ur.RoleId });
            entity.HasOne(ur => ur.User)
                  .WithMany(u => u.UserRoles)
                  .HasForeignKey(ur => ur.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(ur => ur.Role)
                  .WithMany(r => r.UserRoles)
                  .HasForeignKey(ur => ur.RoleId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // RolePermission (many-to-many)
        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(rp => new { rp.RoleId, rp.PermissionId });
            entity.HasOne(rp => rp.Role)
                  .WithMany(r => r.RolePermissions)
                  .HasForeignKey(rp => rp.RoleId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(rp => rp.Permission)
                  .WithMany(p => p.RolePermissions)
                  .HasForeignKey(rp => rp.PermissionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Seed foundational permissions
        var permUserReadId = Guid.Parse("11111111-1111-1111-1111-111111111101");
        var permUserManageId = Guid.Parse("11111111-1111-1111-1111-111111111102");
        var permRoleReadId = Guid.Parse("11111111-1111-1111-1111-111111111103");
        var permRoleManageId = Guid.Parse("11111111-1111-1111-1111-111111111104");
        var permPermissionReadId = Guid.Parse("11111111-1111-1111-1111-111111111105");
        var permSystemRoleManageId = Guid.Parse("11111111-1111-1111-1111-111111111106");

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Permission>().HasData(
            new Permission { Id = permUserReadId, Code = "auth.user.read", Description = "Read user accounts", CreatedAt = seedDate },
            new Permission { Id = permUserManageId, Code = "auth.user.manage", Description = "Manage user accounts and roles", CreatedAt = seedDate },
            new Permission { Id = permRoleReadId, Code = "auth.role.read", Description = "Read roles and permissions", CreatedAt = seedDate },
            new Permission { Id = permRoleManageId, Code = "auth.role.manage", Description = "Create roles and assign permissions", CreatedAt = seedDate },
            new Permission { Id = permPermissionReadId, Code = "auth.permission.read", Description = "Read all system permissions", CreatedAt = seedDate },
            new Permission { Id = permSystemRoleManageId, Code = "auth.role.system.manage", Description = "Assign or manage system roles", CreatedAt = seedDate }
        );

        // Seed Admin role
        var adminRoleId = Guid.Parse("22222222-2222-2222-2222-222222222201");
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = adminRoleId, Name = "Admin", Description = "System Administrator with full management access", IsSystemRole = true, CreatedAt = seedDate }
        );

        // Assign all foundational permissions to Admin role
        modelBuilder.Entity<RolePermission>().HasData(
            new RolePermission { RoleId = adminRoleId, PermissionId = permUserReadId, AssignedAt = seedDate },
            new RolePermission { RoleId = adminRoleId, PermissionId = permUserManageId, AssignedAt = seedDate },
            new RolePermission { RoleId = adminRoleId, PermissionId = permRoleReadId, AssignedAt = seedDate },
            new RolePermission { RoleId = adminRoleId, PermissionId = permRoleManageId, AssignedAt = seedDate },
            new RolePermission { RoleId = adminRoleId, PermissionId = permPermissionReadId, AssignedAt = seedDate },
            new RolePermission { RoleId = adminRoleId, PermissionId = permSystemRoleManageId, AssignedAt = seedDate }
        );
    }
}
