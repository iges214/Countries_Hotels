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
        DateTime? UpdateAt,
        int Rooms,
        int[] RoomIds,
        string CurrencyCode
    )
    {
        public bool IsCheckedIn { get; init; }
        public bool IsCheckedOut { get; init; }
        public string TimeZoneId { get; init; } = "Africa/Lagos";
    }
}
