using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NoName_Hotel.Domain.Entities.Models;

namespace NoName_Hotel.Infrastructure.Data.Configuration;

public sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> b)
    {
        b.HasIndex(r => new { r.HotelId, r.Number }).IsUnique();
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Room_Number", "([Number] BETWEEN 100 AND 200) OR ([Number] BETWEEN 210 AND 300) OR ([Number] BETWEEN 350 AND 400) OR ([Number] BETWEEN 480 AND 500)");
            t.HasCheckConstraint("CK_Room_TwinBeds", "(([Number] BETWEEN 160 AND 200) AND [BedType] = 'Twin' AND [MaxGuests] >= 2) OR (([Number] NOT BETWEEN 160 AND 200) AND [BedType] <> 'Twin')");
            t.HasCheckConstraint("CK_Room_HoneymoonSuite", "[IsHoneymoonSuite] = 0 OR (([Number] BETWEEN 350 AND 400 OR [Number] BETWEEN 480 AND 500) AND [MaxGuests] >= 2)");
        });
        b.Property(r => r.Description).HasMaxLength(2000);
        b.Property(r => r.BedType).HasMaxLength(100);
        b.Property(r => r.Amenities).HasMaxLength(2000);
        b.Property(r => r.PerNightRate).HasPrecision(18, 2);
        b.Property(r => r.SizeSquareMetres).HasPrecision(8, 2);
        b.HasOne(r => r.Hotel).WithMany(h => h.RoomInventory).HasForeignKey(r => r.HotelId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RoomPhotoConfiguration : IEntityTypeConfiguration<RoomPhoto>
{
    public void Configure(EntityTypeBuilder<RoomPhoto> b)
    {
        b.Property(p => p.Url).HasMaxLength(1000);
        b.Property(p => p.Caption).HasMaxLength(200);
        b.HasOne(p => p.Room).WithMany(r => r.Photos).HasForeignKey(p => p.RoomId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class BookingRoomConfiguration : IEntityTypeConfiguration<BookingRoom>
{
    public void Configure(EntityTypeBuilder<BookingRoom> b)
    {
        b.HasKey(r => new { r.BookingId, r.RoomId });
        b.Property(r => r.NightlyRate).HasPrecision(18, 2);
        b.HasOne(r => r.Booking).WithMany(b => b.RoomReservations).HasForeignKey(r => r.BookingId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(r => r.Room).WithMany(r => r.Reservations).HasForeignKey(r => r.RoomId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BranchOfferingConfiguration : IEntityTypeConfiguration<BranchOffering>
{
    public void Configure(EntityTypeBuilder<BranchOffering> b)
    {
        b.Property(o => o.Name).HasMaxLength(150);
        b.Property(o => o.Description).HasMaxLength(2000);
        b.Property(o => o.ImageUrl).HasMaxLength(1000);
        b.Property(o => o.OpeningHours).HasMaxLength(300);
        b.Property(o => o.Price).HasPrecision(18, 2);
        b.HasIndex(o => new { o.HotelId, o.Kind, o.IsPublished });
        b.HasOne(o => o.Hotel).WithMany(h => h.Offerings).HasForeignKey(o => o.HotelId).OnDelete(DeleteBehavior.Restrict);
    }
}
