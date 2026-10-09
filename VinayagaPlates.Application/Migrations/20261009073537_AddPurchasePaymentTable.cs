using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VinayagaPlates.Application.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchasePaymentTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PurchasePayments",
                columns: table => new
                {
                    PaymentId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PurchaseId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Notes = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchasePayments", x => x.PaymentId);
                    table.ForeignKey(
                        name: "FK_PurchasePayments_BusinessAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "BusinessAccounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchasePayments_Purchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalTable: "Purchases",
                        principalColumn: "PurchaseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchasePayments_AccountId",
                table: "PurchasePayments",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchasePayments_PurchaseId",
                table: "PurchasePayments",
                column: "PurchaseId");

            // Custom Data Migration: Move existing PURCHASE transactions into the new PurchasePayments table
            migrationBuilder.Sql(@"
                INSERT INTO ""PurchasePayments"" (""PurchaseId"", ""Amount"", ""AccountId"", ""PaymentMethod"", ""Notes"", ""CreatedBy"", ""CreatedAt"")
                SELECT 
                    CAST(""ReferenceId"" AS INT), 
                    ""Amount"", 
                    ""AccountId"", 
                    'CASH', 
                    ""Description"", 
                    ""CreatedBy"", 
                    ""CreatedAt""
                FROM ""AccountTransactions""
                WHERE ""ReferenceType"" = 'PURCHASE' AND ""ReferenceId"" ~ '^\d+$' AND ""TransactionType"" = 'DEBIT';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchasePayments");
        }
    }
}
