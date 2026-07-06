using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Versatus.ForcaVendas.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPreClienteFieldsToPedido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsNovoCliente",
                table: "pedidos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NomePreCliente",
                table: "pedidos",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreClienteJson",
                table: "pedidos",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsNovoCliente",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "NomePreCliente",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "PreClienteJson",
                table: "pedidos");
        }
    }
}
