namespace NoName_Hotel.api.DTO.HotelDto
{
    public record GetHotelSlimDto
    (
        int Id,
        string Name,
        string Address,
        Double Rating
    );
}
