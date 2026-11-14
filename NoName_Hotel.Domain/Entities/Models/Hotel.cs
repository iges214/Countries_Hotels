
namespace NoName_Hotel.Domain.Entities.Models
{
    public class Hotel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public bool IsDefaultBranch { get; set; }
        public string Address { get; set; } = string.Empty;
        public double Rating { get; set; }
        public decimal PerNightRate { get; set; }
        public int TotalRooms { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string CurrencyCode { get; set; } = "NGN";
        public string TimeZoneId { get; set; } = "Africa/Lagos";
        public ICollection<Room> RoomInventory { get; set; } = [];
        public ICollection<BranchOffering> Offerings { get; set; } = [];

        public int CountryId{ get; set; }
        public Country? Country { get; set; }

        public ICollection<HotelAdmin> Admins { get; set; } = [];

        public ICollection<Booking> Bookings { get; set; } = [];
    }
}
