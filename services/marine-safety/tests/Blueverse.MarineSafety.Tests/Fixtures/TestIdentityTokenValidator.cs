using System.Collections.Concurrent;
using Blueverse.MarineSafety.Authorization;

namespace Blueverse.MarineSafety.Tests.Fixtures;

/// <summary>
/// Deterministic account/session state for exercising the JWT validation
/// event. It mirrors the validation contract without claiming to verify the
/// production Auth-table SQL or PostgreSQL provider behavior.
/// </summary>
public sealed class TestIdentityTokenValidator : IIdentityTokenValidator
{
    private readonly ConcurrentDictionary<Guid, UserState> _users = new();
    private readonly ConcurrentDictionary<Guid, SessionState> _sessions = new();

    public void Reset()
    {
        _users.Clear();
        _sessions.Clear();
    }

    public void RegisterSession(
        Guid userId,
        int tokenVersion,
        Guid sessionId,
        int sessionVersion,
        DateTime expiresAt)
    {
        _users.TryAdd(userId, new UserState(IsActive: true, tokenVersion));
        _sessions[sessionId] = new SessionState(userId, sessionVersion, expiresAt);
    }

    public void SetUserActive(Guid userId, bool isActive) =>
        _users.AddOrUpdate(
            userId,
            _ => new UserState(isActive, TokenVersion: 0),
            (_, current) => current with { IsActive = isActive });

    public void SetTokenVersion(Guid userId, int tokenVersion) =>
        _users.AddOrUpdate(
            userId,
            _ => new UserState(IsActive: true, tokenVersion),
            (_, current) => current with { TokenVersion = tokenVersion });

    public void RevokeSession(Guid sessionId) => _sessions.TryRemove(sessionId, out _);

    public void SetSessionVersion(Guid sessionId, int sessionVersion) =>
        UpdateSession(sessionId, current => current with { SessionVersion = sessionVersion });

    public void SetSessionExpiry(Guid sessionId, DateTime expiresAt) =>
        UpdateSession(sessionId, current => current with { ExpiresAt = expiresAt });

    public void SetSessionOwner(Guid sessionId, Guid userId) =>
        UpdateSession(sessionId, current => current with { UserId = userId });

    public Task<bool> IsCurrentAsync(
        Guid userId,
        int tokenVersion,
        Guid sessionId,
        int sessionVersion,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var isCurrent = _users.TryGetValue(userId, out var user) &&
            user.IsActive &&
            user.TokenVersion == tokenVersion &&
            _sessions.TryGetValue(sessionId, out var session) &&
            session.UserId == userId &&
            session.SessionVersion == sessionVersion &&
            session.ExpiresAt > DateTime.UtcNow;

        return Task.FromResult(isCurrent);
    }

    private void UpdateSession(Guid sessionId, Func<SessionState, SessionState> update)
    {
        if (!_sessions.TryGetValue(sessionId, out var current))
        {
            throw new InvalidOperationException($"Test session {sessionId} has not been registered.");
        }

        _sessions[sessionId] = update(current);
    }

    private sealed record UserState(bool IsActive, int TokenVersion);
    private sealed record SessionState(Guid UserId, int SessionVersion, DateTime ExpiresAt);
}
