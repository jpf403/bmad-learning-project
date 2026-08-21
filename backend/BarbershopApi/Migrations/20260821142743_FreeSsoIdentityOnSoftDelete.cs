using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarbershopApi.Migrations
{
    /// <inheritdoc />
    public partial class FreeSsoIdentityOnSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Accounts_SsoProvider_SsoSubjectId",
                table: "Accounts");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_SsoProvider_SsoSubjectId",
                table: "Accounts",
                columns: new[] { "SsoProvider", "SsoSubjectId" },
                unique: true,
                filter: "SsoProvider IS NOT NULL AND DeletedAt IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Accounts_SsoProvider_SsoSubjectId",
                table: "Accounts");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_SsoProvider_SsoSubjectId",
                table: "Accounts",
                columns: new[] { "SsoProvider", "SsoSubjectId" },
                unique: true,
                filter: "SsoProvider IS NOT NULL");
        }
    }
}
