using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoName_Hotel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HomepageHeroCarousel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HeroRoomImageUrl", table: "BranchWebsites",
                type: "nvarchar(1000)", maxLength: 1000, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(
                name: "HeroKitchenImageUrl", table: "BranchWebsites",
                type: "nvarchar(1000)", maxLength: 1000, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(
                name: "HeroBarImageUrl", table: "BranchWebsites",
                type: "nvarchar(1000)", maxLength: 1000, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(
                name: "HeroPoolImageUrl", table: "BranchWebsites",
                type: "nvarchar(1000)", maxLength: 1000, nullable: false, defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "HeroRoomImageUrl", table: "BranchWebsites");
            migrationBuilder.DropColumn(name: "HeroKitchenImageUrl", table: "BranchWebsites");
            migrationBuilder.DropColumn(name: "HeroBarImageUrl", table: "BranchWebsites");
            migrationBuilder.DropColumn(name: "HeroPoolImageUrl", table: "BranchWebsites");
        }
    }
}
