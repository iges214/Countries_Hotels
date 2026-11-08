using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoName_Hotel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BrowserSessionRenewal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BrowserSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    SecurityStamp = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IdleUntil = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Revoked = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrowserSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BrowserSessions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BrowserRefreshTokens",
                columns: table => new
                {
                    Hash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Used = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrowserRefreshTokens", x => x.Hash);
                    table.ForeignKey(
                        name: "FK_BrowserRefreshTokens_BrowserSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "BrowserSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BrowserRefreshTokens_SessionId",
                table: "BrowserRefreshTokens",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_BrowserSessions_UserId_ExpiresAt",
                table: "BrowserSessions",
                columns: new[] { "UserId", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrowserRefreshTokens");

            migrationBuilder.DropTable(
                name: "BrowserSessions");
        }
    }
}
