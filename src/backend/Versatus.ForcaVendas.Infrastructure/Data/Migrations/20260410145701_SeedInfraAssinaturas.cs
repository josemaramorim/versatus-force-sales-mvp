using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Versatus.ForcaVendas.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedInfraAssinaturas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "infra",
                table: "assinaturas",
                columns: new[] { "tenant_id", "nome_empresa", "ativo", "max_usuarios_simultaneos" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), "Demo Tenant 1", true, 4 },
                    { new Guid("00000000-0000-0000-0000-000000000002"), "Demo Tenant 2", true, 4 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "infra",
                table: "assinaturas",
                keyColumn: "tenant_id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                schema: "infra",
                table: "assinaturas",
                keyColumn: "tenant_id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"));
        }
    }
}
