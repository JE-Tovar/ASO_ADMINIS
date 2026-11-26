using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASO_ADMINIS.Desktop.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCuentasPorCobrar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CondicionPago",
                table: "Ventas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaVencimientoCredito",
                table: "Ventas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CobrosCliente",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CuentaPorCobrarId = table.Column<int>(type: "int", nullable: false),
                    VentaId = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Metodo = table.Column<int>(type: "int", nullable: false),
                    MontoBs = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MontoDivisa = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TasaCambio = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Referencia = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    CuentaBancariaId = table.Column<int>(type: "int", nullable: true),
                    CuentaBancariaNombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CajaId = table.Column<int>(type: "int", nullable: true),
                    TurnoId = table.Column<int>(type: "int", nullable: true),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    MotivoAnulacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaAnulacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AnuladoPorId = table.Column<int>(type: "int", nullable: true),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CobrosCliente", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CuentasPorCobrar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false),
                    VentaId = table.Column<int>(type: "int", nullable: false),
                    ClienteId = table.Column<int>(type: "int", nullable: false),
                    NumeroFactura = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ClienteRif = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    ClienteNombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FechaEmision = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaVencimiento = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalFacturaBs = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PagoInicialBs = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SaldoBs = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TasaCambioEmision = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    FechaCobro = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CuentasPorCobrar", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosCuentaPorCobrar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false),
                    CuentaPorCobrarId = table.Column<int>(type: "int", nullable: false),
                    CobroClienteId = table.Column<int>(type: "int", nullable: true),
                    DocumentoVentaId = table.Column<int>(type: "int", nullable: true),
                    DocumentoOrigen = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    MontoBs = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosCuentaPorCobrar", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CobrosCliente_CuentaPorCobrarId",
                table: "CobrosCliente",
                column: "CuentaPorCobrarId");

            migrationBuilder.CreateIndex(
                name: "IX_CobrosCliente_OrganizacionId_Numero",
                table: "CobrosCliente",
                columns: new[] { "OrganizacionId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CobrosCliente_TurnoId",
                table: "CobrosCliente",
                column: "TurnoId");

            migrationBuilder.CreateIndex(
                name: "IX_CuentasPorCobrar_OrganizacionId_FechaVencimiento",
                table: "CuentasPorCobrar",
                columns: new[] { "OrganizacionId", "FechaVencimiento" });

            migrationBuilder.CreateIndex(
                name: "IX_CuentasPorCobrar_OrganizacionId_VentaId",
                table: "CuentasPorCobrar",
                columns: new[] { "OrganizacionId", "VentaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCuentaPorCobrar_CuentaPorCobrarId_Fecha",
                table: "MovimientosCuentaPorCobrar",
                columns: new[] { "CuentaPorCobrarId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCuentaPorCobrar_DocumentoVentaId",
                table: "MovimientosCuentaPorCobrar",
                column: "DocumentoVentaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CobrosCliente");

            migrationBuilder.DropTable(
                name: "CuentasPorCobrar");

            migrationBuilder.DropTable(
                name: "MovimientosCuentaPorCobrar");

            migrationBuilder.DropColumn(
                name: "CondicionPago",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "FechaVencimientoCredito",
                table: "Ventas");
        }
    }
}
