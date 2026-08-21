using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarbershopApi.Migrations
{
    /// <inheritdoc />
    public partial class RestoreRowVersionTriggerOnFullRollback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No schema change at this point in history -- trg_Accounts_RowVersion already
            // exists from AddAccountEntity's Up(). This migration exists solely so its Down()
            // has a slot to run *after* AddSsoFieldsToAccount.Down()'s table rebuild completes
            // (see that migration's Down() and AddSsoFieldsToAccount_RestoreRowVersionTrigger's
            // Down() for why a trigger-recreate placed inside either of those Down() methods
            // gets silently wiped by their own deferred rebuild -- same EF SQLite behavior that
            // forced the Up()-side two-migration split, confirmed empirically for Down() during
            // Story 4.1's code review). Re-running the idempotent CREATE is harmless even if the
            // trigger already exists.
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_Accounts_RowVersion;");
            migrationBuilder.Sql(
                "CREATE TRIGGER trg_Accounts_RowVersion AFTER UPDATE ON Accounts " +
                "BEGIN UPDATE Accounts SET RowVersion = RowVersion + 1 WHERE rowid = NEW.rowid; END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Runs after AddSsoFieldsToAccount.Down()'s table rebuild has completed when rolling
            // back a full SSO rollback (both SSO migrations' Down() run before this one, since
            // they're chronologically newer) -- this is the only slot where recreating the
            // trigger can actually survive. Restores it to the state AddAccountEntity's own
            // migration originally established.
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_Accounts_RowVersion;");
            migrationBuilder.Sql(
                "CREATE TRIGGER trg_Accounts_RowVersion AFTER UPDATE ON Accounts " +
                "BEGIN UPDATE Accounts SET RowVersion = RowVersion + 1 WHERE rowid = NEW.rowid; END;");
        }
    }
}
