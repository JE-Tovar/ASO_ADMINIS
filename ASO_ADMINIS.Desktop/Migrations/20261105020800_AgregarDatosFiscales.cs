using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASO_ADMINIS.Desktop.Migrations
{
    /// <inheritdoc />
    public partial class AgregarDatosFiscales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Editada a mano: los defaultValue de los porcentajes (16/8/31/3) y de la alícuota de
            // los artículos existentes (1 = TipoAlicuota.General) no salen del modelo, porque
            // allí no llevan HasDefaultValue (ver el comentario en DbContext).
            migrationBuilder.AddColumn<bool>(
                name: "ContribuyenteEspecial",
                table: "Organizaciones",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DireccionFiscal",
                table: "Organizaciones",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "IgtfPorcentaje",
                table: "Organizaciones",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 3m);

            migrationBuilder.AddColumn<decimal>(
                name: "IvaAdicional",
                table: "Organizaciones",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 31m);

            migrationBuilder.AddColumn<decimal>(
                name: "IvaGeneral",
                table: "Organizaciones",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 16m);

            migrationBuilder.AddColumn<decimal>(
                name: "IvaReducida",
                table: "Organizaciones",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 8m);

            migrationBuilder.AddColumn<string>(
                name: "RazonSocial",
                table: "Organizaciones",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Rif",
                table: "Organizaciones",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Telefono",
                table: "Organizaciones",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Alicuota",
                table: "Articulos",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false),
                    Rif = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DireccionFiscal = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Telefono = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Correo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_OrganizacionId_Rif",
                table: "Clientes",
                columns: new[] { "OrganizacionId", "Rif" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Clientes");

            migrationBuilder.DropColumn(
                name: "ContribuyenteEspecial",
                table: "Organizaciones");

            migrationBuilder.DropColumn(
                name: "DireccionFiscal",
                table: "Organizaciones");

            migrationBuilder.DropColumn(
                name: "IgtfPorcentaje",
                table: "Organizaciones");

            migrationBuilder.DropColumn(
                name: "IvaAdicional",
                table: "Organizaciones");

            migrationBuilder.DropColumn(
                name: "IvaGeneral",
                table: "Organizaciones");

            migrationBuilder.DropColumn(
                name: "IvaReducida",
                table: "Organizaciones");

            migrationBuilder.DropColumn(
                name: "RazonSocial",
                table: "Organizaciones");

            migrationBuilder.DropColumn(
                name: "Rif",
                table: "Organizaciones");

            migrationBuilder.DropColumn(
                name: "Telefono",
                table: "Organizaciones");

            migrationBuilder.DropColumn(
                name: "Alicuota",
                table: "Articulos");
        }
    }
}
