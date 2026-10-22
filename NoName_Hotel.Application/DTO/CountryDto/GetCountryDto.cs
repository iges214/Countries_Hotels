using NoName_Hotel.Application.DTO.HotelDto;

namespace NoName_Hotel.Application.DTO.CountryDto;

public class GetCountryDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ShortName { get; set; } = string.Empty;

    public List<GetHotelSlimDto> Hotels { get; set; } = new();
};
