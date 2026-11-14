using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoName_Hotel.Infrastructure.Migrations;

public partial class ExplicitDefaultBranch : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsActive", table: "Hotels", type: "bit", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<bool>(
            name: "IsDefaultBranch", table: "Hotels", type: "bit", nullable: false, defaultValue: false);
        migrationBuilder.AddCheckConstraint(
            name: "CK_Hotels_DefaultBranchMustBeActive", table: "Hotels",
            sql: "[IsDefaultBranch] = 0 OR [IsActive] = 1");
        migrationBuilder.CreateIndex(
            name: "IX_Hotels_ActiveDefaultBranch", table: "Hotels", column: "IsDefaultBranch",
            unique: true, filter: "[IsDefaultBranch] = 1 AND [IsActive] = 1");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Hotels_ActiveDefaultBranch", table: "Hotels");
        migrationBuilder.DropCheckConstraint(name: "CK_Hotels_DefaultBranchMustBeActive", table: "Hotels");
        migrationBuilder.DropColumn(name: "IsActive", table: "Hotels");
        migrationBuilder.DropColumn(name: "IsDefaultBranch", table: "Hotels");
    }
}
