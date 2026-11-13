using System.Collections.ObjectModel;
using System.Windows.Input;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Nota de crédito o de débito sobre una factura emitida.
///
/// - <b>Crédito</b>: se elige cuánto se devuelve de cada renglón de la factura (hasta lo que
///   queda por devolver, descontadas las notas previas), a los mismos precios y alícuotas.
/// - <b>Débito</b>: cargos adicionales como conceptos libres con su alícuota.
///
/// El editor solo recoge los datos; la emisión y sus reglas son de <see cref="VentasService"/>.
/// </summary>
public sealed class NotaFiscalEditorViewModel : CrudEditorViewModelBase
{
    private readonly Venta _factura;

    public NotaFiscalEditorViewModel(Venta factura, TipoDocumentoVenta tipo,
                                     IReadOnlyDictionary<(int? ArticuloId, string Presentacion), decimal>? disponible)
    {
        _factura = factura;
        Tipo = tipo;

        if (EsCredito)
        {
            // El disponible es por artículo + presentación; si la factura tiene dos renglones
            // iguales, el primero se queda con lo que haya y el segundo con el resto.
            var resto = disponible?.ToDictionary(k => k.Key, k => k.Value) ?? [];
            foreach (var linea in factura.Lineas)
            {
                var clave = (linea.ArticuloId, linea.PresentacionNombre);
                var queda = Math.Min(linea.Cantidad, resto.GetValueOrDefault(clave));
                resto[clave] = resto.GetValueOrDefault(clave) - queda;
                Devoluciones.Add(new DevolucionEditorViewModel(linea, queda, Recalcular));
            }
        }
        else
        {
            AgregarCargoCommand = new RelayCommand(() => Cargos.Add(new CargoEditorViewModel(Recalcular)));
            QuitarCargoCommand = new RelayCommand<CargoEditorViewModel>(c => { if (c is not null) Cargos.Remove(c); Recalcular(); });
            Cargos.Add(new CargoEditorViewModel(Recalcular));
        }

        DevolverTodoCommand = new RelayCommand(() =>
        {
            foreach (var d in Devoluciones)
                d.Cantidad = d.Disponible.ToString("0.###");
        });
    }

    public TipoDocumentoVenta Tipo { get; }
    public bool EsCredito => Tipo == TipoDocumentoVenta.NotaCredito;
    public bool EsDebito => !EsCredito;

    public override string Titulo => $"{Venta.TextoDe(Tipo)} sobre la factura {_factura.NumeroFiscal}";
    public override string TextoAccion => $"Emitir {Venta.TextoDe(Tipo).ToLowerInvariant()}";
    public override double AnchoEditor => Ancho.Amplio;

    /// <summary>Documento fiscal con grilla: abre maximizada, con las líneas a todo el alto.</summary>
    public override bool AbreMaximizado => true;

    public string FacturaTexto =>
        $"Factura {_factura.NumeroFiscal} (control {_factura.NumeroControl}) del {_factura.FechaHoraTexto} · " +
        $"{_factura.ClienteTexto} · Total Bs. {_factura.TotalTexto}";

    public ObservableCollection<DevolucionEditorViewModel> Devoluciones { get; } = [];
    public ObservableCollection<CargoEditorViewModel> Cargos { get; } = [];

    public ICommand DevolverTodoCommand { get; }
    public ICommand? AgregarCargoCommand { get; }
    public ICommand? QuitarCargoCommand { get; }

    public IReadOnlyList<OpcionAlicuota> OpcionesAlicuota { get; } = OpcionAlicuota.Todas;

    private string _motivo = string.Empty;
    public string Motivo
    {
        get => _motivo;
        set => SetProperty(ref _motivo, value);
    }

    /// <summary>Total aproximado (sin IGTF); el definitivo lo calcula el servicio al emitir.</summary>
    public string TotalTexto
    {
        get
        {
            var total = EsCredito
                ? Devoluciones.Sum(d => d.MontoConIva)
                : Cargos.Sum(c => c.MontoValor);
            return EsCredito
                ? $"Se acreditan ≈ Bs. {total:N2} (IVA incluido)"
                : $"Cargos: Bs. {total:N2} + IVA";
        }
    }

