using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoName_Hotel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BookingRequestIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientRequestId",
                table: "Bookings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreateRequestHash",
                table: "Bookings",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_UserId_ClientRequestId",
                table: "Bookings",
                columns: new[] { "UserId", "ClientRequestId" },
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bookings_RequestIdentity",
                table: "Bookings",
                sql: "([ClientRequestId] IS NULL AND [CreateRequestHash] IS NULL) OR ([ClientRequestId] IS NOT NULL AND [CreateRequestHash] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_UserId_ClientRequestId",
                table: "Bookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Bookings_RequestIdentity",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "CreateRequestHash",
                table: "Bookings");
        }
    }
}
