namespace NoName_Hotel.Common.JWT
{
    public sealed class JwtSettings
    {
        public string Issuer { get; init; } = string.Empty;
        public string Audience { get; init; } = string.Empty;
        public string key { get; init; } = string.Empty;
        public string DurationInMinutes { get; init; } = "10";
    }
}
