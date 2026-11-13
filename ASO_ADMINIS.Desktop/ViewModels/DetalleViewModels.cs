using System.Collections.Generic;
using System.Linq;
using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Ficha de solo lectura de un registro, la que abre el doble clic sobre una fila que no se
/// puede editar (<see cref="CrudViewModelBase{T, TId}.AbrirCommand"/>). Usa la misma ventana que
/// los editores (<c>CrudEditorWindow</c>), con un solo botón "Cerrar" y nada que validar.
///
/// Cada ficha expone el documento entero como <see cref="Documento"/>: la vista lee de él sus
/// textos ya formateados (<c>…Texto</c>), y los <c>Chip…Style</c> de estado, que disparan sobre
/// <c>Estado</c>, funcionan poniéndole a la etiqueta <c>DataContext="{Binding Documento}"</c>.
/// </summary>
public abstract class DetalleViewModelBase<T>(T documento) : CrudEditorViewModelBase
{
    public T Documento { get; } = documento;

    public override string TextoAccion => "Cerrar";
    public override bool MuestraCancelar => false;

    protected override bool Validar(out string? error)
    {
        error = null;
        return true;
    }

    /// <summary>"Anulada el 12/11/2026: motivo", o vacío si no está anulado (y el dato se oculta).</summary>
    protected static string TextoAnulacion(DateTime? fecha, string? motivo) => fecha is null && string.IsNullOrWhiteSpace(motivo)
        ? string.Empty
        : $"{(fecha is { } f ? $"El {f:dd/MM/yyyy}" : "Sin fecha")}: {(string.IsNullOrWhiteSpace(motivo) ? "sin motivo" : motivo)}";
}

/// <summary>Factura, nota de crédito o nota de débito, entera: cliente, renglones, IVA y pagos.</summary>
public sealed class VentaDetalleViewModel(Venta venta) : DetalleViewModelBase<Venta>(venta)
{
    public override string Titulo => string.IsNullOrWhiteSpace(Documento.NumeroFiscal)
        ? $"{Documento.TipoTexto} {Documento.Numero}"
        : $"{Documento.TipoTexto} N° {Documento.NumeroFiscal}";

    public override double AnchoEditor => Ancho.Amplio;
    public override bool AbreMaximizado => true;

    public bool TienePagos => Documento.Pagos.Count > 0;

    public string ClienteIdentificacion => Documento.EsConsumidorFinal ? "Consumidor final" : Documento.ClienteRif;

    public string DocumentoAfectadoTexto => string.IsNullOrWhiteSpace(Documento.DocumentoAfectadoNumero)
        ? string.Empty
        : Documento.DocumentoAfectadoFecha is { } fecha
            ? $"Factura N° {Documento.DocumentoAfectadoNumero} del {fecha:dd/MM/yyyy}"
            : $"Factura N° {Documento.DocumentoAfectadoNumero}";

    public string NumeroZTexto => Documento.NumeroZ?.ToString() ?? string.Empty;

    public string TransmisionTexto => Documento.EnContingencia
        ? Documento.FechaTransmision is { } fecha
            ? $"Emitida en contingencia, transmitida el {fecha:dd/MM/yyyy HH:mm}"
            : "Emitida en contingencia, por transmitir"
        : string.Empty;

    public string TasaTexto => Documento.TasaCambioUsada > 0 ? $"Bs. {Documento.TasaCambioUsada:N2} por US$" : string.Empty;

    public string AnulacionTexto => TextoAnulacion(Documento.FechaAnulacion, Documento.MotivoAnulacion);

