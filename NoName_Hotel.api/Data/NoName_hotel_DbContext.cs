using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NoName_Hotel.api.Entities.Models;
using System.Reflection;

namespace NoName_Hotel.api.Data
{
    public class NoName_hotel_DbContext : IdentityDbContext<ApplicationUser>
    {
        public NoName_hotel_DbContext(DbContextOptions<NoName_hotel_DbContext> options) : base(options)
        {

        }

        public DbSet<Country> Countries { get; set; }
        public DbSet<Hotel> Hotels { get; set; }

        public DbSet<ApiKey> ApiKeys { get; set; }


        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApiKey>( a =>
                {
                    a.HasIndex(b => b.Key).IsUnique();
                }
                
          );
            builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        }
    }
}
