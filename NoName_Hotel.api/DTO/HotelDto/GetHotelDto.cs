namespace NoName_Hotel.api.DTO.HotelDto;
public record GetHotelDto(
    int Id,
    string Name,
    string Address,
    double Rating,
    int CountryId,
    string Country);

