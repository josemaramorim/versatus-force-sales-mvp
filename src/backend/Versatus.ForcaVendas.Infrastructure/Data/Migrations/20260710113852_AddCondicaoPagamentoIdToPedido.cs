using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Versatus.ForcaVendas.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCondicaoPagamentoIdToPedido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CondicaoPagamentoId",
                table: "pedidos",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "cond-1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CondicaoPagamentoId",
                table: "pedidos");
        }
    }
}
