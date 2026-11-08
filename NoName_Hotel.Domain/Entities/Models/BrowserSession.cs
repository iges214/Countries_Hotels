namespace NoName_Hotel.Domain.Entities.Models;

public sealed class BrowserSession
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required string SecurityStamp { get; set; }
    public DateTime IdleUntil { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool Revoked { get; set; }
}

public sealed class BrowserRefreshToken
{
    public required string Hash { get; set; }
    public Guid SessionId { get; set; }
    public BrowserSession Session { get; set; } = null!;
    public bool Used { get; set; }
}
