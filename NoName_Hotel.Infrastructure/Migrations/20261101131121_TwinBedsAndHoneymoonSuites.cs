using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoName_Hotel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TwinBedsAndHoneymoonSuites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHoneymoonSuite",
                table: "Rooms",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Preserve reservations and stop when an existing bed layout needs human review.
            // The conservative UTC cutoff also covers current stays in other branch time zones.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM Rooms WHERE BedType = 'Twin' AND Number NOT BETWEEN 160 AND 200)
                    THROW 50006, 'Twin rooms outside 160-200 need their numbers or bed specifications reviewed before migrating.', 1;
                IF EXISTS (
                    SELECT 1 FROM Rooms r
                    INNER JOIN BookingRooms br ON br.RoomId = r.Id
                    INNER JOIN Bookings b ON b.Id = br.BookingId
                    WHERE r.Number BETWEEN 160 AND 200
                      AND (r.BedType <> 'Twin' OR r.MaxGuests < 2)
                      AND b.Status <> 'Cancelled'
                      AND b.CheckOut > DATEADD(day, -1, CONVERT(date, SYSUTCDATETIME())))
                    THROW 50007, 'Rooms 160-200 with current or upcoming reservations need their bed layout reviewed before migrating.', 1;

                UPDATE Rooms
                SET IsPublished = CASE WHEN BedType <> 'Twin' OR MaxGuests < 2 THEN 0 ELSE IsPublished END,
                    BedType = 'Twin',
                    MaxGuests = CASE WHEN MaxGuests < 2 THEN 2 ELSE MaxGuests END
                WHERE Number BETWEEN 160 AND 200;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Room_HoneymoonSuite",
                table: "Rooms",
                sql: "[IsHoneymoonSuite] = 0 OR (([Number] BETWEEN 350 AND 400 OR [Number] BETWEEN 480 AND 500) AND [MaxGuests] >= 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Room_TwinBeds",
                table: "Rooms",
                sql: "(([Number] BETWEEN 160 AND 200) AND [BedType] = 'Twin' AND [MaxGuests] >= 2) OR (([Number] NOT BETWEEN 160 AND 200) AND [BedType] <> 'Twin')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Room_HoneymoonSuite",
                table: "Rooms");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Room_TwinBeds",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "IsHoneymoonSuite",
                table: "Rooms");
        }
    }
}
