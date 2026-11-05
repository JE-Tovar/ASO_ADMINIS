namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Datos del formato de contingencia (papel de imprenta autorizada) en que se hizo una factura
/// mientras el emisor fiscal no respondía (Providencia SNAT/2026/00093). Se piden el número del
/// documento y el número de control impresos en el formato; la factura queda pendiente de
/// transmitir.
/// </summary>
public sealed class ContingenciaEditorViewModel(string error) : CrudEditorViewModelBase
{
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
