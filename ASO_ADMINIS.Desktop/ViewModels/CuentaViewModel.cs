using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// La cuenta de quien está dentro: quién es, con qué rol y en qué organización, y el único
/// sitio de la aplicación donde puede cambiar su propia contraseña.
///
/// Hasta ahora solo un administrador podía cambiarla, desde Administración · Usuarios, así que
/// rotar la propia clave obligaba a pedírselo a otro — y de paso a decirle la nueva en voz alta.
///
/// La contraseña actual se exige aunque la sesión ya esté iniciada: un equipo desatendido es
/// justo el escenario en el que alguien cambiaría la clave ajena.
/// </summary>
public sealed class CuentaViewModel : ViewModelBase
{
    /// <summary>El mismo mínimo que exige el alta de usuarios; una sola regla para las dos vías.</summary>
    public const int LargoMinimoPassword = 8;

    private readonly IUsuarioDataSource _usuarios;
    private readonly ISesionActual _sesion;

    public CuentaViewModel(IUsuarioDataSource usuarios, ISesionActual sesion)
    {
        _usuarios = usuarios;
        _sesion = sesion;

        _recordarUsuario = Ajustes.Actual.RecordarUltimoUsuario;
    }

    public AvisoGuardado Aviso { get; } = new();

    private string _errorPassword = string.Empty;

    /// <summary>
    /// Por que no se cambio la contrasena. Va bajo los campos y no en un MessageBox del sistema:
    /// el cuadro de dialogo tapaba el formulario, no seguia el tema de la aplicacion y obligaba a
    /// leerlo y descartarlo antes de poder corregir lo que senalaba.
    /// </summary>
    public string ErrorPassword
    {
        get => _errorPassword;
        private set => SetProperty(ref _errorPassword, value);
    }

    public string NombreUsuario => _sesion.UsuarioActual?.NombreUsuario ?? "—";
    public string NombreCompleto => _sesion.UsuarioActual?.NombreCompleto ?? "—";
    public string RolTexto => _sesion.UsuarioActual?.RolTexto ?? "—";

    public string OrganizacionTexto => Ambito.Actual is { } organizacion
        ? $"{organizacion.Nombre} · {organizacion.Codigo}"
        : "—";

    public string AyudaPassword =>
        $"Al menos {LargoMinimoPassword} caracteres. Se aplica de inmediato: la próxima vez que entres usa la nueva.";

    private bool _recordarUsuario;
    public bool RecordarUsuario
    {
        get => _recordarUsuario;
        set
        {
            if (!SetProperty(ref _recordarUsuario, value))
                return;

            Ajustes.Actual.RecordarUltimoUsuario = value;

            // Al desactivarlo se borra el que ya estaba guardado: dejarlo escrito en el archivo
            // haria que la casilla dijera una cosa y el disco otra.
            if (!value)
                Ajustes.Actual.UltimoUsuario = string.Empty;

            if (Ajustes.Guardar())
                Aviso.Mostrar();
        }
    }

    /// <summary>
    /// Cambia la contraseña del usuario en sesión.
    ///
    /// Recibe las tres por parámetro y no por binding porque el <c>PasswordBox</c> de WPF no
    /// expone su contenido como propiedad enlazable — a propósito, para que no quede colgado
    /// en el árbol de bindings. Es la misma vía que usa <c>LoginView</c>.
    /// </summary>
    /// <returns><c>true</c> si se cambió, para que la vista sepa si limpiar los campos.</returns>
    public bool CambiarPassword(string actual, string nueva, string confirmacion)
    {
        ErrorPassword = string.Empty;

        if (_sesion.UsuarioActual is not { } usuario)
            return false;

        if (!Passwords.Verificar(actual, usuario.PasswordHash, usuario.PasswordSalt))
        {
            ErrorPassword = "La contraseña actual no coincide. No se cambió nada.";
            return false;
        }

        if (nueva.Length < LargoMinimoPassword)
        {
            ErrorPassword = $"La contraseña nueva debe tener al menos {LargoMinimoPassword} caracteres.";
            return false;
        }

        if (nueva != confirmacion)
        {
            ErrorPassword = "La contraseña nueva y su confirmación no son iguales.";
            return false;
        }

        if (nueva == actual)
        {
            ErrorPassword = "La contraseña nueva es igual a la actual. Elige otra.";
            return false;
        }

        var (hash, salt) = Passwords.Crear(nueva);

        var copia = usuario.Clonar();
        copia.PasswordHash = hash;
        copia.PasswordSalt = salt;

        try
        {
            _usuarios.Update(copia);
        }
        catch (Exception ex)
        {
            ErrorPassword = ex.Message;
            return false;
        }

        // La sesión guarda su propia copia del usuario y de ella sale la verificación de la
        // próxima vez: sin esto, cambiar dos veces seguidas fallaría en la segunda.
        usuario.PasswordHash = hash;
        usuario.PasswordSalt = salt;

        ErrorPassword = string.Empty;
        Aviso.Mostrar("Contraseña cambiada. La próxima vez que entres, usa la nueva.");
        return true;
    }
}
