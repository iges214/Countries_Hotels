namespace NoName_Hotel.Application.DTO.HotelDto;
public record GetHotelDto(
    int Id,
    string Name,
    string Address,
    double Rating,
    int CountryId,
    string CountryName,
    decimal PerNightRate,
    int TotalRooms,
    string City,
    string State,
    string CurrencyCode,
    string TimeZoneId,
    bool IsActive,
    bool IsDefaultBranch)
{
    public string BrandName => NoName_Hotel.Domain.HotelBrand.Name;
    public string BrowserTimeZoneId => TimeZoneInfo.TryConvertWindowsIdToIanaId(TimeZoneId, out var iana) ? iana : TimeZoneId;
}


