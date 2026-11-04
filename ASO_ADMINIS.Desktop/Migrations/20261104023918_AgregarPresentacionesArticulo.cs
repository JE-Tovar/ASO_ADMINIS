using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASO_ADMINIS.Desktop.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPresentacionesArticulo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Factor",
                table: "Ventas",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<string>(
                name: "PresentacionNombre",
                table: "Ventas",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<decimal>(
                name: "Cantidad",
                table: "SalidaInventarioLinea",
                type: "decimal(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "Cantidad",
                table: "EntradaInventarioLinea",
                type: "decimal(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddColumn<decimal>(
                name: "CantidadPresentacion",
                table: "EntradaInventarioLinea",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Factor",
                table: "EntradaInventarioLinea",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<string>(
                name: "PresentacionNombre",
                table: "EntradaInventarioLinea",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "PresentacionArticulo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Factor = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PrecioVenta = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ArticuloId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PresentacionArticulo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PresentacionArticulo_Articulos_ArticuloId",
                        column: x => x.ArticuloId,
                        principalTable: "Articulos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PresentacionArticulo_ArticuloId",
                table: "PresentacionArticulo",
                column: "ArticuloId");

            // Agregado a mano: los documentos anteriores se cargaron todos en la unidad base, así
            // que su presentación es esa (factor 1, ya puesto por el default) y lo tecleado es la
            // propia cantidad. Sin esto se leerían como "0,00" de una presentación sin nombre.
            migrationBuilder.Sql(
                "UPDATE EntradaInventarioLinea " +
                "SET CantidadPresentacion = Cantidad, PresentacionNombre = UnidadTexto;");

            // La unidad corta del artículo vendido (mismo texto que Articulo.UnidadCorta; el enum
            // UnidadMedida se guarda como ordinal).
            migrationBuilder.Sql(
                "UPDATE v SET v.PresentacionNombre = CASE a.Unidad " +
                "WHEN 0 THEN N'pza' WHEN 1 THEN N'caja' WHEN 2 THEN N'pal' WHEN 3 THEN N'kg' " +
                "WHEN 4 THEN N'L' WHEN 5 THEN N'm' WHEN 6 THEN N'rollo' WHEN 7 THEN N'saco' " +
                "ELSE N'par' END " +
                "FROM Ventas v INNER JOIN Articulos a ON a.Id = v.ArticuloId;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PresentacionArticulo");

            migrationBuilder.DropColumn(
                name: "Factor",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "PresentacionNombre",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "CantidadPresentacion",
                table: "EntradaInventarioLinea");

            migrationBuilder.DropColumn(
                name: "Factor",
                table: "EntradaInventarioLinea");

            migrationBuilder.DropColumn(
                name: "PresentacionNombre",
                table: "EntradaInventarioLinea");

            migrationBuilder.AlterColumn<decimal>(
                name: "Cantidad",
                table: "SalidaInventarioLinea",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "Cantidad",
                table: "EntradaInventarioLinea",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)");
        }
    }
}
