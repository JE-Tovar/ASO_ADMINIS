namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Editor genérico de "decir por qué": anular un documento, forzar una excepción, rechazar
/// una aprobación. Toda decisión sobre un documento queda registrada con su comentario
/// (capa de auditoría del diseño de autorización), y siempre se pide igual, así que se pide
/// con un solo editor en vez de uno por documento.
/// </summary>
public sealed class MotivoEditorViewModel : CrudEditorViewModelBase
{
    private readonly string _titulo;
    private readonly string _mensajeFaltante;

    /// <param name="titulo">Encabezado de la ventana, p. ej. "Anular salida Nº 4".</param>
    /// <param name="descripcion">Resumen del documento afectado, para que se vea qué se está firmando.</param>
    /// <param name="etiquetaMotivo">Rótulo del campo, p. ej. "Motivo de la anulación".</param>
    /// <param name="mensajeFaltante">Error a mostrar si se deja vacío.</param>
    public MotivoEditorViewModel(string titulo,
                                 string descripcion,
                                 string etiquetaMotivo = "Motivo",
                                 string mensajeFaltante = "Indique el motivo.")
    {
        _titulo = titulo;
        _mensajeFaltante = mensajeFaltante;
        Descripcion = descripcion;
        EtiquetaMotivo = etiquetaMotivo;
    }

    public override string Titulo => _titulo;

    public string Descripcion { get; }
    public string EtiquetaMotivo { get; }

    private string _motivo = string.Empty;
    public string Motivo
    {
        get => _motivo;
        set => SetProperty(ref _motivo, value);
    }

    protected override bool Validar(out string? error)
    {
        if (string.IsNullOrWhiteSpace(Motivo))
        {
            error = _mensajeFaltante;
            return false;
        }

        error = null;
        return true;
    }
}
