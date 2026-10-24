
namespace NoName_Hotel.Domain.Entities.Models
{
    public class Hotel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public double Rating { get; set; }
        public decimal PerNightRate { get; set; }
        public int TotalRooms { get; set; }

        public int CountryId{ get; set; }
        public Country? Country { get; set; }

        public ICollection<HotelAdmin> Admins { get; set; } = [];

        public ICollection<Booking> Bookings { get; set; } = [];
    }
}
