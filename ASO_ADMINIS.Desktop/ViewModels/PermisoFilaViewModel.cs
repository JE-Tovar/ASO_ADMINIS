using System.Linq;
using System.Text.RegularExpressions;
using ASO_ADMINIS.Desktop.Navigation;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Un permiso del catálogo, visto desde un usuario concreto: si su rol se lo da, si hay un ajuste
/// que lo cambia, y si quien está editando puede tocarlo.
///
/// La etiqueta se DERIVA de la cadena del permiso, no sale de una tabla de nombres: una tabla con
/// los 87 se desincronizaría al primer permiso nuevo, y el fallo sería silencioso (una fila sin
/// nombre, o peor, con el nombre de otra cosa).
/// </summary>
public sealed class PermisoFilaViewModel : ViewModelBase
{
    /// <summary>Los "Ver.*" van juntos al final: son de navegación, no de acción.</summary>
    public const string GrupoNavegacion = "Navegación";

    private readonly bool _concedidoOriginal;

    public PermisoFilaViewModel(string permiso, bool baseDelRol, bool concedido, bool editable, string? motivoBloqueo)
    {
        Permiso = permiso;
        BaseDelRol = baseDelRol;
        Editable = editable;
        MotivoBloqueo = motivoBloqueo;

        _concedido = concedido;
        _concedidoOriginal = concedido;

        var esNavegacion = permiso.StartsWith(Permisos.PrefijoVer);
        Grupo = esNavegacion ? GrupoNavegacion : permiso.Split('.')[0];
        OrdenGrupo = esNavegacion ? 1 : 0;
        Etiqueta = esNavegacion ? EtiquetaDeNavegacion(permiso) : Separar(permiso[(permiso.IndexOf('.') + 1)..]);
    }

    public string Permiso { get; }
    public string Grupo { get; }
    public int OrdenGrupo { get; }
    public string Etiqueta { get; }

    /// <summary>¿El rol del usuario ya trae este permiso sin necesidad de ajuste?</summary>
    public bool BaseDelRol { get; }

    public bool Editable { get; }

    /// <summary>Por qué no se puede tocar, o null si se puede. Se muestra como tooltip.</summary>
    public string? MotivoBloqueo { get; }

    private bool _concedido;
    public bool Concedido
    {
        get => _concedido;
        set
        {
            if (SetProperty(ref _concedido, value))
                OnPropertyChanged(nameof(OrigenTexto));
        }
    }

    /// <summary>De dónde sale el estado actual: del rol, o de un ajuste que lo aparta.</summary>
    public string OrigenTexto => Concedido == BaseDelRol
        ? (BaseDelRol ? "del rol" : string.Empty)
        : (Concedido ? "concedido" : "revocado");

    public bool Cambio => Concedido != _concedidoOriginal;

    /// <summary>¿Hace falta guardar un ajuste, o basta con lo que ya da el rol?</summary>
    public bool NecesitaAjuste => Concedido != BaseDelRol;

    /// <summary>"CambiarEstado" → "Cambiar estado". Parte por las mayúsculas internas.</summary>
    private static string Separar(string accion)
    {
        var partes = Regex.Split(accion, @"(?<!^)(?=[A-Z])");
        return string.Join(' ', partes.Select((p, i) => i == 0 ? p : p.ToLowerInvariant()));
    }

    /// <summary>
    /// "Ver.Operaciones.Registro" → "Operaciones · Registro", leyendo el catálogo de navegación
    /// en vez de reconstruirlo a mano: es la misma fuente única que usa el resto del shell.
    /// </summary>
    private static string EtiquetaDeNavegacion(string permiso)
    {
        var clave = permiso[Permisos.PrefijoVer.Length..];

        foreach (var modulo in ModuloCatalogo.Modulos)
            foreach (var submodulo in modulo.Submodulos)
                if (submodulo.Clave == clave)
                    return $"{modulo.Nombre} · {submodulo.Nombre}";

        return ModuloCatalogo.BuscarModulo(clave)?.Nombre ?? clave;
    }
}
