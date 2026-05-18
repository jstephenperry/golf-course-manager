using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayHq.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Holes",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "Par",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "Yardage",
                table: "Courses");

            migrationBuilder.AddColumn<string>(
                name: "BackNineId",
                table: "Courses",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FrontNineId",
                table: "Courses",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Nines",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Holes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    NineId = table.Column<string>(type: "TEXT", nullable: false),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    Par = table.Column<int>(type: "INTEGER", nullable: false),
                    HandicapIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Holes_Nines_NineId",
                        column: x => x.NineId,
                        principalTable: "Nines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NineTeeSets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    NineId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Color = table.Column<string>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NineTeeSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NineTeeSets_Nines_NineId",
                        column: x => x.NineId,
                        principalTable: "Nines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HoleYardages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    HoleId = table.Column<string>(type: "TEXT", nullable: false),
                    TeeSetId = table.Column<string>(type: "TEXT", nullable: false),
                    Yards = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HoleYardages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HoleYardages_Holes_HoleId",
                        column: x => x.HoleId,
                        principalTable: "Holes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HoleYardages_NineTeeSets_TeeSetId",
                        column: x => x.TeeSetId,
                        principalTable: "NineTeeSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Courses_BackNineId",
                table: "Courses",
                column: "BackNineId");

            migrationBuilder.CreateIndex(
                name: "IX_Courses_FrontNineId",
                table: "Courses",
                column: "FrontNineId");

            migrationBuilder.CreateIndex(
                name: "IX_Holes_NineId",
                table: "Holes",
                column: "NineId");

            migrationBuilder.CreateIndex(
                name: "IX_HoleYardages_HoleId_TeeSetId",
                table: "HoleYardages",
                columns: new[] { "HoleId", "TeeSetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HoleYardages_TeeSetId",
                table: "HoleYardages",
                column: "TeeSetId");

            migrationBuilder.CreateIndex(
                name: "IX_NineTeeSets_NineId",
                table: "NineTeeSets",
                column: "NineId");

            migrationBuilder.AddForeignKey(
                name: "FK_Courses_Nines_BackNineId",
                table: "Courses",
                column: "BackNineId",
                principalTable: "Nines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Courses_Nines_FrontNineId",
                table: "Courses",
                column: "FrontNineId",
                principalTable: "Nines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Courses_Nines_BackNineId",
                table: "Courses");

            migrationBuilder.DropForeignKey(
                name: "FK_Courses_Nines_FrontNineId",
                table: "Courses");

            migrationBuilder.DropTable(
                name: "HoleYardages");

            migrationBuilder.DropTable(
                name: "Holes");

            migrationBuilder.DropTable(
                name: "NineTeeSets");

            migrationBuilder.DropTable(
                name: "Nines");

            migrationBuilder.DropIndex(
                name: "IX_Courses_BackNineId",
                table: "Courses");

            migrationBuilder.DropIndex(
                name: "IX_Courses_FrontNineId",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "BackNineId",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "FrontNineId",
                table: "Courses");

            migrationBuilder.AddColumn<int>(
                name: "Holes",
                table: "Courses",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Par",
                table: "Courses",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Yardage",
                table: "Courses",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
