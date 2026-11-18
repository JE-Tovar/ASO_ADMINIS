using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Administración · Contingencia: los talonarios de formatos de contingencia que autorizó la
/// imprenta. Sin al menos uno activo no se puede registrar ninguna factura en contingencia.
/// No hay baja (no existe <c>RangosContingencia.Eliminar</c>): un talonario usado se desactiva.
/// </summary>
public sealed class RangosContingenciaCrudViewModel(IRangoContingenciaDataSource rangos, IServicioDialogo dialogos, ISesionActual sesion)
    : CrudViewModelBase<RangoContingencia, int>(rangos, dialogos, sesion)
{
    public bool PuedeVer { get; } = sesion.Puede(Permisos.RangosContingencia.Crear) || sesion.Puede(Permisos.RangosContingencia.Editar);

    protected override string ModuloPermiso => "RangosContingencia";

    protected override bool CoincideBusqueda(RangoContingencia item, string texto) =>
        item.Imprenta.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.RangoTexto.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Autorizacion.Contains(texto, StringComparison.OrdinalIgnoreCase);

    protected override RangoContingencia CrearNuevo() => new() { Prefijo = "00-", Desde = 1, Digitos = 6 };

    protected override CrudEditorViewModelBase<RangoContingencia> CrearEditor(RangoContingencia item) =>
        new RangoContingenciaEditorViewModel(item, rangos);

    protected override bool PuedeEliminar(RangoContingencia item) => false;
}

/// <summary>Alta y edición de un talonario. Las reglas (solapes, números ya usados) están en <see cref="RangosContingencia"/>.</summary>
public sealed class RangoContingenciaEditorViewModel : CrudEditorViewModelBase<RangoContingencia>
{
    private readonly RangoContingencia _original;
    private readonly IRangoContingenciaDataSource _rangos;

    public RangoContingenciaEditorViewModel(RangoContingencia original, IRangoContingenciaDataSource rangos)
    {
        _original = original;
        _rangos = rangos;
        _imprenta = original.Imprenta;
        _imprentaRif = original.ImprentaRif;
        _autorizacion = original.Autorizacion;
        _fechaAutorizacion = original.FechaAutorizacion;
        _prefijo = original.Prefijo;
        _desde = original.Desde.ToString();
        _hasta = original.Hasta == 0 ? string.Empty : original.Hasta.ToString();
        _digitos = original.Digitos.ToString();
        _activo = original.Id == 0 || original.Activo;
    }

    public override string Titulo => _original.Id == 0 ? "Nuevo talonario de contingencia" : $"Talonario {_original.RangoTexto}";
    public override double AnchoEditor => Ancho.Estandar;

    private string _imprenta;
    public string Imprenta { get => _imprenta; set => SetProperty(ref _imprenta, value); }

    private string _imprentaRif;
    public string ImprentaRif { get => _imprentaRif; set => SetProperty(ref _imprentaRif, value); }

    private string _autorizacion;
    public string Autorizacion { get => _autorizacion; set => SetProperty(ref _autorizacion, value); }

    private DateTime? _fechaAutorizacion;
    public DateTime? FechaAutorizacion { get => _fechaAutorizacion; set => SetProperty(ref _fechaAutorizacion, value); }

    private string _prefijo;
    public string Prefijo { get => _prefijo; set { if (SetProperty(ref _prefijo, value)) OnPropertyChanged(nameof(Ejemplo)); } }

    private string _desde;
    public string Desde { get => _desde; set { if (SetProperty(ref _desde, value)) OnPropertyChanged(nameof(Ejemplo)); } }

    private string _hasta;
    public string Hasta { get => _hasta; set { if (SetProperty(ref _hasta, value)) OnPropertyChanged(nameof(Ejemplo)); } }

    private string _digitos;
    public string Digitos { get => _digitos; set { if (SetProperty(ref _digitos, value)) OnPropertyChanged(nameof(Ejemplo)); } }

    private bool _activo;
    public bool Activo { get => _activo; set => SetProperty(ref _activo, value); }

    /// <summary>Cómo quedan escritos el primero y el último, para comparar con el formato impreso.</summary>
    public string Ejemplo => Leer(out var r, out _)
        ? $"Se escribirán así: {r.RangoTexto} ({r.Total} formatos)."
        : "Complete el primero, el último y las cifras para ver cómo quedan.";

    protected override bool Validar(out string? error)
    {
        if (!Leer(out var rango, out error))
            return false;

        if (Services.Rif.Error(rango.ImprentaRif) is { } errorRif && !string.IsNullOrWhiteSpace(rango.ImprentaRif))
        {
            error = $"RIF de la imprenta: {errorRif}";
            return false;
        }

        error = RangosContingencia.Validar(rango, _rangos.GetAll(), _rangos.NumerosDeControlUsados());
        return error is null;
    }

    public override RangoContingencia ObtenerResultado()
    {
        Leer(out var rango, out _);
        return rango;
    }

    private bool Leer(out RangoContingencia rango, out string? error)
    {
        rango = _original.Clonar();
        rango.Imprenta = (Imprenta ?? string.Empty).Trim();
        rango.ImprentaRif = Services.Rif.Normalizar(ImprentaRif) ?? (ImprentaRif ?? string.Empty).Trim();
        rango.Autorizacion = (Autorizacion ?? string.Empty).Trim();
        rango.FechaAutorizacion = FechaAutorizacion?.Date;
        rango.Prefijo = (Prefijo ?? string.Empty).Trim().ToUpperInvariant();
        rango.Activo = Activo;

        if (!int.TryParse(Desde?.Trim(), out var desde) || !int.TryParse(Hasta?.Trim(), out var hasta)
            || !int.TryParse(Digitos?.Trim(), out var digitos))
        {
            error = "El primero, el último y las cifras tienen que ser números enteros.";
            return false;
        }

        rango.Desde = desde;
        rango.Hasta = hasta;
        rango.Digitos = digitos;
        error = null;
        return true;
    }
}
