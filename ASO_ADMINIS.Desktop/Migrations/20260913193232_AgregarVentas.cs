using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASO_ADMINIS.Desktop.Migrations
{
    /// <inheritdoc />
    public partial class AgregarVentas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VentaId",
                table: "SalidasInventario",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Ventas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModeloId = table.Column<int>(type: "int", nullable: false),
                    ModeloNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    MarcaNombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PrecioUnitarioUsd = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TasaCambioUsada = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ClienteNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    MotivoAnulacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaAnulacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ventas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalidasInventario_VentaId",
                table: "SalidasInventario",
                column: "VentaId");

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_OrganizacionId_Numero",
                table: "Ventas",
                columns: new[] { "OrganizacionId", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ventas");

            migrationBuilder.DropIndex(
                name: "IX_SalidasInventario_VentaId",
                table: "SalidasInventario");

            migrationBuilder.DropColumn(
                name: "VentaId",
                table: "SalidasInventario");
        }
    }
}
