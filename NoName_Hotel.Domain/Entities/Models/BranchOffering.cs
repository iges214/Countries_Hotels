namespace NoName_Hotel.Domain.Entities.Models;

public enum OfferingKind { Meal = 1, Pool = 2, OutdoorActivity = 3, Bar = 4, Lounge = 5 }

public class BranchOffering
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public Hotel? Hotel { get; set; }
    public OfferingKind Kind { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string OpeningHours { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public bool IsPublished { get; set; }
}
