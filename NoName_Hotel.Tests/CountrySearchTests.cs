using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NoName_Hotel.Application.DTO.CountryDto;
using NoName_Hotel.Domain.Entities.Models;
using NoName_Hotel.Infrastructure.Data;
using Xunit;

namespace NoName_Hotel.Tests;

public class CountrySearchTests
{
    [Theory]
    [InlineData(" gb ", 2)]
    [InlineData("BR", 2)]
    [InlineData("b", 2)]
    [InlineData(" geri ", 1)]
    [InlineData("missing", 0)]
    public async Task Country_search_matches_any_part_of_names_and_short_names(string search, int expectedId)
    {
        using var app = new TestApplication();
        await app.SeedAsync();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>();
            db.Countries.Add(new Country { CountryId = 2, Name = "United Kingdom", ShortName = "GBR" });
            await db.SaveChangesAsync();
        }
        using var client = app.AuthenticatedClient();
        var countries = (await client.GetFromJsonAsync<GetCountriesDto[]>(
            "/api/v1/countries?search=" + Uri.EscapeDataString(search)))!;
        if (expectedId == 0) Assert.Empty(countries);
        else Assert.Equal(expectedId, Assert.Single(countries).Id);
    }

    [Fact]
    public async Task Hotel_search_keeps_country_activity_sorting_and_pagination_filters()
    {
        using var app = new TestApplication();
        await app.SeedAsync();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>();
            db.Countries.Add(new Country { CountryId = 2, Name = "Ghana", ShortName = "GH" });
            db.Hotels.AddRange(
                new Hotel { Id = 2, CountryId = 1, Name = "Garden Hotel Annex" },
                new Hotel { Id = 3, CountryId = 1, Name = "Closed Hotel", IsActive = false },
                new Hotel { Id = 4, CountryId = 2, Name = "Other Country Hotel" });
            await db.SaveChangesAsync();
        }
        using var client = app.AuthenticatedClient();
        var first = (await client.GetFromJsonAsync<GetCountryHotelsDto>(
            "/api/v1/countries/1/hotels?search=%20hotel%20&sortBy=name&pageNumber=1&pageSize=1"))!;
        Assert.Equal(2, first.Hotels.Metadata.TotalCount);
        Assert.True(first.Hotels.Metadata.HasNext);
        Assert.Equal(2, Assert.Single(first.Hotels.Data).Id);
        var second = (await client.GetFromJsonAsync<GetCountryHotelsDto>(
            "/api/v1/countries/1/hotels?search=HOTEL&sortBy=name&pageNumber=2&pageSize=1"))!;
        Assert.Equal(1, Assert.Single(second.Hotels.Data).Id);
        var missing = (await client.GetFromJsonAsync<GetCountryHotelsDto>(
            "/api/v1/countries/1/hotels?search=missing"))!;
        Assert.Empty(missing.Hotels.Data);
        Assert.Equal(0, missing.Hotels.Metadata.TotalCount);
    }
}
