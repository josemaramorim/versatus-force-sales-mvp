using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Versatus.ForcaVendas.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EventType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "usuarios",
                keyColumn: "id",
                keyValue: new Guid("7c90e66f-0af5-4ded-90e2-0df0a0b2d001"),
                columns: new[] { "criado_em", "password_hash" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 4, 12, 16, 7, 19, 627, DateTimeKind.Unspecified).AddTicks(5015), new TimeSpan(0, 0, 0, 0, 0)), "$2a$11$ciSTyOS/P1XmbUlSxGlhaeKggvqND4mOTYTakRxHtm52LS/kJclmO" });

            migrationBuilder.UpdateData(
                table: "usuarios",
                keyColumn: "id",
                keyValue: new Guid("7c90e66f-0af5-4ded-90e2-0df0a0b2d002"),
                columns: new[] { "criado_em", "password_hash" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 4, 12, 16, 7, 19, 627, DateTimeKind.Unspecified).AddTicks(5025), new TimeSpan(0, 0, 0, 0, 0)), "$2a$11$ciSTyOS/P1XmbUlSxGlhaeKggvqND4mOTYTakRxHtm52LS/kJclmO" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.UpdateData(
                table: "usuarios",
                keyColumn: "id",
                keyValue: new Guid("7c90e66f-0af5-4ded-90e2-0df0a0b2d001"),
                columns: new[] { "criado_em", "password_hash" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 4, 10, 19, 40, 52, 720, DateTimeKind.Unspecified).AddTicks(5624), new TimeSpan(0, 0, 0, 0, 0)), "$2a$11$NnPJnS3XBqkz9EnLYkUmMOKMrjXHb7jPcG8eVxZ8TjiDGVGcRtr7." });

            migrationBuilder.UpdateData(
                table: "usuarios",
                keyColumn: "id",
                keyValue: new Guid("7c90e66f-0af5-4ded-90e2-0df0a0b2d002"),
                columns: new[] { "criado_em", "password_hash" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 4, 10, 19, 40, 52, 720, DateTimeKind.Unspecified).AddTicks(5631), new TimeSpan(0, 0, 0, 0, 0)), "$2a$11$NnPJnS3XBqkz9EnLYkUmMOKMrjXHb7jPcG8eVxZ8TjiDGVGcRtr7." });
        }
    }
}
