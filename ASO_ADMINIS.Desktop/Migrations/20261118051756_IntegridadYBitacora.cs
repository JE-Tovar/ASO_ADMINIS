using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASO_ADMINIS.Desktop.Migrations
{
    /// <inheritdoc />
    public partial class IntegridadYBitacora : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Usuarios",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Turnos",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "TasasCambio",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "SalidasInventario",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Proveedores",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "PeticionesCambio",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Organizaciones",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "MovimientosBanco",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "GastosVarios",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "FacturasProveedor",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "EntradasInventario",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "CuentasBancarias",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Clientes",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Cajas",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Articulos",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RegistrosAuditoria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: true),
                    UsuarioNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CajaCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Equipo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Entidad = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    EntidadId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Accion = table.Column<int>(type: "int", nullable: false),
                    Resumen = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Detalle = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosAuditoria", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosAuditoria_Entidad_EntidadId",
                table: "RegistrosAuditoria",
                columns: new[] { "Entidad", "EntidadId" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosAuditoria_OrganizacionId_Fecha",
                table: "RegistrosAuditoria",
                columns: new[] { "OrganizacionId", "Fecha" });

            // ---- Editado a mano ----

            // La bitácora solo se agrega: ni la aplicación ni nadie por fuera (SSMS, un script)
            // puede modificar o borrar una fila. El contexto ya lo rechaza; esto lo hace cumplir
            // la propia base.
            migrationBuilder.Sql(@"
CREATE TRIGGER TR_RegistrosAuditoria_SoloAgregar ON RegistrosAuditoria
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51000, N'La bitácora de auditoría no se puede modificar ni borrar.', 1;
END;");

            // Antes de las claves foráneas: una referencia a una caja o un turno que ya no existe
            // haría fallar la migración. La migración de cajas dejó todo consistente, así que esto
            // normalmente no toca nada; queda por si se editó la base a mano entre medio.
            migrationBuilder.Sql(@"
UPDATE Usuarios SET CajaId = NULL WHERE CajaId IS NOT NULL AND CajaId NOT IN (SELECT Id FROM Cajas);
UPDATE Ventas SET CajaId = NULL WHERE CajaId IS NOT NULL AND CajaId NOT IN (SELECT Id FROM Cajas);
UPDATE Ventas SET TurnoId = NULL WHERE TurnoId IS NOT NULL AND TurnoId NOT IN (SELECT Id FROM Turnos);");

            migrationBuilder.AddForeignKey(
                name: "FK_Turnos_Cajas_CajaId",
                table: "Turnos",
                column: "CajaId",
                principalTable: "Cajas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Usuarios_Cajas_CajaId",
                table: "Usuarios",
                column: "CajaId",
                principalTable: "Cajas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Ventas_Cajas_CajaId",
                table: "Ventas",
                column: "CajaId",
                principalTable: "Cajas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Ventas_Turnos_TurnoId",
                table: "Ventas",
                column: "TurnoId",
                principalTable: "Turnos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Turnos_Cajas_CajaId",
                table: "Turnos");

            migrationBuilder.DropForeignKey(
                name: "FK_Usuarios_Cajas_CajaId",
                table: "Usuarios");

            migrationBuilder.DropForeignKey(
                name: "FK_Ventas_Cajas_CajaId",
                table: "Ventas");

            migrationBuilder.DropForeignKey(
                name: "FK_Ventas_Turnos_TurnoId",
                table: "Ventas");

            migrationBuilder.DropTable(
                name: "RegistrosAuditoria");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Turnos");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "TasasCambio");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "SalidasInventario");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Proveedores");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "PeticionesCambio");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Organizaciones");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "MovimientosBanco");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "GastosVarios");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "FacturasProveedor");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "EntradasInventario");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "CuentasBancarias");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Cajas");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Articulos");
        }
    }
}
