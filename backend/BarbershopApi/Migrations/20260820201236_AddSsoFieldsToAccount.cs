using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarbershopApi.Migrations
{
    /// <inheritdoc />
    public partial class AddSsoFieldsToAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AlterColumn below relaxes PasswordHash to nullable, which SQLite can only do via a
            // full table-rebuild (create new Accounts table, copy rows, drop old table, rename).
            // EF's SQLite generator defers that physical rebuild until the very end of this
            // migration -- after every other operation below, regardless of source order -- so a
            // trigger recreated here would just be wiped out again when the old table is dropped.
            // trg_Accounts_RowVersion has no EF metadata (added by hand-written SQL in
            // AddAccountEntity), so it doesn't survive the rebuild on its own; it's restored in
            // the immediately-following AddSsoFieldsToAccount_RestoreRowVersionTrigger migration
            // instead, per EF's own guidance for operations that can't run mid-rebuild (AD-16).
            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "Accounts",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<string>(
                name: "SsoProvider",
                table: "Accounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SsoSubjectId",
                table: "Accounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_SsoProvider_SsoSubjectId",
                table: "Accounts",
                columns: new[] { "SsoProvider", "SsoSubjectId" },
                unique: true,
                filter: "SsoProvider IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Accounts_SsoProvider_SsoSubjectId",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "SsoProvider",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "SsoSubjectId",
                table: "Accounts");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "Accounts",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
