using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASO_ADMINIS.Desktop.Migrations
{
    /// <inheritdoc />
    public partial class AgregarGastosVarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GastosVarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Concepto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Beneficiario = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Referencia = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Moneda = table.Column<int>(type: "int", nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TasaCambio = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    MontoBs = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CuentaId = table.Column<int>(type: "int", nullable: true),
                    CuentaNombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    MotivoAnulacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaAnulacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    CreadoPorNombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GastosVarios", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GastosVarios_OrganizacionId_Fecha",
                table: "GastosVarios",
                columns: new[] { "OrganizacionId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_GastosVarios_OrganizacionId_Numero",
                table: "GastosVarios",
                columns: new[] { "OrganizacionId", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GastosVarios");
        }
    }
}
