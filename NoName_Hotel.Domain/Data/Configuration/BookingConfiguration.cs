using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NoName_Hotel.Domain.Entities.Models;
using System.Reflection.Emit;

namespace NoName_Hotel.Domain.Data.Configuration
{
    public class BookingConfiguration : IEntityTypeConfiguration<Booking>
    {
        public void Configure(EntityTypeBuilder<Booking> builder)
        {
            builder.Property(q => q.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            builder.Property(q => q.Totalprice)
                      .HasPrecision(18, 2);

            builder.HasIndex(x => x.UserId);
            builder.HasIndex(x => x.HotelId);
            builder.HasIndex(x => new { x.CheckIn, x.CheckOut });
        }
    }
}
