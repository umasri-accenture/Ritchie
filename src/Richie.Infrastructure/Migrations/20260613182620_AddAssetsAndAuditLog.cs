using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Richie.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetsAndAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Identifier = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    InvestmentStartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    InvestedAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: true),
                    PurchasePricePerUnit = table.Column<decimal>(type: "TEXT", nullable: true),
                    CurrentValue = table.Column<decimal>(type: "TEXT", nullable: false),
                    ValuationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    InvestmentMode = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    IsExcludedFromPortfolio = table.Column<bool>(type: "INTEGER", nullable: false),
                    Exchange = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    IssuePrice = table.Column<decimal>(type: "TEXT", nullable: true),
                    MaturityDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PlatformName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    PropertyAddress = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    AreaSquareFeet = table.Column<decimal>(type: "TEXT", nullable: true),
                    Weight = table.Column<decimal>(type: "TEXT", nullable: true),
                    Purity = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    AppraiserName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    PolicyNumber = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    GuaranteedReturnPercent = table.Column<decimal>(type: "TEXT", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TimestampUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Module = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Action = table.Column<int>(type: "INTEGER", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assets_UserId",
                table: "Assets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TimestampUtc",
                table: "AuditLogs",
                column: "TimestampUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Assets");

            migrationBuilder.DropTable(
                name: "AuditLogs");
        }
    }
}
