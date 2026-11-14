using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NoName_Hotel.Domain.Entities.Models;
using System.Reflection;
using System.Reflection.Emit;

namespace NoName_Hotel.Infrastructure.Data
{
    public class NoName_hotel_DbContext(DbContextOptions<NoName_hotel_DbContext> options) : IdentityDbContext<ApplicationUser>(options)
    { public DbSet<Country> Countries { get; set; }
        public DbSet<Hotel> Hotels { get; set; }

        public DbSet<ApiKey> ApiKeys { get; set; }

        public DbSet<HotelAdmin> HotelAdmins { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<RoomPhoto> RoomPhotos { get; set; }
        public DbSet<BookingRoom> BookingRooms { get; set; }
        public DbSet<BranchOffering> BranchOfferings { get; set; }
        public DbSet<BranchWebsite> BranchWebsites { get; set; }
        public DbSet<HotelMessage> HotelMessages { get; set; }
        public DbSet<HotelMessageReply> HotelMessageReplies { get; set; }


        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
            builder.Entity<BrowserSession>(session =>
            {
                session.ToTable("BrowserSessions");
                session.Property(s => s.UserId).HasMaxLength(450);
                session.Property(s => s.SecurityStamp).HasMaxLength(128);
                session.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
                session.HasIndex(s => new { s.UserId, s.ExpiresAt });
            });
            builder.Entity<BrowserRefreshToken>(token =>
            {
                token.ToTable("BrowserRefreshTokens");
                token.HasKey(t => t.Hash);
                token.Property(t => t.Hash).HasMaxLength(64).IsUnicode(false);
                token.HasOne(t => t.Session).WithMany().HasForeignKey(t => t.SessionId).OnDelete(DeleteBehavior.Cascade);
            });
            builder.Entity<BranchWebsite>().HasOne(w => w.Hotel).WithOne().HasForeignKey<BranchWebsite>(w => w.HotelId);
            builder.Entity<HotelMessage>().HasIndex(m => new { m.HotelId, m.ReceivedAt });
            builder.Entity<HotelMessageReply>().HasOne(r => r.HotelMessage).WithMany(m => m.Replies).HasForeignKey(r => r.HotelMessageId);
            builder.Entity<Country>().Property(c => c.Name).HasMaxLength(100);
            builder.Entity<Country>().Property(c => c.ShortName).HasMaxLength(10);
            builder.Entity<Hotel>().Property(h => h.Name).HasMaxLength(100);
            builder.Entity<Hotel>().Property(h => h.Address).HasMaxLength(150);
            builder.Entity<ApiKey>().HasIndex(k => k.Key).IsUnique();
            builder.Entity<Hotel>().HasIndex(h => new { h.CountryId, h.Name }).IsUnique();
            builder.Entity<Hotel>().ToTable(t => t.HasCheckConstraint(
                "CK_Hotels_DefaultBranchMustBeActive", "[IsDefaultBranch] = 0 OR [IsActive] = 1"));
            builder.Entity<Hotel>().HasIndex(h => h.IsDefaultBranch).IsUnique()
                .HasFilter("[IsDefaultBranch] = 1 AND [IsActive] = 1")
                .HasDatabaseName("IX_Hotels_ActiveDefaultBranch");

            builder.Entity<Country>()
                .HasIndex(e => e.Name)
                .IsUnique()
                .HasDatabaseName("IX_Countries_Name");


            builder.Entity<Country>()
               .HasIndex(e => e.ShortName)
               .HasDatabaseName("IX_Countries_ShortName");

            builder.Entity<Hotel>()
               .HasIndex(e => e.Name)
               .HasDatabaseName("IX_Hotels_Name");

            builder.Entity<Hotel>()
               .HasIndex(e => e.CountryId)
               .HasDatabaseName("IX_Hotels_CountryId");

            builder.Entity<Hotel>()
               .HasIndex(e => new {e.CountryId, e.Rating})
               .HasDatabaseName("IX_Hotels_CountryId_Rating");


        }


    }
}
