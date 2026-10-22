using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NoName_Hotel.Domain.Entities.Models;

namespace NoName_Hotel.Domain.Data.Configuration
{
    public class HotelAdminConfiguration : IEntityTypeConfiguration<HotelAdmin>
    {
        public void Configure(EntityTypeBuilder<HotelAdmin> builder)
        {
            builder.HasOne(x => x.Hotel)
                .WithMany(x => x.Admins)
                .HasForeignKey(x => x.HotelId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => x.HotelId);
            builder.HasIndex(x => x.UserId);
        }
    }
}