using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NoName_Hotel.Domain.Entities.Models;
using System.Reflection.Emit;

namespace NoName_Hotel.Infrastructure.Data.Configuration
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
            builder.Property(x => x.RowVersion).IsRowVersion();
            builder.Property(x => x.CreateRequestHash).HasMaxLength(64).IsUnicode(false);
            builder.HasIndex(x => new { x.UserId, x.ClientRequestId }).IsUnique()
                .HasFilter("[ClientRequestId] IS NOT NULL");
            builder.ToTable(t => t.HasCheckConstraint("CK_Bookings_RequestIdentity",
                "([ClientRequestId] IS NULL AND [CreateRequestHash] IS NULL) OR ([ClientRequestId] IS NOT NULL AND [CreateRequestHash] IS NOT NULL)"));
            builder.Property(x => x.Rooms).HasDefaultValue(1);
            builder.HasIndex(x => x.HotelId);
            builder.HasIndex(x => new { x.CheckIn, x.CheckOut });
        }
    }
}
