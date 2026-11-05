using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASO_ADMINIS.Desktop.Migrations
{
    /// <inheritdoc />
    public partial class ArticuloDescripcion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Editada a mano: el scaffold borraba "Nombre" y creaba "Descripcion" vacía, perdiendo
            // los datos. Es el mismo dato con otro nombre (y hasta 200 caracteres): se renombra.
            migrationBuilder.RenameColumn(name: "Nombre", table: "Articulos", newName: "Descripcion");

            migrationBuilder.AlterColumn<string>(
                name: "Descripcion", table: "Articulos", type: "nvarchar(200)", maxLength: 200, nullable: false,
                oldClrType: typeof(string), oldType: "nvarchar(150)", oldMaxLength: 150);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Descripcion", table: "Articulos", type: "nvarchar(150)", maxLength: 150, nullable: false,
                oldClrType: typeof(string), oldType: "nvarchar(200)", oldMaxLength: 200);

            migrationBuilder.RenameColumn(name: "Descripcion", table: "Articulos", newName: "Nombre");
        }
    }
}
