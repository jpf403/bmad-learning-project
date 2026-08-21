using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarbershopApi.Migrations
{
    /// <inheritdoc />
    public partial class AddSsoFieldsToAccount_RestoreRowVersionTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AddSsoFieldsToAccount's PasswordHash-nullable change forced a SQLite table rebuild
            // that silently dropped trg_Accounts_RowVersion (no EF metadata, added by hand-written
            // SQL in AddAccountEntity) -- restore it here, in a migration of its own, since EF
            // defers that rebuild to the very end of a migration regardless of statement order,
            // so recreating the trigger inside AddSsoFieldsToAccount itself doesn't survive it.
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_Accounts_RowVersion;");
            migrationBuilder.Sql(
                "CREATE TRIGGER trg_Accounts_RowVersion AFTER UPDATE ON Accounts " +
                "BEGIN UPDATE Accounts SET RowVersion = RowVersion + 1 WHERE rowid = NEW.rowid; END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_Accounts_RowVersion;");
        }
    }
}
