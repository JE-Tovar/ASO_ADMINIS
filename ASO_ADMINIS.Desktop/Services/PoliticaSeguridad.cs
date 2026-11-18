namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Las reglas de seguridad de las cuentas, en un solo sitio. Antes el largo mínimo de la
/// contraseña estaba escrito en cuatro pantallas distintas.
/// </summary>
public static class PoliticaSeguridad
{
    /// <summary>Intentos fallidos seguidos que bloquean la cuenta.</summary>
    public const int IntentosAntesDeBloquear = 5;

    /// <summary>Cuánto dura el bloqueo. Un administrador puede levantarlo antes.</summary>
    public static readonly TimeSpan DuracionBloqueo = TimeSpan.FromMinutes(15);

    public const int LargoMinimoPassword = 8;

    /// <summary>Minutos sin uso antes de pedir la contraseña de nuevo, si la organización no dice otra cosa.</summary>
    public const int MinutosInactividadPorDefecto = 15;

    /// <summary>Cada cuánto se revisa si cambiaron el rol, los permisos o el estado del usuario en sesión.</summary>
    public static readonly TimeSpan IntervaloRefrescoPermisos = TimeSpan.FromSeconds(60);

    public static string DescripcionPassword =>
        $"Al menos {LargoMinimoPassword} caracteres, con letras y números, y distinta del nombre de usuario.";

    /// <returns>El problema de la contraseña, o null si cumple.</returns>
    public static string? ErrorPassword(string? password, string? nombreUsuario = null)
    {
        password ??= string.Empty;

        if (password.Length < LargoMinimoPassword)
            return $"La contraseña debe tener al menos {LargoMinimoPassword} caracteres.";

        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            return "La contraseña debe combinar letras y números.";

        if (!string.IsNullOrWhiteSpace(nombreUsuario)
            && password.Contains(nombreUsuario.Trim(), StringComparison.OrdinalIgnoreCase))
            return "La contraseña no puede contener el nombre de usuario.";

        return null;
    }
}
