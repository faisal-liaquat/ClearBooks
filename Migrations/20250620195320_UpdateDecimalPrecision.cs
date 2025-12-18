using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClearBooksFYP.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDecimalPrecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "voucher_number",
                table: "voucher_header",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "voucher_date",
                table: "voucher_header",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "date");

            migrationBuilder.AlterColumn<string>(
                name: "transaction_type",
                table: "voucher_header",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "voucher_header",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "voucher_header",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "user_id",
                table: "voucher_header",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "voucher_detail",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "user_id",
                table: "gl_mappings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "subaccount",
                table: "chart_of_accounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "user_id",
                table: "chart_of_accounts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    password_hash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "payment_header",
                columns: table => new
                {
                    payment_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    payment_number = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    payment_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    payee_name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    payment_method = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    account_id = table.Column<int>(type: "int", nullable: false),
                    total_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    reference_number = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_header", x => x.payment_id);
                    table.ForeignKey(
                        name: "FK_payment_header_chart_of_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "chart_of_accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_header_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receipts1",
                columns: table => new
                {
                    receipt_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    receipt_number = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    payer_name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    payment_method = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipts1", x => x.receipt_id);
                    table.ForeignKey(
                        name: "FK_receipts1_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_sessions",
                columns: table => new
                {
                    session_id = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    expires_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_sessions", x => x.session_id);
                    table.ForeignKey(
                        name: "FK_user_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payment_attachments",
                columns: table => new
                {
                    attachment_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    payment_id = table.Column<int>(type: "int", nullable: false),
                    file_name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    file_path = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    file_type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    upload_date = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_attachments", x => x.attachment_id);
                    table.ForeignKey(
                        name: "FK_payment_attachments_payment_header_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payment_header",
                        principalColumn: "payment_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payment_detail",
                columns: table => new
                {
                    detail_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    payment_id = table.Column<int>(type: "int", nullable: false),
                    voucher_id = table.Column<int>(type: "int", nullable: false),
                    amount_paid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_detail", x => x.detail_id);
                    table.ForeignKey(
                        name: "FK_payment_detail_payment_header_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payment_header",
                        principalColumn: "payment_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_payment_detail_voucher_header_voucher_id",
                        column: x => x.voucher_id,
                        principalTable: "voucher_header",
                        principalColumn: "voucher_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_voucher_header_user_id",
                table: "voucher_header",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_gl_mappings_user_id",
                table: "gl_mappings",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_chart_of_accounts_user_id",
                table: "chart_of_accounts",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_attachments_payment_id",
                table: "payment_attachments",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_detail_payment_id",
                table: "payment_detail",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_detail_voucher_id",
                table: "payment_detail",
                column: "voucher_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_header_account_id",
                table: "payment_header",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_header_user_id",
                table: "payment_header",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_receipts1_user_id",
                table: "receipts1",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_user_id",
                table: "user_sessions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_username",
                table: "users",
                column: "username",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_chart_of_accounts_users_user_id",
                table: "chart_of_accounts",
                column: "user_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_gl_mappings_users_user_id",
                table: "gl_mappings",
                column: "user_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_voucher_header_users_user_id",
                table: "voucher_header",
                column: "user_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_chart_of_accounts_users_user_id",
                table: "chart_of_accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_gl_mappings_users_user_id",
                table: "gl_mappings");

            migrationBuilder.DropForeignKey(
                name: "FK_voucher_header_users_user_id",
                table: "voucher_header");

            migrationBuilder.DropTable(
                name: "payment_attachments");

            migrationBuilder.DropTable(
                name: "payment_detail");

            migrationBuilder.DropTable(
                name: "receipts1");

            migrationBuilder.DropTable(
                name: "user_sessions");

            migrationBuilder.DropTable(
                name: "payment_header");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropIndex(
                name: "IX_voucher_header_user_id",
                table: "voucher_header");

            migrationBuilder.DropIndex(
                name: "IX_gl_mappings_user_id",
                table: "gl_mappings");

            migrationBuilder.DropIndex(
                name: "IX_chart_of_accounts_user_id",
                table: "chart_of_accounts");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "voucher_header");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "gl_mappings");

            migrationBuilder.DropColumn(
                name: "subaccount",
                table: "chart_of_accounts");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "chart_of_accounts");

            migrationBuilder.AlterColumn<string>(
                name: "voucher_number",
                table: "voucher_header",
                type: "nvarchar(50)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "voucher_date",
                table: "voucher_header",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "transaction_type",
                table: "voucher_header",
                type: "nvarchar(50)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "voucher_header",
                type: "nvarchar(20)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "voucher_header",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "voucher_detail",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }
    }
}
