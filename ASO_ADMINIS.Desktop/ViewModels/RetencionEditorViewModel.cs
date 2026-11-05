using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Registro del comprobante de retención de IVA que entregó el cliente por una factura. Las
/// reglas (14 dígitos, 75/100 %, una por factura, comprobante no repetido) las aplica
/// <see cref="RetencionesService"/> al registrar.
/// </summary>
public sealed class RetencionEditorViewModel : CrudEditorViewModelBase
{
    private readonly Venta _factura;

    public RetencionEditorViewModel(Venta factura)
    {
        _factura = factura;
        _fecha = DateTime.Today;
        _porcentaje = 75m;
    }

    public override string Titulo => $"Retención de IVA · factura {_factura.NumeroFiscal}";
    public override string TextoAccion => "Registrar retención";

    public string FacturaTexto =>
        $"{_factura.ClienteRif} · {_factura.ClienteNombre}\nFactura {_factura.NumeroFiscal} del {_factura.FechaTexto} · IVA Bs. {_factura.TotalIva:N2}";

    public IReadOnlyList<decimal> Porcentajes { get; } = RetencionesService.PorcentajesValidos;

    private decimal _porcentaje;
    public decimal Porcentaje
    {
        get => _porcentaje;
        set
        {
            if (SetProperty(ref _porcentaje, value))
                OnPropertyChanged(nameof(MontoTexto));
        }
    }

    public string MontoTexto => $"Retenido: Bs. {RetencionesService.Calcular(_factura, Porcentaje):N2}";

    private string _numeroComprobante = string.Empty;
    public string NumeroComprobante
    {
        get => _numeroComprobante;
        set => SetProperty(ref _numeroComprobante, value);
    }

    private DateTime _fecha;
    public DateTime Fecha
    {
        get => _fecha;
        set => SetProperty(ref _fecha, value);
    }

    protected override bool Validar(out string? error)
    {
        if (new string(NumeroComprobante.Where(char.IsDigit).ToArray()).Length != 14)
        {
            error = "El número de comprobante tiene 14 dígitos (AAAAMM + correlativo de 8).";
            return false;
        }

        error = null;
        return true;
    }
}
