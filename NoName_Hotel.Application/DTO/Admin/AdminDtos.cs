using System.ComponentModel.DataAnnotations;
using NoName_Hotel.Application.DTO.Offerings;

namespace NoName_Hotel.Application.DTO.Admin;

public record AdminAccessDto(string Id, string FirstName, string LastName, string Email, bool IsSystemAdministrator, int[] BranchIds);
public record AdminRoomAssignment(int RoomId, int Number, string Category, string BedType, int MaxGuests, int? Guests, decimal NightlyRate);
public record AdminBookingDto(int Id, int HotelId, string GuestName, string GuestEmail, DateOnly CheckIn, DateOnly CheckOut,
    int Guests, int Rooms, decimal TotalPrice, string CurrencyCode, string Status, DateTime CreateAt, DateTime? UpdateAt,
    DateTime? ActualCheckInAt, DateTime? ActualCheckOutAt, DateTime ExpectedCheckoutAt, List<AdminRoomAssignment> Assignments);
public record RoomGuestInput(int RoomId, [Range(0, 10)] int Guests);
public class CheckInDto
{
    [Required, MinLength(1)] public List<RoomGuestInput> Rooms { get; set; } = [];
}
public class SaveWebsiteDto : IValidatableObject
{
    [Required, MaxLength(100)] public string DisplayName { get; set; } = "IGES Hotel & Suites";
    [MaxLength(1000)] public string HeroImageUrl { get; set; } = "";
    [MaxLength(1000)] public string? HeroRoomImageUrl { get; set; }
    [MaxLength(1000)] public string? HeroKitchenImageUrl { get; set; }
    [MaxLength(1000)] public string? HeroBarImageUrl { get; set; }
    [MaxLength(1000)] public string? HeroPoolImageUrl { get; set; }
    [MaxLength(254)] public string PublicEmail { get; set; } = "";
    [MaxLength(30), RegularExpression(@"^$|^\+?[0-9 ()-]{7,30}$")] public string Phone { get; set; } = "";
    [MaxLength(20000)] public string PrivacyPolicy { get; set; } = "";
    [MaxLength(20000)] public string BookingPolicy { get; set; } = "";
    public bool ContactPublished { get; set; }
    public bool PrivacyPublished { get; set; }
    public bool BookingPolicyPublished { get; set; }
    public TimeOnly CheckoutTime { get; set; } = new(12, 0);
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        (string Field, string? Url)[] heroImages =
        [
            (nameof(HeroImageUrl), HeroImageUrl),
            (nameof(HeroRoomImageUrl), HeroRoomImageUrl),
            (nameof(HeroKitchenImageUrl), HeroKitchenImageUrl),
            (nameof(HeroBarImageUrl), HeroBarImageUrl),
            (nameof(HeroPoolImageUrl), HeroPoolImageUrl)
        ];
        foreach (var (field, url) in heroImages)
            if (!string.IsNullOrWhiteSpace(url) && !MediaUrls.IsValid(url.Trim()))
                yield return new("Use an HTTPS or uploaded hero image URL.", [field]);
        if (PublicEmail.Length > 0 && !new EmailAddressAttribute().IsValid(PublicEmail)) yield return new("Enter a valid public email.");
        if (PrivacyPublished && string.IsNullOrWhiteSpace(PrivacyPolicy)) yield return new("Enter a privacy policy before publishing.");
        if (BookingPolicyPublished && string.IsNullOrWhiteSpace(BookingPolicy)) yield return new("Enter booking terms before publishing.");
    }
}
public class ContactMessageDto
{
    [Required, MaxLength(100)] public string SenderName { get; set; } = "";
    [Required, EmailAddress, MaxLength(254)] public string SenderEmail { get; set; } = "";
    [Required, MaxLength(200)] public string Subject { get; set; } = "";
    [Required, MaxLength(10000)] public string Body { get; set; } = "";
}
public class ReplyMessageDto
{
    public Guid RequestId { get; set; }
    [Required, MaxLength(10000)] public string Body { get; set; } = "";
}
public record MessageStateDto(bool IsRead, bool IsArchived);
public record AssignAdministratorDto([Required, EmailAddress] string Email);
