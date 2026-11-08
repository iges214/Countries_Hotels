using System.ComponentModel.DataAnnotations;
using NoName_Hotel.Domain.Entities.Models;

namespace NoName_Hotel.Application.DTO.Offerings;

public class SaveOfferingDto : IValidatableObject
{
    [EnumDataType(typeof(OfferingKind))] public OfferingKind Kind { get; set; }
    [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(2000)] public string Description { get; set; } = string.Empty;
    [MaxLength(1000)] public string ImageUrl { get; set; } = string.Empty;
    [Required, MaxLength(300)] public string OpeningHours { get; set; } = string.Empty;
    [Range(typeof(decimal), "0", "9999999999999999.99")] public decimal? Price { get; set; }
    public bool IsPublished { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (ImageUrl.Length > 0 && !MediaUrls.IsValid(ImageUrl))
            yield return new ValidationResult("Image URL must be HTTPS or a local uploaded image.", [nameof(ImageUrl)]);
    }
}

public static class MediaUrls
{
    public static bool IsValid(string url) =>
        (url.StartsWith("/uploads/", StringComparison.Ordinal) && !url.Contains("..") && !url.Contains('\\')) ||
        (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps);
}

public record OfferingDto(int Id, int HotelId, string Kind, string Name, string Description,
    string ImageUrl, string OpeningHours, decimal? Price, string CurrencyCode, bool IsPublished);
