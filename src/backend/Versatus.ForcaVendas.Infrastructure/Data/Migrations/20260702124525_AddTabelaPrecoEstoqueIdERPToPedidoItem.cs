using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Versatus.ForcaVendas.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTabelaPrecoEstoqueIdERPToPedidoItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TabelaPrecoEstoqueIdERP",
                table: "pedido_itens",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "usuarios",
                keyColumn: "id",
                keyValue: new Guid("7c90e66f-0af5-4ded-90e2-0df0a0b2d001"),
                column: "password_hash",
                value: "$2a$11$yCty/Ja8ozIT5XncSFjkW..pXSdhG0Q//CfLXhmoYL6Ok4NmF1LhC");

            migrationBuilder.UpdateData(
                table: "usuarios",
                keyColumn: "id",
                keyValue: new Guid("7c90e66f-0af5-4ded-90e2-0df0a0b2d002"),
                column: "password_hash",
                value: "$2a$11$yCty/Ja8ozIT5XncSFjkW..pXSdhG0Q//CfLXhmoYL6Ok4NmF1LhC");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TabelaPrecoEstoqueIdERP",
                table: "pedido_itens");

            migrationBuilder.UpdateData(
                table: "usuarios",
                keyColumn: "id",
                keyValue: new Guid("7c90e66f-0af5-4ded-90e2-0df0a0b2d001"),
                column: "password_hash",
                value: "$2a$11$ciSTyOS/P1XmbUlSxGlhaeKggvqND4mOTYTakRxHtm52LS/kJclmO");

            migrationBuilder.UpdateData(
                table: "usuarios",
                keyColumn: "id",
                keyValue: new Guid("7c90e66f-0af5-4ded-90e2-0df0a0b2d002"),
                column: "password_hash",
                value: "$2a$11$ciSTyOS/P1XmbUlSxGlhaeKggvqND4mOTYTakRxHtm52LS/kJclmO");
        }
    }
}
