using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASO_ADMINIS.Desktop.Migrations
{
    /// <inheritdoc />
    public partial class ContingenciaYDocumentoFiscal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentoRuta",
                table: "Ventas",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "RangosContingencia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false),
                    Prefijo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Desde = table.Column<int>(type: "int", nullable: false),
                    Hasta = table.Column<int>(type: "int", nullable: false),
                    Digitos = table.Column<int>(type: "int", nullable: false),
                    Imprenta = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ImprentaRif = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    Autorizacion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FechaAutorizacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RangosContingencia", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RangosContingencia_OrganizacionId_Prefijo_Desde",
                table: "RangosContingencia",
                columns: new[] { "OrganizacionId", "Prefijo", "Desde" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RangosContingencia");

            migrationBuilder.DropColumn(
                name: "DocumentoRuta",
                table: "Ventas");
        }
    }
}
