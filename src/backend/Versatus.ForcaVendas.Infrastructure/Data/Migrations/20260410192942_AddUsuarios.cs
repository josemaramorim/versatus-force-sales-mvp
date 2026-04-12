using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Versatus.ForcaVendas.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUsuarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "usuarios",
                columns: new[] { "id", "ativo", "criado_em", "password_hash", "role", "tenant_id", "username" },
                values: new object[,]
                {
                    { new Guid("7c90e66f-0af5-4ded-90e2-0df0a0b2d001"), true, new DateTimeOffset(new DateTime(2026, 4, 10, 19, 29, 41, 670, DateTimeKind.Unspecified).AddTicks(996), new TimeSpan(0, 0, 0, 0, 0)), "$2a$11$TRj0gb782W4JHVcO9d88xeQc5u.EsleRRJRDE78rCgpxaltpeHq1e", "admin", new Guid("00000000-0000-0000-0000-000000000001"), "admin" },
                    { new Guid("7c90e66f-0af5-4ded-90e2-0df0a0b2d002"), true, new DateTimeOffset(new DateTime(2026, 4, 10, 19, 29, 41, 670, DateTimeKind.Unspecified).AddTicks(1003), new TimeSpan(0, 0, 0, 0, 0)), "$2a$11$TRj0gb782W4JHVcO9d88xeQc5u.EsleRRJRDE78rCgpxaltpeHq1e", "gestor", new Guid("00000000-0000-0000-0000-000000000002"), "gestor" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_tenant_id_username",
                table: "usuarios",
                columns: new[] { "tenant_id", "username" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "usuarios");
        }
    }
}
