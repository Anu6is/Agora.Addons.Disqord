using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Launcher.Migrations
{
    /// <inheritdoc />
    public partial class TransactionFees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PremiumListings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EntryRoleId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    RequiredEntries = table.Column<ulong>(type: "INTEGER", nullable: false),
                    EntryFee = table.Column<string>(type: "TEXT", nullable: false),
                    ExtendBy = table.Column<TimeSpan>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PremiumListings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PremiumListings_ProductListing_Id",
                        column: x => x.Id,
                        principalTable: "ProductListing",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TransactionFeeSettings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    ServerFee = table.Column<string>(type: "TEXT", nullable: true),
                    BrokerFee = table.Column<string>(type: "TEXT", nullable: true),
                    AllowEntryFee = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionFeeSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransactionFeeSettings_Emporium_Id",
                        column: x => x.Id,
                        principalTable: "Emporium",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PremiumListings");

            migrationBuilder.DropTable(
                name: "TransactionFeeSettings");
        }
    }
}
