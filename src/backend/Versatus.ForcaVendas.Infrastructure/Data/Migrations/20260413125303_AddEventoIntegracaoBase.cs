using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Versatus.ForcaVendas.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventoIntegracaoBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "usuarios",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "eventos_integracao_pedidos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PedidoId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceEventId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Tipo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: true),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Sucesso = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eventos_integracao_pedidos", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "usuarios",
                keyColumn: "id",
                keyValue: new Guid("7c90e66f-0af5-4ded-90e2-0df0a0b2d001"),
                columns: new[] { "criado_em", "email" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 4, 12, 16, 7, 19, 627, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "admin@demo1.versatus.com" });

            migrationBuilder.UpdateData(
                table: "usuarios",
                keyColumn: "id",
                keyValue: new Guid("7c90e66f-0af5-4ded-90e2-0df0a0b2d002"),
                columns: new[] { "criado_em", "email" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 4, 12, 16, 7, 19, 627, DateTimeKind.Unspecified).AddTicks(10), new TimeSpan(0, 0, 0, 0, 0)), "gestor@demo2.versatus.com" });

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_email_global",
                table: "usuarios",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_eventos_integracao_idempotencia",
                table: "eventos_integracao_pedidos",
                columns: new[] { "TenantId", "PedidoId", "SourceEventId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "eventos_integracao_pedidos");

            migrationBuilder.DropIndex(
                name: "ix_usuarios_email_global",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "email",
                table: "usuarios");

            migrationBuilder.UpdateData(
                table: "usuarios",
                keyColumn: "id",
                keyValue: new Guid("7c90e66f-0af5-4ded-90e2-0df0a0b2d001"),
                column: "criado_em",
                value: new DateTimeOffset(new DateTime(2026, 4, 12, 16, 7, 19, 627, DateTimeKind.Unspecified).AddTicks(5015), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "usuarios",
                keyColumn: "id",
                keyValue: new Guid("7c90e66f-0af5-4ded-90e2-0df0a0b2d002"),
                column: "criado_em",
                value: new DateTimeOffset(new DateTime(2026, 4, 12, 16, 7, 19, 627, DateTimeKind.Unspecified).AddTicks(5025), new TimeSpan(0, 0, 0, 0, 0)));
        }
    }
}
