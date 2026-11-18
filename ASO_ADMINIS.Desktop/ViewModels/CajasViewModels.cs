using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Padrón de cajas (Administración · Cajas). No hay baja: una caja con historia se desactiva,
/// y no existe el permiso <c>Cajas.Eliminar</c>, así que el comando base queda apagado solo.
/// </summary>
public sealed class CajasCrudViewModel(ICajaDataSource cajas, IServicioDialogo dialogos, ISesionActual sesion)
    : CrudViewModelBase<Caja, int>(cajas, dialogos, sesion)
{
    protected override string ModuloPermiso => "Cajas";

    protected override bool CoincideBusqueda(Caja item, string texto) =>
        item.Codigo.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase);

    protected override Caja CrearNuevo() => new();

    protected override CrudEditorViewModelBase<Caja> CrearEditor(Caja item) =>
        new CajaEditorViewModel(item, cajas);

    protected override bool PuedeEliminar(Caja item) => false;
}

/// <summary>
/// Alta y edición de una caja. El código solo se escribe al crearla: ventas, turnos y cierres Z
/// guardan una copia, y cambiarlo después los dejaría apuntando a un nombre que ya no existe.
/// </summary>
public sealed class CajaEditorViewModel : CrudEditorViewModelBase<Caja>
{
    private readonly Caja _original;
    private readonly ICajaDataSource _cajas;

    public CajaEditorViewModel(Caja original, ICajaDataSource cajas)
    {
        _original = original;
        _cajas = cajas;
        _codigo = original.Codigo;
        _nombre = original.Nombre;
        _activa = original.Id == 0 || original.Activa;
    }

    public override string Titulo => EsNueva ? "Nueva caja" : $"Caja {_original.Codigo}";

    public bool EsNueva => _original.Id == 0;

    private string _codigo;
    public string Codigo
    {
        get => _codigo;
        set => SetProperty(ref _codigo, value);
    }

    private string _nombre;
    public string Nombre
    {
        get => _nombre;
        set => SetProperty(ref _nombre, value);
    }

    private bool _activa;
    public bool Activa
    {
        get => _activa;
        set => SetProperty(ref _activa, value);
    }

    protected override bool Validar(out string? error)
    {
        var codigo = Normalizar(Codigo);
        if (codigo.Length == 0)
        {
            error = "Indique el código de la caja (p. ej. CAJA-01).";
            return false;
        }

        if (codigo.Length > 20 || codigo.Contains(' '))
        {
            error = "El código no puede llevar espacios ni pasar de 20 caracteres.";
            return false;
        }

        if (EsNueva && _cajas.GetAll().Any(c => string.Equals(c.Codigo, codigo, StringComparison.OrdinalIgnoreCase)))
        {
            error = $"Ya existe una caja {codigo}.";
            return false;
        }

        error = null;
        return true;
    }

    public override Caja ObtenerResultado()
    {
        var caja = _original.Clonar();
        if (EsNueva)
            caja.Codigo = Normalizar(Codigo);
        caja.Nombre = (Nombre ?? string.Empty).Trim();
        caja.Activa = Activa;
        return caja;
    }

    private static string Normalizar(string? codigo) => (codigo ?? string.Empty).Trim().ToUpperInvariant();
}
