namespace NoName_Hotel.Application.DTO.HotelDto
{
    public record GetAllHotelDto(
        int Id,
        string Name,
        String Address,
        double Rating,
        int CountryId,
        decimal PerNightRate,
        int TotalRooms);
}

