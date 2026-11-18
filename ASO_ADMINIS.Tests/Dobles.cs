using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;
using ASO_ADMINIS.Desktop.Services.Fiscal;

namespace ASO_ADMINIS.Tests;

/// <summary>
/// Antes de cualquier prueba: el diario fiscal va a una carpeta temporal, nunca al %AppData% real
/// de la máquina (ahí quedarían "documentos emitidos" de prueba para conciliar a mano).
/// </summary>
internal static class InicioDePruebas
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Iniciar() =>
        DiarioFiscal.Carpeta = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aso-pruebas", Guid.NewGuid().ToString("N"));
}

/// <summary>Una fuente cuyo contenido se puede fotografiar y devolver a la foto.</summary>
internal interface IRestaurable
{
    object Foto();
    void Restaurar(object foto);
}

/// <summary>
/// Unidad de trabajo de prueba: si la operación lanza, devuelve las fuentes indicadas a como
/// estaban, igual que haría el rollback de la transacción real. Así se prueba que un servicio
/// mete sus escrituras en UNA unidad de trabajo, sin SQL Server.
/// </summary>
internal sealed class TransaccionEnMemoria(params IRestaurable[] fuentes) : IUnidadDeTrabajo
{
    public int Ejecuciones { get; private set; }

    public T Ejecutar<T>(string? bloqueo, Func<T> accion)
    {
        Ejecuciones++;
        var fotos = fuentes.Select(f => f.Foto()).ToList();
        try
        {
            return accion();
        }
        catch
        {
            for (var i = 0; i < fuentes.Length; i++)
                fuentes[i].Restaurar(fotos[i]);
            throw;
        }
    }
}

/// <summary>Fuente de datos en memoria con Id autoincremental.</summary>
internal class FuenteEnMemoria<T> : ICrudDataSource<T, int>, IRestaurable where T : class, IEntidad<int>
{
    protected readonly List<T> Filas = [];
    private int _siguienteId = 1;

    public object Foto() => (Filas.ToList(), _siguienteId);

    public void Restaurar(object foto)
    {
        var (filas, siguiente) = ((List<T>, int))foto;
        Filas.Clear();
        Filas.AddRange(filas);
        _siguienteId = siguiente;
    }

    public IEnumerable<T> GetAll() => Filas.ToList();
    public T? GetById(int id) => Filas.FirstOrDefault(f => f.Id == id);

    public virtual T Add(T item)
    {
        // IEntidad expone Id de solo lectura; los modelos concretos sí tienen setter.
        typeof(T).GetProperty(nameof(IEntidad<int>.Id))!.SetValue(item, _siguienteId++);
        Filas.Add(item);
        return item;
    }

    public virtual void Update(T item)
    {
        var i = Filas.FindIndex(f => f.Id == item.Id);
        if (i < 0)
            throw new InvalidOperationException($"No existe la fila {item.Id}.");
        Filas[i] = item;
    }

    public void Delete(int id) => Filas.RemoveAll(f => f.Id == id);
}

internal sealed class GastosEnMemoria : FuenteEnMemoria<GastoVario>, IGastoVarioDataSource;

internal sealed class FacturasProveedorEnMemoria : FuenteEnMemoria<FacturaProveedor>, IFacturaProveedorDataSource
{
    public IEnumerable<FacturaProveedor> GetByProveedor(int proveedorId) =>
        GetAll().Where(f => f.ProveedorId == proveedorId);
}

internal sealed class CuentasEnMemoria : FuenteEnMemoria<CuentaBancaria>, ICuentaBancariaDataSource
{
    public IEnumerable<CuentaBancaria> GetActivas() => GetAll().Where(c => c.Activa);
}

internal sealed class TasasEnMemoria : FuenteEnMemoria<TasaCambio>, ITasaCambioDataSource
{
    public TasaCambio? GetUltima() => GetAll().OrderByDescending(t => t.Fecha).FirstOrDefault();
}

