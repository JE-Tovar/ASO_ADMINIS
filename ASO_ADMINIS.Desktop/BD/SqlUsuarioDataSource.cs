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

    /// <summary>El listado lleva el código de la caja asignada, para la columna "Caja" de la grilla.</summary>
    public override IEnumerable<Usuario> GetAll()
    {
        using var context = new AsoAdminisDbContext();
        var usuarios = Ordenar(context.Usuarios).ToList();
        var cajas = context.Cajas.ToDictionary(c => c.Id, c => c.Codigo);

        foreach (var u in usuarios)
            u.CajaTexto = u.CajaId is { } id && cajas.TryGetValue(id, out var codigo) ? codigo : string.Empty;

        return usuarios;
    }

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

    public Usuario RegistrarIntentoFallido(int usuarioId, int limite, DateTime bloquearHasta)
    {
        using var context = new AsoAdminisDbContext();
        var usuario = context.Usuarios.IgnoreQueryFilters().First(u => u.Id == usuarioId);

        usuario.IntentosFallidos++;
        if (usuario.IntentosFallidos >= limite)
        {
            usuario.BloqueadoHasta = bloquearHasta;
            usuario.IntentosFallidos = 0;
        }

        context.SaveChanges();
        return usuario;
    }

    public void ReiniciarIntentos(int usuarioId)
    {
        using var context = new AsoAdminisDbContext();
        var usuario = context.Usuarios.IgnoreQueryFilters().First(u => u.Id == usuarioId);
        if (usuario.IntentosFallidos == 0 && usuario.BloqueadoHasta is null)
            return;

        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;
        context.SaveChanges();
    }

    public ResultadoAutenticacion? LeerParaSesion(int usuarioId)
    {
        using var context = new AsoAdminisDbContext();
        var usuario = context.Usuarios.IgnoreQueryFilters().AsNoTracking().FirstOrDefault(u => u.Id == usuarioId);
        if (usuario is null)
            return null;

        var ajustes = context.PermisosUsuario.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.UsuarioId == usuarioId).ToList();
        return new ResultadoAutenticacion(usuario, ajustes);
    }

    public bool ExisteAlguno()
    {
        using var context = new AsoAdminisDbContext();
        return context.Usuarios.IgnoreQueryFilters().Any();
    }
}