    /// <summary>Las filas del desglose fiscal que tienen monto: exento, cada alícuota, IGTF.</summary>
    public IReadOnlyList<RenglonTotal> Totales
    {
        get
        {
            var v = Documento;
            var filas = new List<RenglonTotal>();
            if (v.MontoExento != 0) filas.Add(new("Exento", v.MontoExento));
            if (v.BaseGeneral != 0) filas.Add(new($"Base imponible {v.PorcentajeGeneral:0.##} %", v.BaseGeneral));
            if (v.IvaGeneral != 0) filas.Add(new($"IVA {v.PorcentajeGeneral:0.##} %", v.IvaGeneral));
            if (v.BaseReducida != 0) filas.Add(new($"Base imponible {v.PorcentajeReducida:0.##} %", v.BaseReducida));
            if (v.IvaReducida != 0) filas.Add(new($"IVA {v.PorcentajeReducida:0.##} %", v.IvaReducida));
            if (v.BaseAdicional != 0) filas.Add(new($"Base imponible {v.PorcentajeAdicional:0.##} %", v.BaseAdicional));
            if (v.IvaAdicional != 0) filas.Add(new($"IVA {v.PorcentajeAdicional:0.##} %", v.IvaAdicional));
            if (v.Igtf != 0) filas.Add(new($"IGTF {v.PorcentajeIgtf:0.##} % (sobre {v.BaseIgtf:N2})", v.Igtf));
            if (v.Vuelto != 0) filas.Add(new("Vuelto", v.Vuelto));
            return filas;
        }
    }
}

/// <summary>Una fila del desglose de totales de la ficha de una venta.</summary>
public sealed record RenglonTotal(string Concepto, decimal Monto)
{
    public string MontoTexto => Monto.ToString("N2");
}

/// <summary>Entrada de almacén: de dónde vino, qué entró y a qué precio.</summary>
public sealed class EntradaDetalleViewModel(EntradaInventario entrada) : DetalleViewModelBase<EntradaInventario>(entrada)
{
    public override string Titulo => $"Entrada {Documento.Numero}";
    public override double AnchoEditor => Ancho.Amplio;
    public override bool AbreMaximizado => true;

    /// <summary>Un ajuste no lleva precios: se ocultan sus columnas y el total.</summary>
    public bool MuestraPrecios => Documento.GeneraCuentaPorPagar || Documento.Total != 0;

    public string VencimientoTexto => Documento.FechaVencimiento is { } fecha ? fecha.ToString("dd/MM/yyyy") : string.Empty;
    public string CuentaPorPagarTexto => Documento.FacturaProveedorNumero;
    public string AnulacionTexto => TextoAnulacion(Documento.FechaAnulacion, Documento.MotivoAnulacion);
}

/// <summary>Boleto de salida: a dónde fue, quién lo retiró y qué se llevó.</summary>
public sealed class SalidaDetalleViewModel(SalidaInventario salida) : DetalleViewModelBase<SalidaInventario>(salida)
{
    public override string Titulo => $"Boleto de salida {Documento.Numero}";
    public override double AnchoEditor => Ancho.Amplio;
    public override bool AbreMaximizado => true;

    public string TotalUnidadesTexto => Documento.TotalUnidades.ToString("N2");
    public string AnulacionTexto => TextoAnulacion(Documento.FechaAnulacion, Documento.MotivoAnulacion);
}

/// <summary>Gasto vario: todos sus campos, incluidos los que la lista no muestra.</summary>
public sealed class GastoVarioDetalleViewModel(GastoVario gasto) : DetalleViewModelBase<GastoVario>(gasto)
{
    public override string Titulo => $"Gasto {Documento.Numero}";
    public override double AnchoEditor => Ancho.Estandar;

    public string TasaTexto => Documento.Moneda == MonedaGasto.Usd ? $"Bs. {Documento.TasaCambio:N2} por US$" : string.Empty;
    public string RegistroTexto => $"{Documento.CreadoPorNombre} · {Documento.FechaCreacion:dd/MM/yyyy HH:mm}";
    public string AnulacionTexto => TextoAnulacion(Documento.FechaAnulacion, Documento.MotivoAnulacion);
}

/// <summary>Factura de proveedor que ya no se edita (pagada o anulada): renglones y pago.</summary>
public sealed class FacturaProveedorDetalleViewModel(FacturaProveedor factura) : DetalleViewModelBase<FacturaProveedor>(factura)
{
    public override string Titulo => $"Factura {Documento.NumeroDocumento} · {Documento.ProveedorNombre}";
    public override double AnchoEditor => Ancho.Amplio;

    public bool TieneLineas => Documento.Lineas.Count > 0;
    public string EmisionTexto => Documento.FechaEmision.ToString("dd/MM/yyyy");
    public string AnulacionTexto => TextoAnulacion(Documento.FechaAnulacion, Documento.MotivoAnulacion);
}
