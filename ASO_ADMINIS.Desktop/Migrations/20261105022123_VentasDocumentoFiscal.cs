using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASO_ADMINIS.Desktop.Migrations
{
    /// <inheritdoc />
    public partial class VentasDocumentoFiscal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Editada a mano. El scaffold tomó seis columnas nuevas por renombres de las viejas
            // (ArticuloId -> Tipo, Cantidad -> Total...). Aquí se agregan como nuevas, se pasan los
            // datos de cada venta vieja a un renglón y un pago, y recién al final se borran las
            // columnas viejas.

            migrationBuilder.AddColumn<int>(
                name: "Tipo", table: "Ventas", type: "int", nullable: false, defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Total", table: "Ventas", type: "decimal(18,2)", nullable: false, defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Vuelto", table: "Ventas", type: "decimal(18,2)", nullable: false, defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "NumeroZ", table: "Ventas", type: "int", nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SerialEmisor", table: "Ventas", type: "nvarchar(40)", maxLength: 40, nullable: false, defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CajaCodigo", table: "Ventas", type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "ClienteNombre",
                table: "Ventas",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseAdicional",
                table: "Ventas",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseGeneral",
                table: "Ventas",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseIgtf",
                table: "Ventas",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseReducida",
                table: "Ventas",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ClienteDireccion",
                table: "Ventas",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ClienteId",
                table: "Ventas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClienteRif",
                table: "Ventas",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Concepto",
                table: "Ventas",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DocumentoAfectadoFecha",
                table: "Ventas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DocumentoAfectadoId",
                table: "Ventas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentoAfectadoNumero",
                table: "Ventas",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "EnContingencia",
                table: "Ventas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EsHistorico",
                table: "Ventas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaTransmision",
                table: "Ventas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Igtf",
                table: "Ventas",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "IvaAdicional",
                table: "Ventas",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "IvaGeneral",
                table: "Ventas",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "IvaReducida",
                table: "Ventas",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MontoExento",
                table: "Ventas",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "NumeroControl",
                table: "Ventas",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NumeroFiscal",
                table: "Ventas",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "PorcentajeAdicional",
                table: "Ventas",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PorcentajeGeneral",
                table: "Ventas",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PorcentajeIgtf",
                table: "Ventas",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PorcentajeReducida",
                table: "Ventas",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "VentaId",
                table: "EntradasInventario",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VentaLineas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArticuloId = table.Column<int>(type: "int", nullable: true),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PresentacionNombre = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Factor = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PrecioUnitarioUsd = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Alicuota = table.Column<int>(type: "int", nullable: false),
                    PorcentajeIva = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Iva = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VentaId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VentaLineas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VentaLineas_Ventas_VentaId",
                        column: x => x.VentaId,
                        principalTable: "Ventas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VentaPagos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Metodo = table.Column<int>(type: "int", nullable: false),
                    MontoBs = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MontoDivisa = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Referencia = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    VentaId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VentaPagos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VentaPagos_Ventas_VentaId",
                        column: x => x.VentaId,
                        principalTable: "Ventas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_DocumentoAfectadoId",
                table: "Ventas",
                column: "DocumentoAfectadoId");

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_OrganizacionId_Fecha",
                table: "Ventas",
                columns: new[] { "OrganizacionId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_EntradasInventario_VentaId",
                table: "EntradasInventario",
                column: "VentaId");

            migrationBuilder.CreateIndex(
                name: "IX_VentaLineas_VentaId",
                table: "VentaLineas",
                column: "VentaId");

            migrationBuilder.CreateIndex(
                name: "IX_VentaPagos_VentaId",
                table: "VentaPagos",
                column: "VentaId");

            // ---- Datos: cada venta vieja (una línea, sin IVA ni emisor) pasa a ser un documento
            // histórico de un renglón y, si tenía método, un pago. No entra al libro de ventas.
            // El monto en Bs se reconstruye como lo mostraba la pantalla: cantidad × precio US$ × tasa.
            migrationBuilder.Sql(@"
INSERT INTO VentaLineas (VentaId, ArticuloId, Codigo, Descripcion, PresentacionNombre, Factor, Cantidad,
                         PrecioUnitarioUsd, PrecioUnitario, Alicuota, PorcentajeIva, Monto, Iva)
SELECT Id, ArticuloId, ArticuloCodigo, ArticuloNombre, PresentacionNombre, Factor, Cantidad,
       PrecioUnitarioUsd, ROUND(PrecioUnitarioUsd * TasaCambioUsada, 2), 0, 0,
       ROUND(Cantidad * ROUND(PrecioUnitarioUsd * TasaCambioUsada, 2), 2), 0
FROM Ventas;");

            // MetodoPago 1 = EfectivoDivisa, 6 = Zelle: en divisa, se guarda también el monto en US$.
            migrationBuilder.Sql(@"
INSERT INTO VentaPagos (VentaId, Metodo, MontoBs, MontoDivisa, Referencia)
SELECT Id, MetodoPago, ROUND(Cantidad * ROUND(PrecioUnitarioUsd * TasaCambioUsada, 2), 2),
       CASE WHEN MetodoPago IN (1, 6) THEN ROUND(Cantidad * PrecioUnitarioUsd, 2) ELSE 0 END, ''
FROM Ventas
WHERE MetodoPago IS NOT NULL;");

            migrationBuilder.Sql(@"
UPDATE Ventas
SET EsHistorico = 1,
    Tipo = 0,
    MontoExento = ROUND(Cantidad * ROUND(PrecioUnitarioUsd * TasaCambioUsada, 2), 2),
    Total = ROUND(Cantidad * ROUND(PrecioUnitarioUsd * TasaCambioUsada, 2), 2);");

            foreach (var vieja in new[]
                     {
                         "ArticuloId", "ArticuloCodigo", "ArticuloNombre", "Cantidad",
                         "PresentacionNombre", "Factor", "PrecioUnitarioUsd", "MetodoPago"
                     })
                migrationBuilder.DropColumn(name: vieja, table: "Ventas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "VentasDocumentoFiscal no es reversible: un documento con varios renglones y pagos no cabe " +
                "en el modelo anterior de una sola línea. Restaure un respaldo de la base si hace falta volver.");
        }
    }
}
