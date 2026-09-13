using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.BD;

/// <summary>
/// Cuerpo comun de las fuentes de datos SQL. Las 25 clases <c>Sql…DataSource</c> hacian
/// literalmente lo mismo con el <c>DbSet</c> cambiado; aqui esta una sola vez y cada fuente
/// concreta solo declara su entidad y, si las tiene, sus consultas propias.
///
/// Usa <c>context.Set&lt;T&gt;()</c> en vez del <c>DbSet</c> nombrado. Es equivalente: el filtro
/// global por organizacion se registra por tipo de entidad en <c>AsoAdminisDbContext.OnModelCreating</c>,
/// no en la propiedad, asi que el aislamiento por nucleo sigue aplicandose igual.
///
/// Cada metodo abre y cierra su propio contexto, como antes: las fuentes son sin estado y el
/// ambito se lee al construir el contexto (ver <c>Configuration/DataSourceFactory.cs</c>).
/// </summary>
public abstract class SqlCrudDataSource<T, TId> : ICrudDataSource<T, TId>
    where T : class, IEntidad<TId>
{
    /// <summary>
    /// Orden del listado. Por defecto el que devuelva el motor; lo redefine quien necesite uno
    /// estable (ver <see cref="SqlUsuarioDataSource"/>).
    /// </summary>
    protected virtual IQueryable<T> Ordenar(IQueryable<T> consulta) => consulta;

    public virtual IEnumerable<T> GetAll()
    {
        using var context = new AsoAdminisDbContext();
        return Ordenar(context.Set<T>()).ToList();
    }

    public virtual T? GetById(TId id)
    {
        using var context = new AsoAdminisDbContext();
        return context.Set<T>().Find(id);
    }

    public virtual T Add(T item)
    {
        using var context = new AsoAdminisDbContext();
        context.Set<T>().Add(item);
        context.SaveChanges();
        return item;
    }

    public virtual void Update(T item)
    {
        using var context = new AsoAdminisDbContext();
        context.Set<T>().Update(item);
        context.SaveChanges();
    }

    public virtual void Delete(TId id)
    {
        using var context = new AsoAdminisDbContext();

        if (context.Set<T>().Find(id) is not { } entidad)
            return;

        context.Set<T>().Remove(entidad);
        context.SaveChanges();
    }

    /// <summary>
    /// Consulta filtrada, para los <c>GetBy…</c> propios de cada fuente. Materializa con
    /// <c>ToList()</c> antes de cerrar el contexto, igual que el resto de la clase.
    /// </summary>
    protected static List<T> Consultar(Func<IQueryable<T>, IQueryable<T>> filtro)
    {
        using var context = new AsoAdminisDbContext();
        return filtro(context.Set<T>()).ToList();
    }
}

/// <summary>
/// Fuente de un agregado: una raiz con una coleccion de hijos que se guarda entera de una vez
/// (finca con sus lotes, liquidacion o factura con sus lineas).
///
/// Existe por un detalle que se paga caro si se olvida: cada metodo abre un contexto nuevo, asi
/// que la entidad que llega viene DESCONECTADA. Un <c>Update</c> ingenuo de la cabecera guardaria
/// los hijos nuevos pero no borraria los que el usuario quito en el editor, y quedarian huerfanos
/// en la tabla. Por eso se carga el grafo rastreado, se eliminan los hijos viejos y recien
/// entonces se asignan los nuevos.
///
/// Tampoco puede usar <c>Find</c>: no admite <c>Include</c>, y sin el la raiz llegaria sin hijos.
/// </summary>
public abstract class SqlAgregadoDataSource<T, TId> : SqlCrudDataSource<T, TId>
    where T : class, IEntidad<TId>
{
    /// <summary>Cadena de <c>Include</c> que trae la raiz con sus hijos.</summary>
    protected abstract IQueryable<T> Incluir(IQueryable<T> consulta);

    /// <summary>Predicado de clave primaria. Reemplaza a <c>Find</c>, que no admite Include.</summary>
    protected abstract Expression<Func<T, bool>> PorId(TId id);

    /// <summary>Los hijos que hay que borrar antes de reemplazarlos.</summary>
    protected abstract IEnumerable<object> HijosDe(T raiz);

    /// <summary>Pasa al grafo rastreado lo que <c>SetValues</c> no copia: los hijos y las colecciones.</summary>
    protected abstract void CopiarHijos(T destino, T origen);

    public override IEnumerable<T> GetAll()
    {
        using var context = new AsoAdminisDbContext();
        return Ordenar(Incluir(context.Set<T>())).ToList();
    }

    public override T? GetById(TId id)
    {
        using var context = new AsoAdminisDbContext();
        return Incluir(context.Set<T>()).FirstOrDefault(PorId(id));
    }

    public override void Update(T item)
    {
        using var context = new AsoAdminisDbContext();
        var existente = Incluir(context.Set<T>()).First(PorId(item.Id));

        context.RemoveRange(HijosDe(existente));
        context.SaveChanges();

        context.Entry(existente).CurrentValues.SetValues(item);
        CopiarHijos(existente, item);
        context.SaveChanges();
    }

    public override void Delete(TId id)
    {
        using var context = new AsoAdminisDbContext();

        if (Incluir(context.Set<T>()).FirstOrDefault(PorId(id)) is not { } raiz)
            return;

        context.Set<T>().Remove(raiz);
        context.SaveChanges();
    }
}
