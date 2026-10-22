    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using NoName_Hotel.Domain.Entities.Models;

    namespace NoName_Hotel.Domain.Data.Configuration
    {
        public class HotelConfiguration : IEntityTypeConfiguration<Hotel>
        {
            public void Configure(EntityTypeBuilder<Hotel> builder)
            {
                builder.Property(h => h.PerNightRate)
                    .HasPrecision(18, 2);
            }
        }
    }
