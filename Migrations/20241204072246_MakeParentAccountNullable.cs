using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClearBooksFYP.Migrations
{
    /// <inheritdoc />
    public partial class MakeParentAccountNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Description",
                table: "chart_of_accounts",
                newName: "description");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "chart_of_accounts",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ParentAccount",
                table: "chart_of_accounts",
                newName: "parent_account");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "chart_of_accounts",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "AccountType",
                table: "chart_of_accounts",
                newName: "account_type");

            migrationBuilder.RenameColumn(
                name: "AccountName",
                table: "chart_of_accounts",
                newName: "account_name");

            migrationBuilder.RenameColumn(
                name: "AccountId",
                table: "chart_of_accounts",
                newName: "account_id");

            migrationBuilder.RenameColumn(
                name: "AccountNumber",
                table: "chart_of_accounts",
                newName: "account_number");

            migrationBuilder.AlterColumn<string>(
                name: "parent_account",
                table: "chart_of_accounts",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "description",
                table: "chart_of_accounts",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "chart_of_accounts",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "parent_account",
                table: "chart_of_accounts",
                newName: "ParentAccount");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "chart_of_accounts",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "account_type",
                table: "chart_of_accounts",
                newName: "AccountType");

            migrationBuilder.RenameColumn(
                name: "account_name",
                table: "chart_of_accounts",
                newName: "AccountName");

            migrationBuilder.RenameColumn(
                name: "account_id",
                table: "chart_of_accounts",
                newName: "AccountId");

            migrationBuilder.RenameColumn(
                name: "account_number",
                table: "chart_of_accounts",
                newName: "AccountNumber");

            migrationBuilder.AlterColumn<string>(
                name: "ParentAccount",
                table: "chart_of_accounts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
