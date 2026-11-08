namespace NoName_Hotel.Application.DTO.AuthDto;

public sealed record BrowserSessionTokens(Guid SessionId, string AccessToken, string RefreshToken, DateTime ExpiresAt);
