using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NoName_Hotel.Application.Contracts;
using NoName_Hotel.Application.DTO.AuthDto;
using NoName_Hotel.Common.JWT;
using NoName_Hotel.Domain.Entities.Models;

namespace NoName_Hotel.Application.Services.AuthServices;

public sealed class BrowserSessionService(
    IHotelRepository db,
    UserManager<ApplicationUser> users,
    IOptions<JwtSettings> jwtOptions,
    TimeProvider clock)
{
    public const string SessionIdClaim = "session_id";
    private static readonly TimeSpan IdleLimit = TimeSpan.FromHours(6);
    private static readonly TimeSpan MaximumSession = TimeSpan.FromHours(24);

    public Task<BrowserSessionTokens> StartAsync(ApplicationUser user) =>
        db.ExecuteSerializableAsync(async () =>
        {
            var now = clock.GetUtcNow().UtcDateTime;
            await db.Query<BrowserSession>().Where(s => s.UserId == user.Id && s.ExpiresAt <= now).ExecuteDeleteAsync();
            var session = new BrowserSession
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                SecurityStamp = user.SecurityStamp ?? "",
                IdleUntil = now + IdleLimit,
                ExpiresAt = now + MaximumSession
            };
            db.Add(session);
            var tokens = await IssueAsync(user, session, now);
            await db.SaveChangesAsync();
            return tokens;
        });

    public Task<BrowserSessionTokens?> RefreshAsync(Guid sessionId, string? refreshToken, bool activity)
    {
        if (string.IsNullOrEmpty(refreshToken)) return Task.FromResult<BrowserSessionTokens?>(null);
        return db.ExecuteSerializableAsync<BrowserSessionTokens?>(async () =>
        {
            var token = await FindTokenAsync(sessionId, refreshToken);
            if (token is null) return null;

            var session = token.Session;
            var now = clock.GetUtcNow().UtcDateTime;
            if (session.Revoked || session.IdleUntil <= now || session.ExpiresAt <= now) return null;

            // Only replay of a previously issued secret can revoke the session.
            var user = token.Used ? null : await users.FindByIdAsync(session.UserId);
            if (user is null || user.SecurityStamp != session.SecurityStamp || await users.IsLockedOutAsync(user))
            {
                session.Revoked = true;
                await db.SaveChangesAsync();
                return null;
            }

            token.Used = true;
            if (activity)
                session.IdleUntil = Min(now + IdleLimit, session.ExpiresAt);
            var tokens = await IssueAsync(user, session, now);
            await db.SaveChangesAsync();
            return tokens;
        });
    }

    public async Task RevokeAsync(Guid sessionId, string? refreshToken, string? authenticatedUserId)
    {
        await db.ExecuteSerializableAsync(async () =>
        {
            BrowserSession? session = null;
            if (authenticatedUserId is not null)
                session = await db.Query<BrowserSession>(true).SingleOrDefaultAsync(s => s.Id == sessionId && s.UserId == authenticatedUserId);
            else if (!string.IsNullOrEmpty(refreshToken))
                session = (await FindTokenAsync(sessionId, refreshToken))?.Session;
            if (session is null || session.Revoked) return false;
            session.Revoked = true;
            await db.SaveChangesAsync();
            return true;
        });
    }

    public Task<bool> IsActiveAsync(Guid sessionId, string userId)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return db.Query<BrowserSession>().AnyAsync(s => s.Id == sessionId && s.UserId == userId
            && !s.Revoked && s.IdleUntil > now && s.ExpiresAt > now);
    }

    private Task<BrowserRefreshToken?> FindTokenAsync(Guid sessionId, string value)
    {
        var hash = Hash(value);
        return db.Query<BrowserRefreshToken>(true).Include(t => t.Session)
            .SingleOrDefaultAsync(t => t.SessionId == sessionId && t.Hash == hash);
    }

    private async Task<BrowserSessionTokens> IssueAsync(ApplicationUser user, BrowserSession session, DateTime now)
    {
        var secret = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        db.Add(new BrowserRefreshToken { SessionId = session.Id, Hash = Hash(secret) });
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new("security_stamp", user.SecurityStamp ?? ""),
            new(SessionIdClaim, session.Id.ToString()),
            EpochClaim("idle_until", session.IdleUntil),
            EpochClaim("session_until", session.ExpiresAt)
        };
        claims.AddRange((await users.GetRolesAsync(user)).Select(role => new Claim(ClaimTypes.Role, role)));
        var settings = jwtOptions.Value;
        var minutes = Math.Min(int.Parse(settings.DurationInMinutes), 10);
        var expires = Min(now.AddMinutes(minutes), Min(session.IdleUntil, session.ExpiresAt));
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.key)), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(settings.Issuer, settings.Audience, claims, expires: expires, signingCredentials: credentials);
        return new(session.Id, new JwtSecurityTokenHandler().WriteToken(jwt), secret, session.ExpiresAt);
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static DateTime Min(DateTime a, DateTime b) => a <= b ? a : b;
    private static Claim EpochClaim(string name, DateTime value) =>
        new(name, new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64);
}
