using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASO_ADMINIS.Desktop.Migrations
{
    /// <inheritdoc />
    public partial class SeguridadYRespaldos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BloqueadoHasta",
                table: "Usuarios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DebeCambiarPassword",
                table: "Usuarios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "IntentosFallidos",
                table: "Usuarios",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinutosInactividad",
                table: "Organizaciones",
                type: "int",
                nullable: false,
                // Editado a mano: las organizaciones existentes arrancan con el bloqueo por
                // inactividad del valor por defecto (PoliticaSeguridad), no apagado.
                defaultValue: 15);

            migrationBuilder.CreateTable(
                name: "Respaldos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Origen = table.Column<int>(type: "int", nullable: false),
                    Exitoso = table.Column<bool>(type: "bit", nullable: false),
                    Verificado = table.Column<bool>(type: "bit", nullable: false),
                    Archivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TamanoBytes = table.Column<long>(type: "bigint", nullable: false),
                    DuracionMs = table.Column<int>(type: "int", nullable: false),
                    Equipo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UsuarioNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Respaldos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Respaldos_Fecha",
                table: "Respaldos",
                column: "Fecha");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Respaldos");

            migrationBuilder.DropColumn(
                name: "BloqueadoHasta",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "DebeCambiarPassword",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "IntentosFallidos",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "MinutosInactividad",
                table: "Organizaciones");
        }
    }
}
