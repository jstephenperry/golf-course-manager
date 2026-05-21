using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayHq.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConcurrencyTokensAndMoneyConverter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Tabs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Members",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "Tabs");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Members");
        }
    }
}
