using NoName_Hotel.Application.DTO.HotelDto;
using NoName_Hotel.Common.QuerableXtensions.Paging;

namespace NoName_Hotel.Application.DTO.CountryDto
{
    public class GetCountryHotelsDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public PageResult<GetHotelSlimDto> Hotels { get; set; } = new();
    }
}
