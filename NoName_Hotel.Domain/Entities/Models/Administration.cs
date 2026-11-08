using System.ComponentModel.DataAnnotations;

namespace NoName_Hotel.Domain.Entities.Models;

public class BranchWebsite
{
    [Key] public int HotelId { get; set; }
    public Hotel? Hotel { get; set; }
    [MaxLength(100)] public string DisplayName { get; set; } = "";
    [MaxLength(1000)] public string HeroImageUrl { get; set; } = "";
    [MaxLength(1000)] public string HeroRoomImageUrl { get; set; } = "";
    [MaxLength(1000)] public string HeroKitchenImageUrl { get; set; } = "";
    [MaxLength(1000)] public string HeroBarImageUrl { get; set; } = "";
    [MaxLength(1000)] public string HeroPoolImageUrl { get; set; } = "";
    [MaxLength(254)] public string PublicEmail { get; set; } = "";
    [MaxLength(30)] public string Phone { get; set; } = "";
    [MaxLength(20000)] public string PrivacyPolicy { get; set; } = "";
    [MaxLength(20000)] public string BookingPolicy { get; set; } = "";
    public bool ContactPublished { get; set; }
    public bool PrivacyPublished { get; set; }
    public bool BookingPolicyPublished { get; set; }
    public TimeOnly CheckoutTime { get; set; } = new(12, 0);
}

public class HotelMessage
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public Hotel? Hotel { get; set; }
    [MaxLength(100)] public string SenderName { get; set; } = "";
    [MaxLength(254)] public string SenderEmail { get; set; } = "";
    [MaxLength(200)] public string Subject { get; set; } = "";
    [MaxLength(10000)] public string Body { get; set; } = "";
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
    public bool IsArchived { get; set; }
    public ICollection<HotelMessageReply> Replies { get; set; } = [];
}

public class HotelMessageReply
{
    public Guid Id { get; set; }
    public int HotelMessageId { get; set; }
    public HotelMessage? HotelMessage { get; set; }
    [MaxLength(10000)] public string Body { get; set; } = "";
    [MaxLength(450)] public string AdministratorId { get; set; } = "";
    [MaxLength(30)] public string DeliveryStatus { get; set; } = "Sending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
