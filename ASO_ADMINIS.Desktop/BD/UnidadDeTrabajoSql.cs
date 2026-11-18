using System.Transactions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.BD;

/// <summary>
/// La unidad de trabajo real: un <see cref="TransactionScope"/> en el que se enlistan solas todas
/// las conexiones que abran las fuentes de datos.
///
/// <para><b>Por qué no escala a una transacción distribuida</b> (que .NET 8 no soporta): las
/// fuentes abren y cierran su contexto dentro de cada método, así que nunca hay dos conexiones
/// abiertas a la vez; SqlClient devuelve la misma conexión del pool, ya enlistada, a la siguiente
/// que se abre. Si algún día una fuente abriera un contexto dentro de otro, fallaría aquí con un
/// error claro en vez de corromper nada.</para>
///
/// <para><b>ReadCommitted</b> y no el Serializable que trae por defecto TransactionScope: con
/// Serializable, dos cajas leyendo el kardex a la vez se bloquean en cruz. La exclusión que sí
/// hace falta la da el candado con nombre (<c>sp_getapplock</c>).</para>
/// </summary>
public sealed class UnidadDeTrabajoSql : IUnidadDeTrabajo
{
    private static readonly TimeSpan Limite = TimeSpan.FromSeconds(60);
    private const int EsperaCandadoMs = 20_000;

    public T Ejecutar<T>(string? bloqueo, Func<T> accion)
    {
        using var alcance = new TransactionScope(
            TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted, Timeout = Limite },
            TransactionScopeAsyncFlowOption.Enabled);

        if (bloqueo is not null)
            TomarCandado(bloqueo);

        var resultado = accion();
        alcance.Complete();
        return resultado;
    }

    /// <summary>
    /// Candado exclusivo por organización, dueño la transacción: se suelta solo al confirmar o al
    /// deshacer. Si otra caja lo tiene más de <see cref="EsperaCandadoMs"/>, se avisa en vez de
    /// esperar indefinidamente.
    /// </summary>
    private static void TomarCandado(string nombre)
    {
        var recurso = $"ASO:{nombre}:{Ambito.Exigir()}";

        using var context = new AsoAdminisDbContext();
        var resultado = new SqlParameter("@r", System.Data.SqlDbType.Int) { Direction = System.Data.ParameterDirection.Output };
        context.Database.ExecuteSqlRaw(
            "EXEC @r = sp_getapplock @Resource = @recurso, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = @espera;",
            resultado, new SqlParameter("@recurso", recurso), new SqlParameter("@espera", EsperaCandadoMs));

        // 0 = concedido, 1 = concedido tras esperar; negativo = vencido, cancelado o error.
        if (resultado.Value is int r && r < 0)
        {
            Registro.Advertencia("No se obtuvo el candado {Recurso} (resultado {Resultado})", recurso, r);
            throw new InvalidOperationException(
                "Otra caja está registrando un documento en este momento. Reintenta en unos segundos.");
        }
    }
}
