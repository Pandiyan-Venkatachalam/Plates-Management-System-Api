using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VinayagaPlates.Application.Migrations
{
    /// <inheritdoc />
    public partial class AddRolesAndPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RolePermissions_Roles_RoleId",
                table: "RolePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_Roles_RoleId",
                table: "UserRoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Roles",
                table: "Roles");

            migrationBuilder.RenameTable(
                name: "Roles",
                newName: "vp_ms_Role");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "vp_ms_Role",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "vp_ms_Role",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "vp_ms_Role",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "vp_ms_Role",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "vp_ms_Role",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "vp_ms_Role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "vp_ms_Role",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "vp_ms_Role",
                type: "text",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_vp_ms_Role",
                table: "vp_ms_Role",
                column: "RoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_RolePermissions_vp_ms_Role_RoleId",
                table: "RolePermissions",
                column: "RoleId",
                principalTable: "vp_ms_Role",
                principalColumn: "RoleId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_vp_ms_Role_RoleId",
                table: "UserRoles",
                column: "RoleId",
                principalTable: "vp_ms_Role",
                principalColumn: "RoleId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RolePermissions_vp_ms_Role_RoleId",
                table: "RolePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_vp_ms_Role_RoleId",
                table: "UserRoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_vp_ms_Role",
                table: "vp_ms_Role");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "vp_ms_Role");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "vp_ms_Role");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "vp_ms_Role");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "vp_ms_Role");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "vp_ms_Role");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "vp_ms_Role");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "vp_ms_Role");

            migrationBuilder.RenameTable(
                name: "vp_ms_Role",
                newName: "Roles");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Roles",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Roles",
                table: "Roles",
                column: "RoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_RolePermissions_Roles_RoleId",
                table: "RolePermissions",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "RoleId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_Roles_RoleId",
                table: "UserRoles",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "RoleId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