/// <summary>Movimientos de banco en memoria; con <see cref="Fallar"/> el siguiente Add lanza.</summary>
internal sealed class MovimientosEnMemoria : FuenteEnMemoria<MovimientoBanco>, IMovimientoBancoDataSource
{
    public bool Fallar { get; set; }

    public override MovimientoBanco Add(MovimientoBanco item) =>
        Fallar ? throw new InvalidOperationException("Banco caído (simulado).") : base.Add(item);

    public IEnumerable<MovimientoBanco> GetByCuenta(int cuentaId) => GetAll().Where(m => m.CuentaId == cuentaId);

    public IEnumerable<MovimientoBanco> GetByOrigen(OrigenMovimiento origen, int origenId) =>
        GetAll().Where(m => m.Origen == origen && m.OrigenId == origenId);
}

/// <summary>
/// Ventas en memoria que imita el índice único (OrganizacionId, Numero) de SQL Server. Con
/// <see cref="RivalEnElPrimerAdd"/> simula otra caja que guarda el mismo número justo antes.
/// </summary>
internal sealed class VentasEnMemoria : FuenteEnMemoria<Venta>, IVentaDataSource
{
    public bool RivalEnElPrimerAdd { get; set; }

    public override Venta Add(Venta item)
    {
        if (RivalEnElPrimerAdd)
        {
            RivalEnElPrimerAdd = false;
            base.Add(new Venta { Numero = item.Numero, NumeroFiscal = "OTRA-CAJA" });
        }

        if (Filas.Any(v => v.Numero == item.Numero))
            throw new InvalidOperationException($"Índice único: Numero {item.Numero} repetido.");

        return base.Add(item);
    }

    public void MarcarTransmitido(int id, DateTime cuando)
    {
        var v = GetById(id)!;
        v.Estado = EstadoVenta.Registrada;
        v.FechaTransmision = cuando;
    }

    public void EstamparZ(IReadOnlyCollection<int> ids, int numeroZ)
    {
        foreach (var v in Filas.Where(v => ids.Contains(v.Id) && v.NumeroZ is null))
            v.NumeroZ = numeroZ;
    }
}

/// <summary>Turnos en memoria que imita el índice único filtrado: un solo turno abierto por caja.</summary>
internal sealed class TurnosEnMemoria : FuenteEnMemoria<Turno>, ITurnoDataSource
{
    public override Turno Add(Turno item)
    {
        if (item.EstaAbierto && Filas.Any(t => t.CajaId == item.CajaId && t.EstaAbierto))
            throw new InvalidOperationException($"Índice único: la caja {item.CajaId} ya tiene un turno abierto.");
        return base.Add(item);
    }

    public Turno? AbiertoDe(int cajaId) => Filas.FirstOrDefault(t => t.CajaId == cajaId && t.EstaAbierto);
}

/// <summary>Usuarios en memoria, con el bloqueo por intentos como lo hace la fuente SQL.</summary>
internal sealed class UsuariosEnMemoria : FuenteEnMemoria<Usuario>, IUsuarioDataSource
{
    public Usuario? BuscarPorNombreSinAmbito(string nombreUsuario) =>
        Filas.FirstOrDefault(u => u.NombreUsuario == nombreUsuario)?.Clonar();

    public IReadOnlyList<PermisoUsuario> AjustesDe(int usuarioId) => [];
    public bool ExisteAlguno() => Filas.Count > 0;

    public Usuario RegistrarIntentoFallido(int usuarioId, int limite, DateTime bloquearHasta)
    {
        var u = GetById(usuarioId)!;
        u.IntentosFallidos++;
        if (u.IntentosFallidos >= limite)
        {
            u.BloqueadoHasta = bloquearHasta;
            u.IntentosFallidos = 0;
        }
        return u.Clonar();
    }

    public void ReiniciarIntentos(int usuarioId)
    {
        var u = GetById(usuarioId)!;
        u.IntentosFallidos = 0;
        u.BloqueadoHasta = null;
    }

    public ResultadoAutenticacion? LeerParaSesion(int usuarioId) =>
        GetById(usuarioId) is { } u ? new ResultadoAutenticacion(u.Clonar(), []) : null;
}

