namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Datos del formato de contingencia (papel de imprenta autorizada) en que se hizo una factura
/// mientras el emisor fiscal no respondía (Providencia SNAT/2026/00093). Se piden el número del
/// documento y el número de control impresos en el formato; la factura queda pendiente de
/// transmitir.
/// </summary>
public sealed class ContingenciaEditorViewModel : CrudEditorViewModelBase
{
    private readonly string error;

    /// <param name="sugerencia">El siguiente número libre del talonario, si hay; se propone ya escrito.</param>
    public ContingenciaEditorViewModel(string error, Services.RangosContingencia.Sugerencia? sugerencia = null)
    {
        this.error = error;
        _numeroControl = sugerencia?.NumeroControl ?? string.Empty;
        TalonarioTexto = sugerencia is null
            ? "No hay talonarios de contingencia con números libres. Un administrador tiene que cargar el rango autorizado en Administración · Contingencia."
            : $"Talonario {sugerencia.Rango.RangoTexto} ({sugerencia.Rango.Imprenta}): quedan {sugerencia.Quedan}. " +
              "Se propone el siguiente libre; corríjalo si usó otro formato.";
        PocosQuedan = sugerencia is null || sugerencia.PocosQuedan;
    }

    /// <summary>Qué talonario se usa y cuántos quedan.</summary>
    public string TalonarioTexto { get; }

    /// <summary>El talonario se acaba (o no hay): se resalta para que se pida otro a tiempo.</summary>
    public bool PocosQuedan { get; }

    public override string Titulo => "Emitir en contingencia";
    public override string TextoAccion => "Registrar en contingencia";

    public string Explicacion =>
        $"El emisor fiscal no respondió:\n{error}\n\n" +
        "Si hizo la factura a mano en un formato de contingencia de imprenta autorizada, copie aquí sus números. " +
        "Quedará pendiente de transmitir y se enviará al emisor con \"Transmitir pendientes\" cuando vuelva.";

    private string _numeroDocumento = string.Empty;
    public string NumeroDocumento
    {
        get => _numeroDocumento;
        set => SetProperty(ref _numeroDocumento, value);
    }

    private string _numeroControl = string.Empty;
    public string NumeroControl
    {
        get => _numeroControl;
        set => SetProperty(ref _numeroControl, value);
    }

    protected override bool Validar(out string? mensaje)
    {
        if (string.IsNullOrWhiteSpace(NumeroDocumento) || string.IsNullOrWhiteSpace(NumeroControl))
        {
            mensaje = "Indique el número de factura y el número de control impresos en el formato.";
            return false;
        }

        mensaje = null;
        return true;
    }
}
