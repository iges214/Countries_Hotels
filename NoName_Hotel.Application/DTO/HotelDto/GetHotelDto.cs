namespace NoName_Hotel.Application.DTO.HotelDto;
public record GetHotelDto(
    int Id,
    string Name,
    string Address,
    double Rating,
    int CountryId,
    string CountryName);