    private void Recalcular() => OnPropertyChanged(nameof(TotalTexto));

    public IReadOnlyList<(VentaLinea LineaFactura, decimal Cantidad)> DevolucionesElegidas =>
        [.. Devoluciones.Where(d => d.CantidadValor > 0).Select(d => (d.Linea, d.CantidadValor))];

    public IReadOnlyList<(string Descripcion, decimal MontoBs, TipoAlicuota Alicuota)> CargosElegidos =>
        [.. Cargos.Where(c => !string.IsNullOrWhiteSpace(c.Descripcion) || c.MontoValor != 0)
                  .Select(c => (c.Descripcion.Trim(), c.MontoValor, c.Alicuota))];

    protected override bool Validar(out string? error)
    {
        if (string.IsNullOrWhiteSpace(Motivo))
        {
            error = "Indique el motivo de la nota.";
            return false;
        }

        if (EsCredito)
        {
            if (Devoluciones.FirstOrDefault(d => !d.CantidadEsValida) is { } mala)
            {
                error = $"Revise la cantidad a devolver de {mala.Linea.Descripcion}.";
                return false;
            }

            if (Devoluciones.FirstOrDefault(d => d.CantidadValor > d.Disponible) is { } excedida)
            {
                error = $"De {excedida.Linea.Descripcion} solo quedan {excedida.Disponible:#,0.###} por devolver.";
                return false;
            }

            if (DevolucionesElegidas.Count == 0)
            {
                error = "Indique qué se devuelve.";
                return false;
            }
        }
        else if (CargosElegidos.Count == 0 || Cargos.Any(c => !string.IsNullOrWhiteSpace(c.Monto) && c.MontoValor <= 0))
        {
            error = "Agregue al menos un cargo con descripción y monto mayor que cero.";
            return false;
        }

        error = null;
        return true;
    }
}

/// <summary>Un renglón de la factura con cuánto se devuelve.</summary>
public sealed class DevolucionEditorViewModel(VentaLinea linea, decimal disponible, Action alCambiar) : ViewModelBase
{
    public VentaLinea Linea { get; } = linea;
    public decimal Disponible { get; } = disponible;

    public string FacturadoTexto => Linea.CantidadTexto;
    public string DisponibleTexto => Disponible.ToString("#,0.###");

    private string _cantidad = string.Empty;
    public string Cantidad
    {
        get => _cantidad;
        set
        {
            if (!SetProperty(ref _cantidad, value))
                return;
            OnPropertyChanged(nameof(MontoTexto));
            alCambiar();
        }
    }

    public bool CantidadEsValida => string.IsNullOrWhiteSpace(Cantidad) || (decimal.TryParse(Cantidad, out var v) && v >= 0);
    public decimal CantidadValor => decimal.TryParse(Cantidad, out var v) ? v : 0m;

    public decimal MontoConIva
    {
        get
        {
            var monto = CalculoFiscal.Redondear(CantidadValor * Linea.PrecioUnitario);
            return monto + CalculoFiscal.Redondear(monto * Linea.PorcentajeIva / 100m);
        }
    }

    public string MontoTexto => CantidadValor > 0 ? CalculoFiscal.Redondear(CantidadValor * Linea.PrecioUnitario).ToString("N2") : string.Empty;
}

/// <summary>Un cargo de una nota de débito.</summary>
public sealed class CargoEditorViewModel(Action alCambiar) : ViewModelBase
{
    private string _descripcion = string.Empty;
    public string Descripcion
    {
        get => _descripcion;
        set => SetProperty(ref _descripcion, value);
    }

    private string _monto = string.Empty;
    public string Monto
    {
        get => _monto;
        set { if (SetProperty(ref _monto, value)) alCambiar(); }
    }

    private TipoAlicuota _alicuota = TipoAlicuota.General;
    public TipoAlicuota Alicuota
    {
        get => _alicuota;
        set => SetProperty(ref _alicuota, value);
    }

    public decimal MontoValor => decimal.TryParse(Monto, out var v) ? v : 0m;
}
