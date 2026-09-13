using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.BD;

public class SqlUsuarioDataSource : SqlCrudDataSource<Usuario, int>, IUsuarioDataSource
{
    protected override IQueryable<Usuario> Ordenar(IQueryable<Usuario> consulta)
        => consulta.OrderBy(u => u.NombreUsuario);

    // IgnoreQueryFilters: al autenticar no hay ambito todavia, asi que el filtro global
    // taparia al propio usuario que intenta entrar. Es uno de los dos sitios donde se salta
    // a proposito (el otro es el selector de organizaciones del Desarrollador).
    //
    // Por eso los tres metodos de abajo NO usan los ayudantes de la clase base: la base
    // consulta siempre dentro del ambito, que es justo lo que aqui hay que evitar.
    public Usuario? BuscarPorNombreSinAmbito(string nombreUsuario)
    {
        using var context = new AsoAdminisDbContext();
        return context.Usuarios
            .IgnoreQueryFilters()
            .FirstOrDefault(u => u.NombreUsuario == nombreUsuario);
    }

    public IReadOnlyList<PermisoUsuario> AjustesDe(int usuarioId)
    {
        using var context = new AsoAdminisDbContext();
        return context.PermisosUsuario
            .IgnoreQueryFilters()
            .Where(p => p.UsuarioId == usuarioId)
            .ToList();
    }

    public bool ExisteAlguno()
    {
        using var context = new AsoAdminisDbContext();
        return context.Usuarios.IgnoreQueryFilters().Any();
    }
}
