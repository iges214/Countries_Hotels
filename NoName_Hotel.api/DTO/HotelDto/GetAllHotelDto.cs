namespace NoName_Hotel.api.DTO.HotelDto
{
    public record GetAllHotelDto(
        int Id,
        string Name,
        String Address,
        double Rating,
        int CountryId);
}

