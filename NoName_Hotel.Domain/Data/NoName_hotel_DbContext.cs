using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NoName_Hotel.Domain.Entities.Models;
using System.Reflection;
using System.Reflection.Emit;

namespace NoName_Hotel.Domain.Data
{
    public class NoName_hotel_DbContext(DbContextOptions<NoName_hotel_DbContext> options) : IdentityDbContext<ApplicationUser>(options)
    { public DbSet<Country> Countries { get; set; }
        public DbSet<Hotel> Hotels { get; set; }

        public DbSet<ApiKey> ApiKeys { get; set; }

        public DbSet<HotelAdmin> HotelAdmins { get; set; }
        public DbSet<Booking> Bookings { get; set; }


        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());


        }


    }
}
