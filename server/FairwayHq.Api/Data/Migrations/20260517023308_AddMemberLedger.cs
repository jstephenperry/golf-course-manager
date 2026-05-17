using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayHq.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MemberLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    MemberId = table.Column<string>(type: "TEXT", nullable: false),
                    EntryType = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Method = table.Column<string>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", nullable: false),
                    PostedAt = table.Column<string>(type: "TEXT", nullable: false),
                    SourceKind = table.Column<string>(type: "TEXT", nullable: false),
                    SourceId = table.Column<string>(type: "TEXT", nullable: true),
                    ReversesEntryId = table.Column<string>(type: "TEXT", nullable: true),
                    VoidedAt = table.Column<string>(type: "TEXT", nullable: true),
                    VoidedByEntryId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberLedgerEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberLedgerEntries_MemberId_PostedAt",
                table: "MemberLedgerEntries",
                columns: new[] { "MemberId", "PostedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemberLedgerEntries");
        }
    }
}
