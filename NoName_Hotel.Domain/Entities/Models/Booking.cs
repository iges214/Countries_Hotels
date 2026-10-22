using NoName_Hotel.Common.Enum;

namespace NoName_Hotel.Domain.Entities.Models
{
    public class Booking
    {
        public int Id { get; set; }

        public Hotel? Hotel { get; set; }
        public required int HotelId { get; set; }

        public ApplicationUser? User { get; set; }
        public required string UserId { get; set; }

        public DateOnly CheckIn { get; set; }
        public DateOnly CheckOut { get; set; }

        public int Guest { get; set; }

        public decimal Totalprice { get; set; }

        public DateTime CreateAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdateAt { get; set; }

        public BookingStatus Status { get; set; } = BookingStatus.Pending;



    }
}