/// <summary>Talonarios en memoria; los números usados salen de las ventas en memoria.</summary>
internal sealed class RangosEnMemoria(VentasEnMemoria ventas) : FuenteEnMemoria<RangoContingencia>, IRangoContingenciaDataSource
{
    public IReadOnlyList<string> NumerosDeControlUsados() =>
        [.. ventas.GetAll().Select(v => v.NumeroControl).Where(n => !string.IsNullOrWhiteSpace(n))];
}

internal sealed class ClientesEnMemoria : FuenteEnMemoria<Cliente>, IClienteDataSource;

internal sealed class ArticulosEnMemoria : FuenteEnMemoria<Articulo>, IArticuloDataSource
{
    public IEnumerable<Articulo> GetActivos() => GetAll();
}

internal sealed class SalidasEnMemoria : FuenteEnMemoria<SalidaInventario>, ISalidaInventarioDataSource;

internal sealed class EntradasEnMemoria : FuenteEnMemoria<EntradaInventario>, IEntradaInventarioDataSource
{
    public IEnumerable<EntradaInventario> GetByFacturaProveedor(int facturaProveedorId) =>
        GetAll().Where(e => e.FacturaProveedorId == facturaProveedorId);
}

/// <summary>Sesión que lo puede todo salvo los permisos de <see cref="Negados"/>.</summary>
internal sealed class SesionTotal : ISesionActual
{
    public HashSet<string> Negados { get; } = [];

    public Usuario? UsuarioActual { get; set; } = new() { Id = 1, NombreCompleto = "Pruebas" };
    public Caja? CajaActual { get; set; } = new() { Id = 1, Codigo = "CAJA-01", Activa = true };
    public bool EstaAutenticado => true;
    public void IniciarSesion(Usuario usuario, IEnumerable<PermisoUsuario>? ajustes = null) { }
    public void CerrarSesion() { }
    public bool Puede(string permiso) => !Negados.Contains(permiso);
    public bool PuedeSolicitar(string permiso) => true;
}

/// <summary>Emisor que numera en memoria, sin escribir archivos.</summary>
internal sealed class EmisorDePrueba : IEmisorFiscal
{
    private int _correlativo;

    public string Nombre => "Emisor de prueba";
    public bool TieneReportesXZ => false;
    public bool EsFiscal => false;

    public ResultadoFiscal Emitir(Venta documento, Organizacion emisor)
    {
        _correlativo++;
        return new ResultadoFiscal($"F{_correlativo:D8}", $"00-{_correlativo:D8}", "PRUEBA", DateTime.Now, null);
    }

    public void TransmitirContingencia(Venta documento, Organizacion emisor) { }
    public EstadoEmisor ProbarConexion() => new(true, "Emisor de prueba");
    public ResumenFiscal ReporteX(string caja) => throw new NotSupportedException();
    public ResumenFiscal ReporteZ(string caja) => throw new NotSupportedException();
}

internal static class Escenario
{
    public static Organizacion Organizacion() => new()
    {
        Id = 1,
        Nombre = "Comercio de prueba",
        Codigo = "PRB",
        RazonSocial = "Comercio de Prueba, C.A.",
        Rif = "J-00002961-0",
        DireccionFiscal = "Av. Principal, Caracas"
    };

    /// <summary>Id del servicio que siembra <see cref="Montar"/>: exento, 10 US$.</summary>
    public const int ServicioMezclaId = 1;

    /// <summary>Id del aceite que siembra <see cref="Montar"/>: bien en la general, 5 US$ el litro, 20 L en existencia.</summary>
    public const int AceiteId = 2;

    public sealed record Montaje(VentasService Ventas, ArticulosEnMemoria Articulos,
                                 SalidasEnMemoria Salidas, EntradasEnMemoria Entradas,
                                 ClientesEnMemoria ClientesFuente, ClientesService Clientes, SesionTotal Sesion,
                                 TurnosEnMemoria TurnosFuente, TurnosService Turnos);

