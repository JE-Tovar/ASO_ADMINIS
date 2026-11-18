namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Ejecuta una operación de dominio de forma atómica: lo que escriban las fuentes de datos dentro
/// de <see cref="Ejecutar{T}"/> se guarda entero o no se guarda nada.
///
/// Existe porque cada método de las fuentes Sql abre su propio contexto, así que una operación
/// que toca dos tablas (venta + salida de almacén, pago + asiento de banco) quedaba a medias si
/// fallaba la segunda escritura.
///
/// <para><b>Bloqueo:</b> con <c>bloqueo</c> no nulo, además se toma un candado exclusivo con ese
/// nombre para la organización activa, que dura hasta el final de la transacción. Lo usan las
/// operaciones que validan y luego escriben algo que otra caja podría estar escribiendo a la vez
/// (la existencia del almacén, los correlativos "último + 1").</para>
///
/// Es estática como <see cref="Ajustes"/> y <see cref="CambiosDeDatos"/>: el arranque pone la
/// implementación de SQL; las pruebas en memoria se quedan con la directa, que no hace nada.
/// </summary>
public interface IUnidadDeTrabajo
{
    T Ejecutar<T>(string? bloqueo, Func<T> accion);
}

public static class UnidadDeTrabajo
{
    /// <summary>Candado de los documentos de la organización: ventas, almacén y correlativos.</summary>
    public const string Documentos = "Documentos";

    private static IUnidadDeTrabajo _global = new Directa();
    private static readonly AsyncLocal<IUnidadDeTrabajo?> _local = new();

    /// <summary>La de la aplicación (la fija el arranque), salvo que este flujo use otra (<see cref="Usar"/>).</summary>
    public static IUnidadDeTrabajo Actual
    {
        get => _local.Value ?? _global;
        set => _global = value;
    }

    /// <summary>
    /// Otra unidad de trabajo solo para el flujo actual, hasta desechar lo devuelto. Es para las
    /// pruebas: corren en paralelo, y cambiar <see cref="Actual"/> para todas se pisarían.
    /// </summary>
    public static IDisposable Usar(IUnidadDeTrabajo unidad)
    {
        var anterior = _local.Value;
        _local.Value = unidad;
        return new Restaurar(() => _local.Value = anterior);
    }

    private sealed class Restaurar(Action accion) : IDisposable
    {
        public void Dispose() => accion();
    }

    public static T Ejecutar<T>(string? bloqueo, Func<T> accion) => Actual.Ejecutar(bloqueo, accion);

    public static void Ejecutar(string? bloqueo, Action accion) =>
        Actual.Ejecutar<object?>(bloqueo, () => { accion(); return null; });

    /// <summary>Sin transacción ni bloqueo: para las pruebas con fuentes en memoria.</summary>
    public sealed class Directa : IUnidadDeTrabajo
    {
        public T Ejecutar<T>(string? bloqueo, Func<T> accion) => accion();
    }
}
