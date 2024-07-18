using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Launcher.Migrations
{
    /// <inheritdoc />
    public partial class CustomAnnouncements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Announcements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    AnnouncementType = table.Column<int>(type: "INTEGER", nullable: false),
                    EmporiumId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Announcements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Announcements_Emporium_EmporiumId",
                        column: x => x.EmporiumId,
                        principalTable: "Emporium",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_EmporiumId",
                table: "Announcements",
                column: "EmporiumId");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_GuildId_AnnouncementType",
                table: "Announcements",
                columns: new[] { "GuildId", "AnnouncementType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Announcements");
        }
    }
}
