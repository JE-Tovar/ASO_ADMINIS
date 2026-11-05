using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASO_ADMINIS.Desktop.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCierreZRetenciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CierresZ",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false),
                    NumeroZ = table.Column<int>(type: "int", nullable: false),
                    CajaCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SerialEmisor = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Documentos = table.Column<int>(type: "int", nullable: false),
                    Exento = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BaseGeneral = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IvaGeneral = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BaseReducida = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IvaReducida = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BaseAdicional = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IvaAdicional = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Igtf = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DocumentosSistema = table.Column<int>(type: "int", nullable: false),
                    TotalSistema = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    CreadoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CierresZ", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RetencionesIvaRecibidas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false),
                    VentaId = table.Column<int>(type: "int", nullable: false),
                    VentaNumeroFiscal = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ClienteRif = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    ClienteNombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NumeroComprobante = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FechaComprobante = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Porcentaje = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    IvaFactura = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MontoRetenido = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetencionesIvaRecibidas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CierresZ_OrganizacionId_CajaCodigo_NumeroZ",
                table: "CierresZ",
                columns: new[] { "OrganizacionId", "CajaCodigo", "NumeroZ" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetencionesIvaRecibidas_OrganizacionId_ClienteRif_NumeroComprobante",
                table: "RetencionesIvaRecibidas",
                columns: new[] { "OrganizacionId", "ClienteRif", "NumeroComprobante" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetencionesIvaRecibidas_OrganizacionId_VentaId",
                table: "RetencionesIvaRecibidas",
                columns: new[] { "OrganizacionId", "VentaId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CierresZ");

            migrationBuilder.DropTable(
                name: "RetencionesIvaRecibidas");
        }
    }
}
