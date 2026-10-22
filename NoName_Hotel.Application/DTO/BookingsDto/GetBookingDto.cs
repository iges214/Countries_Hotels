namespace NoName_Hotel.Application.DTO.BookingsDto
{
    public record GetBookingDto
    (
        int Id,
        int HotelId,
        string HotelName,
        DateOnly CheckIn,
        DateOnly CheckOut,
        int Guests,
        decimal TotalPrice,
        string Status,
        DateTime CreateAt,
        DateTime? UpdateAt
    );
}
