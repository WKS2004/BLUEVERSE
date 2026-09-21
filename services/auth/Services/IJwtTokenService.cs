using Blueverse.Auth.Models;

namespace Blueverse.Auth.Services;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAt) GenerateToken(
        User user,
        Guid sessionId,
        int sessionVersion,
        IEnumerable<string> roles,
        IEnumerable<string> permissions);
}
