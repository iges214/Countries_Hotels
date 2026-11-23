using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NoName_Hotel.Infrastructure.Data.Configuration
{
    public class RoleConfiguration : IEntityTypeConfiguration<IdentityRole>
    {
        public void Configure(EntityTypeBuilder<IdentityRole> builder)
        {
            // IdentityRole generates a random stamp in its constructor. Seed metadata must
            // match the existing snapshot; runtime role updates manage their own stamps.
            builder.HasData(
                 new IdentityRole
                 {
                     Id = "c78e8f15-6a6c-4c8a-b5d1-98394b071953",
                     ConcurrencyStamp = null,
                     Name = "Administrator",
                     NormalizedName = "ADMINISTRATOR"
                 },
                 new IdentityRole
                 {
                     Id = "36aac992-72ff-4527-9008-52e7c145ca39",
                     ConcurrencyStamp = null,
                     Name = "User",
                     NormalizedName = "USER"
                 },
                 new IdentityRole
                 {
                     Id = "36aac992-72ff-4527-9008-52e7c145cb52",
                     ConcurrencyStamp = null,
                     Name = "Hotel Admin",
                     NormalizedName = "HOTEL ADMIN"
                 }
            );
        }
    }

}
