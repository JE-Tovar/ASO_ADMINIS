using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;
using ASO_ADMINIS.Desktop.Services.Fiscal;

namespace ASO_ADMINIS.Tests;

/// <summary>Fuente de datos en memoria con Id autoincremental.</summary>
internal class FuenteEnMemoria<T> : ICrudDataSource<T, int> where T : class, IEntidad<int>
{
    protected readonly List<T> Filas = [];
    private int _siguienteId = 1;

    public IEnumerable<T> GetAll() => Filas.ToList();
    public T? GetById(int id) => Filas.FirstOrDefault(f => f.Id == id);

    public virtual T Add(T item)
    {
        // IEntidad expone Id de solo lectura; los modelos concretos sí tienen setter.
        typeof(T).GetProperty(nameof(IEntidad<int>.Id))!.SetValue(item, _siguienteId++);
        Filas.Add(item);
        return item;
    }

    public void Update(T item)
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

/// <summary>Sesión que lo puede todo: las pruebas de permisos no son el objetivo aquí.</summary>
internal sealed class SesionTotal : ISesionActual
{
    public Usuario? UsuarioActual { get; } = new() { Id = 1, NombreCompleto = "Pruebas" };
    public bool EstaAutenticado => true;
    public void IniciarSesion(Usuario usuario, IEnumerable<PermisoUsuario>? ajustes = null) { }
    public void CerrarSesion() { }
    public bool Puede(string permiso) => true;
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

    public static VentasService Servicio(VentasEnMemoria ventas)
    {
        var articulos = new ArticulosEnMemoria();
        var salidas = new SalidasEnMemoria();
        var entradas = new EntradasEnMemoria();
        var org = Organizacion();
        return new VentasService(ventas, articulos, salidas, entradas,
                                 new InventarioService(articulos, entradas, salidas),
                                 new SesionTotal(), new EmisorDePrueba(),
                                 organizacion: () => org, caja: () => "CAJA-01");
    }

    /// <summary>
    /// Factura de un servicio sin artículo de almacén (no exige existencia), exenta, 10 US$ a
    /// tasa 40, pagada en bolívares.
    /// </summary>
    public static Venta Borrador() => new()
    {
        TasaCambioUsada = 40m,
        Lineas =
        [
            new VentaLinea
            {
                Descripcion = "Mezcla de pintura a la medida",
                Cantidad = 1,
                Factor = 1,
                PrecioUnitarioUsd = 10m,
                Alicuota = TipoAlicuota.Exento
            }
        ],
        Pagos = [new VentaPago { Metodo = MetodoPago.EfectivoBs, MontoBs = 400m }]
    };
}
