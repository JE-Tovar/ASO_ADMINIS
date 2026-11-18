using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASO_ADMINIS.Desktop.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCajasYTurnos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CajaId",
                table: "Ventas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TurnoId",
                table: "Ventas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CajaId",
                table: "Usuarios",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Cajas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cajas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Turnos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false),
                    CajaId = table.Column<int>(type: "int", nullable: false),
                    CajaCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    UsuarioNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    Apertura = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FondoBs = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FondoDivisa = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Cierre = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EsperadoBs = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EsperadoDivisa = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ContadoBs = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ContadoDivisa = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CerradoPorId = table.Column<int>(type: "int", nullable: true),
                    CerradoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Turnos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_CajaId",
                table: "Ventas",
                column: "CajaId");

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_TurnoId",
                table: "Ventas",
                column: "TurnoId");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_CajaId",
                table: "Usuarios",
                column: "CajaId");

            migrationBuilder.CreateIndex(
                name: "IX_Cajas_OrganizacionId_Codigo",
                table: "Cajas",
                columns: new[] { "OrganizacionId", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_CajaId_Abierto",
                table: "Turnos",
                column: "CajaId",
                unique: true,
                filter: "[Estado] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_OrganizacionId_Apertura",
                table: "Turnos",
                columns: new[] { "OrganizacionId", "Apertura" });

            // ---- Editado a mano: datos existentes ----
            // Hasta ahora la caja era un texto libre escrito en cada PC. Se da de alta una Caja por
            // cada código distinto que aparezca en ventas o cierres Z, por organización, y las
            // ventas quedan apuntando a ella. Los turnos empiezan vacíos: lo histórico no tiene.
            migrationBuilder.Sql(@"
INSERT INTO Cajas (OrganizacionId, Codigo, Nombre, Activa)
SELECT DISTINCT x.OrganizacionId, UPPER(LTRIM(RTRIM(x.CajaCodigo))), N'', CAST(1 AS bit)
FROM (
    SELECT OrganizacionId, CajaCodigo FROM Ventas
    UNION
    SELECT OrganizacionId, CajaCodigo FROM CierresZ
) AS x
WHERE x.CajaCodigo IS NOT NULL AND LTRIM(RTRIM(x.CajaCodigo)) <> N'';");

            migrationBuilder.Sql(@"
UPDATE v SET v.CajaId = c.Id
FROM Ventas AS v
INNER JOIN Cajas AS c
    ON c.OrganizacionId = v.OrganizacionId
   AND c.Codigo = UPPER(LTRIM(RTRIM(v.CajaCodigo)));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Cajas");

            migrationBuilder.DropTable(
                name: "Turnos");

            migrationBuilder.DropIndex(
                name: "IX_Ventas_CajaId",
                table: "Ventas");

            migrationBuilder.DropIndex(
                name: "IX_Ventas_TurnoId",
                table: "Ventas");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_CajaId",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "CajaId",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "TurnoId",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "CajaId",
                table: "Usuarios");
        }
    }
}
