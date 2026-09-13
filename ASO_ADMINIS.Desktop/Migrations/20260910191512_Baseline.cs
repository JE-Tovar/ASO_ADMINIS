using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASO_ADMINIS.Desktop.Migrations
{
    /// <inheritdoc />
    public partial class Baseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Articulos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Unidad = table.Column<int>(type: "int", nullable: false),
                    Minimo = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Ubicacion = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Articulos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CuentasBancarias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Banco = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NumeroCuenta = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Moneda = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SaldoInicial = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FechaApertura = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CuentasBancarias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EntradasInventario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    ProveedorId = table.Column<int>(type: "int", nullable: true),
                    ProveedorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NumeroDocumento = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FechaVencimiento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompradoPor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    FacturaProveedorId = table.Column<int>(type: "int", nullable: true),
                    FacturaProveedorNumero = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MotivoAnulacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaAnulacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    CreadoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntradasInventario", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FacturasProveedor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false),
                    NumeroDocumento = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProveedorId = table.Column<int>(type: "int", nullable: false),
                    ProveedorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FechaEmision = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaVencimiento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    FechaPago = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoAnulacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaAnulacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacturasProveedor", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosBanco",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false),
                    CuentaId = table.Column<int>(type: "int", nullable: false),
                    CuentaNombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Concepto = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Referencia = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    Origen = table.Column<int>(type: "int", nullable: false),
                    OrigenId = table.Column<int>(type: "int", nullable: true),
                    ContraparteId = table.Column<int>(type: "int", nullable: true),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    FechaConciliacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConciliadoPorId = table.Column<int>(type: "int", nullable: true),
                    MotivoAnulacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaAnulacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosBanco", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Organizaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizaciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PermisosUsuario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    UsuarioNombre = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Permiso = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Concedido = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermisosUsuario", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PeticionesCambio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false),
                    Permiso = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Accion = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TipoEntidad = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    EntidadId = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    EntidadDescripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    SolicitadoPorId = table.Column<int>(type: "int", nullable: false),
                    SolicitadoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SolicitadoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResueltoPorId = table.Column<int>(type: "int", nullable: true),
                    ResueltoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ResueltoEn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ComentarioResolucion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeticionesCambio", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Proveedores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Rif = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Telefono = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proveedores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalidasInventario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Destino = table.Column<int>(type: "int", nullable: false),
                    DestinoDetalle = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Motivo = table.Column<int>(type: "int", nullable: false),
                    RetiradoPor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    AutorizadoPorId = table.Column<int>(type: "int", nullable: false),
                    AutorizadoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    MotivoAnulacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaAnulacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalidasInventario", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false),
                    NombreUsuario = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    NombreCompleto = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Rol = table.Column<int>(type: "int", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PasswordSalt = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EntradaInventarioLinea",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArticuloId = table.Column<int>(type: "int", nullable: false),
                    ArticuloCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ArticuloNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    UnidadTexto = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EntradaInventarioId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntradaInventarioLinea", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntradaInventarioLinea_EntradasInventario_EntradaInventarioId",
                        column: x => x.EntradaInventarioId,
                        principalTable: "EntradasInventario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FacturaProveedorLinea",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DestinoTexto = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CantidadTexto = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FacturaProveedorId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacturaProveedorLinea", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FacturaProveedorLinea_FacturasProveedor_FacturaProveedorId",
                        column: x => x.FacturaProveedorId,
                        principalTable: "FacturasProveedor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalidaInventarioLinea",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArticuloId = table.Column<int>(type: "int", nullable: false),
                    ArticuloCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ArticuloNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    UnidadTexto = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SalidaInventarioId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalidaInventarioLinea", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalidaInventarioLinea_SalidasInventario_SalidaInventarioId",
                        column: x => x.SalidaInventarioId,
                        principalTable: "SalidasInventario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Articulos_OrganizacionId_Codigo",
                table: "Articulos",
                columns: new[] { "OrganizacionId", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntradaInventarioLinea_EntradaInventarioId",
                table: "EntradaInventarioLinea",
                column: "EntradaInventarioId");

            migrationBuilder.CreateIndex(
                name: "IX_EntradasInventario_FacturaProveedorId",
                table: "EntradasInventario",
                column: "FacturaProveedorId");

            migrationBuilder.CreateIndex(
                name: "IX_EntradasInventario_OrganizacionId_Numero",
                table: "EntradasInventario",
                columns: new[] { "OrganizacionId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FacturaProveedorLinea_FacturaProveedorId",
                table: "FacturaProveedorLinea",
                column: "FacturaProveedorId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosBanco_CuentaId_Fecha",
                table: "MovimientosBanco",
                columns: new[] { "CuentaId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosBanco_Origen_OrigenId",
                table: "MovimientosBanco",
                columns: new[] { "Origen", "OrigenId" });

            migrationBuilder.CreateIndex(
                name: "IX_Organizaciones_Codigo",
                table: "Organizaciones",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermisosUsuario_UsuarioId_Permiso",
                table: "PermisosUsuario",
                columns: new[] { "UsuarioId", "Permiso" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalidaInventarioLinea_SalidaInventarioId",
                table: "SalidaInventarioLinea",
                column: "SalidaInventarioId");

            migrationBuilder.CreateIndex(
                name: "IX_SalidasInventario_OrganizacionId_Numero",
                table: "SalidasInventario",
                columns: new[] { "OrganizacionId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_NombreUsuario",
                table: "Usuarios",
                column: "NombreUsuario",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Articulos");

            migrationBuilder.DropTable(
                name: "CuentasBancarias");

            migrationBuilder.DropTable(
                name: "EntradaInventarioLinea");

            migrationBuilder.DropTable(
                name: "FacturaProveedorLinea");

            migrationBuilder.DropTable(
                name: "MovimientosBanco");

            migrationBuilder.DropTable(
                name: "Organizaciones");

            migrationBuilder.DropTable(
                name: "PermisosUsuario");

            migrationBuilder.DropTable(
                name: "PeticionesCambio");

            migrationBuilder.DropTable(
                name: "Proveedores");

            migrationBuilder.DropTable(
                name: "SalidaInventarioLinea");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "EntradasInventario");

            migrationBuilder.DropTable(
                name: "FacturasProveedor");

            migrationBuilder.DropTable(
                name: "SalidasInventario");
        }
    }
}
