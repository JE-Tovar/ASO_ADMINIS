using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASO_ADMINIS.Desktop.Migrations
{
    /// <inheritdoc />
    public partial class QuitarRolOperador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Escrita a mano: el rol Operador se retira (Rol.OperadorRetirado = 0 ya no da permisos).
            // Sus usuarios pasan a Supervisor (= 1), que tiene todo lo que tenía el Operador y más,
            // así que nadie pierde acceso. Sus ajustes de permisos (PermisosUsuario) se conservan.
            migrationBuilder.Sql("UPDATE Usuarios SET Rol = 1 WHERE Rol = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sin vuelta atrás: ya no se sabe qué Supervisores eran Operadores. Si hiciera falta,
            // la bitácora no lo registra (fue SQL directo); está en el respaldo previo.
        }
    }
}