    public static VentasService Servicio(VentasEnMemoria ventas) => Montar(ventas).Ventas;

    /// <summary>
    /// VentasService sobre un catálogo con un servicio y un bien con existencia, con la sesión en
    /// CAJA-01 y su turno ya abierto (sin turno no se factura). Con <paramref name="abrirTurno"/>
    /// en false, la caja queda sin turno.
    /// </summary>
    public static Montaje Montar(VentasEnMemoria ventas, bool abrirTurno = true, IEmisorFiscal? emisor = null)
    {
        var articulos = new ArticulosEnMemoria();
        var salidas = new SalidasEnMemoria();
        var entradas = new EntradasEnMemoria();

        articulos.Add(new Articulo
        {
            Codigo = "SRV-MEZ", Descripcion = "Mezcla de pintura a la medida", Tipo = TipoArticulo.Servicio,
            PrecioVenta = 10m, Alicuota = TipoAlicuota.Exento, Activo = true
        });
        articulos.Add(new Articulo
        {
            Codigo = "ACE-15W40", Descripcion = "Aceite de motor 15W40 mineral", Unidad = UnidadMedida.Litro,
            PrecioVenta = 5m, Alicuota = TipoAlicuota.General, Activo = true
        });
        entradas.Add(new EntradaInventario
        {
            Numero = "ENT-000001", Tipo = TipoEntrada.Ajuste, Estado = EstadoEntrada.Registrada,
            Lineas = [new EntradaInventarioLinea { ArticuloId = AceiteId, Cantidad = 20m, CantidadPresentacion = 20m, Factor = 1m }]
        });

        var org = Organizacion();
        var sesion = new SesionTotal();
        var fuenteClientes = new ClientesEnMemoria();
        var clientes = new ClientesService(fuenteClientes, sesion);
        // Un talonario de contingencia autorizado: 00-000001 al 00-000500.
        var rangos = new RangosEnMemoria(ventas);
        rangos.Add(new RangoContingencia { Prefijo = "00-", Desde = 1, Hasta = 500, Digitos = 6, Imprenta = "Imprenta de prueba", Activo = true });

        var fuenteTurnos = new TurnosEnMemoria();
        var turnos = new TurnosService(fuenteTurnos, ventas, sesion);
        if (abrirTurno)
            turnos.Abrir(0m, 0m);

        var servicio = new VentasService(ventas, articulos, salidas, entradas,
                                         new InventarioService(articulos, entradas, salidas),
                                         sesion, emisor ?? new EmisorDePrueba(),
                                         organizacion: () => org,
                                         turno: turnos.TurnoParaFacturar,
                                         clientes: clientes,
                                         rangos: rangos);
        return new Montaje(servicio, articulos, salidas, entradas, fuenteClientes, clientes, sesion,
                           fuenteTurnos, turnos);
    }

    /// <summary>
    /// Factura del servicio de mezcla (no exige existencia), exenta, 10 US$ a tasa 40, pagada en
    /// bolívares.
    /// </summary>
    public static Venta Borrador() => new()
    {
        TasaCambioUsada = 40m,
        ClienteRif = "V-11111111",
        ClienteNombre = "Cliente de prueba",
        ClienteDireccion = "Caracas",
        Lineas = [Renglon(ServicioMezclaId, "serv", 1)],
        Pagos = [new VentaPago { Metodo = MetodoPago.EfectivoBs, MontoBs = 400m }]
    };

    /// <summary>Renglón como lo arma el editor: con precio y alícuota del borrador (que el servicio reemplaza).</summary>
    public static VentaLinea Renglon(int articuloId, string presentacion, decimal cantidad, decimal precioUsd = 10m) => new()
    {
        ArticuloId = articuloId,
        Descripcion = "lo que diga la pantalla",
        PresentacionNombre = presentacion,
        Factor = 1,
        Cantidad = cantidad,
        PrecioUnitarioUsd = precioUsd,
        Alicuota = TipoAlicuota.Exento
    };
}
