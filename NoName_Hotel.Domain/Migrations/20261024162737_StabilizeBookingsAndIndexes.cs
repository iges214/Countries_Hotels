using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoName_Hotel.Domain.Migrations
{
    /// <inheritdoc />
    public partial class StabilizeBookingsAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Stop before altering data if existing records need manual cleanup.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM Hotels WHERE DATALENGTH(Name) > 200 OR DATALENGTH(Address) > 300)
                    THROW 50001, 'Hotel names or addresses exceed the new limits. Review these records before migrating.', 1;
                IF EXISTS (SELECT 1 FROM Countries WHERE DATALENGTH(Name) > 200 OR DATALENGTH(ShortName) > 20)
                    THROW 50002, 'Country names exceed the new limits. Review these records before migrating.', 1;
                IF EXISTS (SELECT Name FROM Countries GROUP BY Name HAVING COUNT(*) > 1)
                    THROW 50003, 'Duplicate country names must be resolved before migrating.', 1;
                IF EXISTS (SELECT CountryId, Name FROM Hotels GROUP BY CountryId, Name HAVING COUNT(*) > 1)
                    THROW 50004, 'Duplicate hotel names within a country must be resolved before migrating.', 1;
                IF EXISTS (SELECT [Key] FROM ApiKeys GROUP BY [Key] HAVING COUNT(*) > 1)
                    THROW 50005, 'Duplicate API keys must be resolved before migrating.', 1;
                """);
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Hotels",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "Hotels",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "TotalRooms",
                table: "Hotels",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "ShortName",
                table: "Countries",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Countries",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "Rooms",
                table: "Bookings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Bookings",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Hotels_CountryId_Name",
                table: "Hotels",
                columns: new[] { "CountryId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Hotels_CountryId_Rating",
                table: "Hotels",
                columns: new[] { "CountryId", "Rating" });

            migrationBuilder.CreateIndex(
                name: "IX_Hotels_Name",
                table: "Hotels",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Countries_Name",
                table: "Countries",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Countries_ShortName",
                table: "Countries",
                column: "ShortName");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_Key",
                table: "ApiKeys",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Hotels_CountryId_Name",
                table: "Hotels");

            migrationBuilder.DropIndex(
                name: "IX_Hotels_CountryId_Rating",
                table: "Hotels");

            migrationBuilder.DropIndex(
                name: "IX_Hotels_Name",
                table: "Hotels");

            migrationBuilder.DropIndex(
                name: "IX_Countries_Name",
                table: "Countries");

            migrationBuilder.DropIndex(
                name: "IX_Countries_ShortName",
                table: "Countries");

            migrationBuilder.DropIndex(
                name: "IX_ApiKeys_Key",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "TotalRooms",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "Rooms",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Bookings");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Hotels",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "Hotels",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "ShortName",
                table: "Countries",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Countries",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);
        }
    }
}
