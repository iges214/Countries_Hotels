namespace NoName_Hotel.Domain.Entities.Models
{
    public class Country
    {
        public int CountryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ShortName { get; set; } = string.Empty;
        public IList<Hotel> Hotels { get; set; } = [];
    }
}
