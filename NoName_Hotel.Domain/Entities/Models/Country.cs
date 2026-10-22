namespace NoName_Hotel.Domain.Entities.Models
{
    public class Country
    {
        public int CountryId { get; set; }
        public string Name { get; set; }
        public string ShortName { get; set; }
        public IList<Hotel> Hotels { get; set; }
    }
}