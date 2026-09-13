using System.Security.Cryptography;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Hash de contrasennas con PBKDF2-SHA256, salt por usuario y comparacion en tiempo constante.
/// Usa solo System.Security.Cryptography: no agrega dependencias al proyecto.
/// </summary>
public static class Passwords
{
    private const int BytesSalt = 16;
    private const int BytesHash = 32;

    /// <summary>Iteraciones segun la recomendacion de OWASP para PBKDF2-SHA256.</summary>
    private const int Iteraciones = 210_000;

    /// <summary>Genera salt nuevo y devuelve ambos en Base64, listos para guardar.</summary>
    public static (string Hash, string Salt) Crear(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(BytesSalt);
        var hash = Derivar(password, salt);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    /// <summary>
    /// Comprueba la contrasenna contra el hash guardado. Devuelve false ante datos corruptos
    /// o vacios en vez de lanzar: un registro mal formado es un login fallido, no un cierre.
    /// </summary>
    public static bool Verificar(string password, string hashBase64, string saltBase64)
    {
        if (string.IsNullOrEmpty(hashBase64) || string.IsNullOrEmpty(saltBase64))
            return false;

        byte[] esperado, salt;
        try
        {
            esperado = Convert.FromBase64String(hashBase64);
            salt = Convert.FromBase64String(saltBase64);
        }
        catch (FormatException)
        {
            return false;
        }

        var calculado = Derivar(password, salt);
        return CryptographicOperations.FixedTimeEquals(calculado, esperado);
    }

    private static byte[] Derivar(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, Iteraciones, HashAlgorithmName.SHA256, BytesHash);
}
