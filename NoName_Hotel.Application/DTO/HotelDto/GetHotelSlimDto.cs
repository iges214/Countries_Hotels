namespace NoName_Hotel.Application.DTO.HotelDto
{
    public record GetHotelSlimDto
    (
        int Id,
        string Name,
        string Address,
        Double Rating
    );
}
