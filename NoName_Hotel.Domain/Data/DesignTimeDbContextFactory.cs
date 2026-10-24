using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace NoName_Hotel.Domain.Data;
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<NoName_hotel_DbContext>
{
    public NoName_hotel_DbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__NoName_HotelDbConnectionString")
            ?? "Server=(localdb)\\mssqllocaldb;Database=NoName_HotelDb;Trusted_Connection=True;TrustServerCertificate=True";
        return new(new DbContextOptionsBuilder<NoName_hotel_DbContext>().UseSqlServer(connection).Options);
    }
}
