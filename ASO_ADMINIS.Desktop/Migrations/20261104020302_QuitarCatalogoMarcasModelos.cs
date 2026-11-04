using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASO_ADMINIS.Desktop.Migrations
{
    /// <inheritdoc />
    public partial class QuitarCatalogoMarcasModelos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Orden escrito a mano (no el que generó EF) para no perder datos: primero se
            // traspasa lo que vivía en Modelos/Marcas al artículo vinculado, y recién al final
            // se borran el vínculo Articulos.ModeloId y las dos tablas.

            // 1. El precio de venta pasa del modelo a su artículo.
            migrationBuilder.AddColumn<decimal>(
                name: "PrecioVenta",
                table: "Articulos",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(
                "UPDATE a SET a.PrecioVenta = m.PrecioVenta " +
                "FROM Articulos a INNER JOIN Modelos m ON a.ModeloId = m.Id;");

            // 2. Cada venta deja de apuntar al modelo y pasa a apuntar a su artículo.
            migrationBuilder.RenameColumn(
                name: "ModeloId",
                table: "Ventas",
                newName: "ArticuloId");

            migrationBuilder.Sql(
                "UPDATE v SET v.ArticuloId = a.Id " +
                "FROM Ventas v INNER JOIN Articulos a ON a.ModeloId = v.ArticuloId;");

            // 3. Snapshots de texto de la venta: nombre y código del artículo, sin marca.
            migrationBuilder.RenameColumn(
                name: "ModeloNombre",
                table: "Ventas",
                newName: "ArticuloNombre");

            migrationBuilder.AddColumn<string>(
                name: "ArticuloCodigo",
                table: "Ventas",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                "UPDATE v SET v.ArticuloCodigo = a.Codigo " +
                "FROM Ventas v INNER JOIN Articulos a ON a.Id = v.ArticuloId;");

            migrationBuilder.DropColumn(
                name: "MarcaNombre",
                table: "Ventas");

            // 4. Ya sin nada que dependa de ellos: el vínculo y las tablas del catálogo.
            migrationBuilder.DropIndex(
                name: "IX_Articulos_ModeloId",
                table: "Articulos");

            migrationBuilder.DropColumn(
                name: "ModeloId",
                table: "Articulos");

            migrationBuilder.DropTable(
                name: "Modelos");

            migrationBuilder.DropTable(
                name: "Marcas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArticuloCodigo",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "PrecioVenta",
                table: "Articulos");

            migrationBuilder.RenameColumn(
                name: "ArticuloNombre",
                table: "Ventas",
                newName: "ModeloNombre");

            migrationBuilder.RenameColumn(
                name: "ArticuloId",
                table: "Ventas",
                newName: "ModeloId");

            migrationBuilder.AddColumn<string>(
                name: "MarcaNombre",
                table: "Ventas",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ModeloId",
                table: "Articulos",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Marcas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Marcas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Modelos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    MarcaId = table.Column<int>(type: "int", nullable: false),
                    MarcaNombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false),
                    PrecioVenta = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Unidad = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Modelos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Articulos_ModeloId",
                table: "Articulos",
                column: "ModeloId");

            migrationBuilder.CreateIndex(
                name: "IX_Marcas_OrganizacionId_Nombre",
                table: "Marcas",
                columns: new[] { "OrganizacionId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Modelos_MarcaId",
                table: "Modelos",
                column: "MarcaId");
        }
    }
}
