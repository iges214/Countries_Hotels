using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoName_Hotel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdministrationWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ActualCheckInAt",
                table: "Bookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActualCheckOutAt",
                table: "Bookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Guests",
                table: "BookingRooms",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BranchWebsites",
                columns: table => new
                {
                    HotelId = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    HeroImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PublicEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PrivacyPolicy = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: false),
                    BookingPolicy = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: false),
                    ContactPublished = table.Column<bool>(type: "bit", nullable: false),
                    PrivacyPublished = table.Column<bool>(type: "bit", nullable: false),
                    BookingPolicyPublished = table.Column<bool>(type: "bit", nullable: false),
                    CheckoutTime = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchWebsites", x => x.HotelId);
                    table.ForeignKey(
                        name: "FK_BranchWebsites_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HotelMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HotelId = table.Column<int>(type: "int", nullable: false),
                    SenderName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SenderEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HotelMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HotelMessages_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HotelMessageReplies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HotelMessageId = table.Column<int>(type: "int", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    AdministratorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    DeliveryStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HotelMessageReplies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HotelMessageReplies_HotelMessages_HotelMessageId",
                        column: x => x.HotelMessageId,
                        principalTable: "HotelMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HotelMessageReplies_HotelMessageId",
                table: "HotelMessageReplies",
                column: "HotelMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_HotelMessages_HotelId_ReceivedAt",
                table: "HotelMessages",
                columns: new[] { "HotelId", "ReceivedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BranchWebsites");

            migrationBuilder.DropTable(
                name: "HotelMessageReplies");

            migrationBuilder.DropTable(
                name: "HotelMessages");

            migrationBuilder.DropColumn(
                name: "ActualCheckInAt",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ActualCheckOutAt",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "Guests",
                table: "BookingRooms");
        }
    }
}
