using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services.Fiscal;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Cierre del día: el resumen diario que calcula el sistema (siempre existe) y, con una
/// máquina fiscal, los reportes X y Z.
///
/// El Z se pide a la máquina, se guarda con lo que dice el sistema al lado para poder ver si
/// cuadran, y se estampa su número en los documentos de esa caja que todavía no tenían Z — así
/// el libro de ventas puede consolidar las ventas a consumidor final por Z.
/// </summary>
public sealed class CierreService(IVentaDataSource ventas,
                                  ICierreZDataSource cierres,
                                  IEmisorFiscal emisor,
                                  ISesionActual sesion)
{
    public IEmisorFiscal Emisor => emisor;

    /// <summary>Totales del día por alícuota y medio de pago, netos de notas de crédito.</summary>
    public ResumenDiario ResumenDiario(DateTime dia, string? caja = null)
    {
        var docs = ventas.GetAll()
            .Where(v => v.Vigente && !v.EsHistorico && v.Fecha.Date == dia.Date)
            .Where(v => string.IsNullOrWhiteSpace(caja) || v.CajaCodigo == caja)
            .ToList();
        return Services.ResumenDiario.De(dia, docs);
    }

    public IReadOnlyList<CierreZ> Historial() =>
        [.. cierres.GetAll().OrderByDescending(c => c.Fecha)];

    public ResumenFiscal ReporteX(string caja)
    {
        if (!emisor.TieneReportesXZ)
            throw new InvalidOperationException("El emisor fiscal configurado no tiene reporte X (es una imprenta digital).");
        return emisor.ReporteX(caja);
    }

    public CierreZ CerrarZ(string caja)
    {
        if (!sesion.Puede(Permisos.Ventas.CierreZ))
            throw new InvalidOperationException("No tienes permiso para hacer el cierre Z.");

        if (!emisor.TieneReportesXZ)
            throw new InvalidOperationException("El emisor fiscal configurado no tiene reporte Z (es una imprenta digital).");

        if (string.IsNullOrWhiteSpace(caja))
            throw new InvalidOperationException("Esta máquina no tiene caja asignada. Indíquela en Configuración · Aplicación.");

        var z = emisor.ReporteZ(caja);

        // Los documentos que entran en este Z: los de la caja que aún no tenían Z, ya
        // transmitidos (un documento en contingencia todavía no está en la máquina).
        var incluidos = ventas.GetAll()
            .Where(v => v.CajaCodigo == caja && v.NumeroZ is null && !v.EsHistorico
                        && v.Estado == EstadoVenta.Registrada)
            .ToList();

        var nuevo = new CierreZ
        {
            NumeroZ = z.NumeroZ,
            CajaCodigo = caja,
            SerialEmisor = incluidos.Select(v => v.SerialEmisor).FirstOrDefault(s => s != "CONTINGENCIA") ?? string.Empty,
            Fecha = z.FechaHora,
            Documentos = z.Documentos,
            Exento = z.Exento,
            BaseGeneral = z.BaseGeneral,
            IvaGeneral = z.IvaGeneral,
            BaseReducida = z.BaseReducida,
            IvaReducida = z.IvaReducida,
            BaseAdicional = z.BaseAdicional,
            IvaAdicional = z.IvaAdicional,
            Igtf = z.Igtf,
            Total = z.Total,
            DocumentosSistema = incluidos.Count,
            TotalSistema = incluidos.Sum(v => v.Signo * v.Total),
            CreadoPorId = sesion.UsuarioActual?.Id ?? 0,
            CreadoPorNombre = sesion.UsuarioActual?.NombreCompleto ?? string.Empty
        };

        // El Z ya se hizo en la máquina y no se repite: si guardarlo falla, queda en el diario
        // fiscal para registrarlo a mano, igual que un documento emitido sin guardar.
        CierreZ cierre;
        try
        {
            cierre = cierres.Add(nuevo);
            if (incluidos.Count > 0)
                ventas.EstamparZ([.. incluidos.Select(v => v.Id)], z.NumeroZ);
        }
        catch (Exception ex)
        {
            DiarioFiscal.Anotar("CierreZ", new { Cierre = nuevo, Documentos = incluidos.Select(v => v.Id).ToList() }, ex);
            throw new InvalidOperationException(
                $"El reporte Z n° {z.NumeroZ} YA SE HIZO en la máquina, pero no se pudo guardar en la base: {ex.Message}" +
                $"\n\nQuedó anotado en el diario fiscal ({DiarioFiscal.RutaTexto}) para registrarlo a mano.", ex);
        }

        return cierre;
    }
}

/// <summary>Totales de un día (o de cualquier conjunto de documentos), netos de notas de crédito.</summary>
public sealed record ResumenDiario(
    DateTime Dia,
    int Facturas,
    int NotasCredito,
    int NotasDebito,
    decimal Exento,
    decimal BaseGeneral,
    decimal IvaGeneral,
    decimal BaseReducida,
    decimal IvaReducida,
    decimal BaseAdicional,
    decimal IvaAdicional,
    decimal Igtf,
    decimal Total,
    IReadOnlyList<(string Metodo, decimal MontoBs, decimal MontoDivisa)> PorMetodo)
{
    public static ResumenDiario De(DateTime dia, IReadOnlyList<Venta> docs)
    {
        decimal S(Func<Venta, decimal> f) => docs.Sum(v => v.Signo * f(v));

        var porMetodo = docs.SelectMany(v => v.Pagos)
            .GroupBy(p => p.MetodoTexto)
            .Select(g => (g.Key, g.Sum(p => p.MontoBs), g.Sum(p => p.MontoDivisa)))
            .OrderByDescending(x => x.Item2)
            .ToList();

        return new ResumenDiario(
            dia,
            docs.Count(v => v.Tipo == TipoDocumentoVenta.Factura),
            docs.Count(v => v.Tipo == TipoDocumentoVenta.NotaCredito),
            docs.Count(v => v.Tipo == TipoDocumentoVenta.NotaDebito),
            S(v => v.MontoExento),
            S(v => v.BaseGeneral), S(v => v.IvaGeneral),
            S(v => v.BaseReducida), S(v => v.IvaReducida),
            S(v => v.BaseAdicional), S(v => v.IvaAdicional),
            S(v => v.Igtf),
            S(v => v.Total),
            porMetodo);
    }
}
