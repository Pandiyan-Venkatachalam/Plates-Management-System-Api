using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VinayagaPlates.Application.Migrations
{
    /// <inheritdoc />
    public partial class AddSalePaymentTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalePayments",
                columns: table => new
                {
                    PaymentId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SaleId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    PaymentMethod = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalePayments", x => x.PaymentId);
                    table.ForeignKey(
                        name: "FK_SalePayments_BusinessAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "BusinessAccounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SalePayments_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "SaleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalePayments_AccountId",
                table: "SalePayments",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SalePayments_SaleId",
                table: "SalePayments",
                column: "SaleId");

            // Custom SQL to migrate legacy "SALE" AccountTransactions into "SalePayments"
            migrationBuilder.Sql(@"
                INSERT INTO ""SalePayments"" (""SaleId"", ""Amount"", ""AccountId"", ""PaymentMethod"", ""Notes"", ""CreatedBy"", ""CreatedAt"", ""UpdatedBy"")
                SELECT 
                    CAST(""ReferenceId"" AS integer),
                    ""Amount"",
                    ""AccountId"",
                    'LEGACY',
                    ""Description"",
                    ""CreatedBy"",
                    ""CreatedAt"",
                    ''
                FROM ""AccountTransactions""
                WHERE ""ReferenceType"" = 'SALE';

                UPDATE ""AccountTransactions""
                SET ""ReferenceType"" = 'SALE_PAYMENT',
                    ""ReferenceId"" = sp.""PaymentId""::text
                FROM ""SalePayments"" sp
                WHERE ""AccountTransactions"".""ReferenceType"" = 'SALE'
                  AND CAST(""AccountTransactions"".""ReferenceId"" AS integer) = sp.""SaleId"";
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalePayments");
        }
    }
}
