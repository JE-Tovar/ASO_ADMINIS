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

    public string ClienteIdentificacion => Services.TextosDeVenta.ClienteIdentificacion(Documento);

    public string DocumentoAfectadoTexto => Services.TextosDeVenta.DocumentoAfectado(Documento);

    // ---------------------------------------------------------------- Comprobante no fiscal

    /// <summary>Imprime la copia no fiscal (hoja carta) en la impresora que se elija.</summary>
    public System.Windows.Input.ICommand ImprimirComprobanteCommand => new RelayCommand(() =>
        ConComprobante("No se pudo imprimir", c =>
        {
            if (Services.ComprobanteImpreso.Imprimir(c))
                Services.Registro.Info("Comprobante no fiscal impreso: {Documento}", c.Titulo);
        }));

    /// <summary>Guarda la copia no fiscal como PDF, para mandarla al cliente.</summary>
    public System.Windows.Input.ICommand GuardarPdfCommand => new RelayCommand(() =>
        ConComprobante("No se pudo guardar el PDF", c =>
        {
            var ajustes = Services.Ajustes.Actual;
            var carpeta = System.IO.Directory.Exists(ajustes.UltimaCarpetaPdf)
                ? ajustes.UltimaCarpetaPdf
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            var dialogo = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Guardar comprobante no fiscal",
                Filter = "PDF|*.pdf",
                FileName = c.NombreArchivo,
                InitialDirectory = carpeta
            };
            if (dialogo.ShowDialog() != true)
                return;

            Services.ComprobantePdf.Guardar(c, dialogo.FileName);
            Services.Registro.Info("Comprobante no fiscal en PDF: {Documento} → {Ruta}", c.Titulo, dialogo.FileName);

            ajustes.UltimaCarpetaPdf = System.IO.Path.GetDirectoryName(dialogo.FileName) ?? string.Empty;
            Services.Ajustes.Guardar();

            if (new Services.ServicioDialogo().Confirmar("PDF guardado",
                    $"Se guardó en:\n{dialogo.FileName}\n\n¿Abrirlo ahora?"))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dialogo.FileName) { UseShellExecute = true });
        }));

    private void ConComprobante(string tituloError, Action<Services.ComprobanteNoFiscal> accion)
    {
        try
        {
            var organizacion = Services.Ambito.Actual
                ?? throw new InvalidOperationException("No hay una organización activa.");
            var emisor = Configuration.DataSourceFactory.CrearEmisorFiscal();
            accion(Services.ComprobanteNoFiscal.De(Documento, organizacion, emisor.Nombre, emisor.EsFiscal));
        }
        catch (Exception ex)
        {
            Services.Registro.Error(ex, "{Titulo}: {Documento}", tituloError, Documento.Numero);
            new Services.ServicioDialogo().Informar(tituloError, ex.Message);
        }
    }

    public string NumeroZTexto => Documento.NumeroZ?.ToString() ?? string.Empty;

    /// <summary>El documento que devolvió el emisor (PDF de la imprenta digital…), si está en esta PC.</summary>
    public bool TieneDocumento => !string.IsNullOrWhiteSpace(Documento.DocumentoRuta) && System.IO.File.Exists(Documento.DocumentoRuta);

    public System.Windows.Input.ICommand AbrirDocumentoCommand => new RelayCommand(() =>
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Documento.DocumentoRuta) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Services.Registro.Advertencia(ex, "No se pudo abrir el documento {Ruta}", Documento.DocumentoRuta);
        }
    }, () => TieneDocumento);

    public string TransmisionTexto => Documento.EnContingencia
        ? Documento.FechaTransmision is { } fecha
            ? $"Emitida en contingencia, transmitida el {fecha:dd/MM/yyyy HH:mm}"
            : "Emitida en contingencia, por transmitir"
        : string.Empty;

    public string TasaTexto => Documento.TasaCambioUsada > 0 ? $"Bs. {Documento.TasaCambioUsada:N2} por US$" : string.Empty;

    public string AnulacionTexto => TextoAnulacion(Documento.FechaAnulacion, Documento.MotivoAnulacion);

    /// <summary>Las filas del desglose fiscal que tienen monto: exento, cada alícuota, IGTF.</summary>
    public IReadOnlyList<RenglonTotal> Totales =>
        [.. Services.TextosDeVenta.Totales(Documento).Select(t => new RenglonTotal(t.Concepto, t.Monto))];
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
