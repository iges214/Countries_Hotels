    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using NoName_Hotel.Domain.Entities.Models;

    namespace NoName_Hotel.Infrastructure.Data.Configuration
    {
        public class HotelConfiguration : IEntityTypeConfiguration<Hotel>
        {
            public void Configure(EntityTypeBuilder<Hotel> builder)
            {
                builder.Property(h => h.City).HasMaxLength(100);
                builder.Property(h => h.State).HasMaxLength(100);
                builder.Property(h => h.CurrencyCode).HasMaxLength(3);
                builder.Property(h => h.TimeZoneId).HasMaxLength(100);
                builder.Property(h => h.PerNightRate)
                    .HasPrecision(18, 2);
            }
        }
    }
