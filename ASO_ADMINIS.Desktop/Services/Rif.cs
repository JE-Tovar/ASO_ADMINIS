using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Identificación fiscal venezolana: RIF (con dígito verificador) o cédula de identidad.
///
/// El dígito verificador del RIF es un módulo 11: el valor de la letra (V=1, E=2, J=3, P=4,
/// G=5) por 4, más los 8 dígitos por 3,2,7,6,5,4,3,2; DV = 11 − (suma mod 11), y si da 10 u 11
/// el DV es 0. Comprobado con RIF públicos: G-20000303-0 (SENIAT) y J-00002961-0.
///
/// Una cédula (V o E seguida de hasta 8 dígitos, sin DV) también se acepta: el consumidor
/// final se identifica con su C.I., y no todas las personas naturales tienen RIF.
/// </summary>
public static partial class Rif
{
    private static readonly int[] Pesos = [3, 2, 7, 6, 5, 4, 3, 2];

    [GeneratedRegex(@"^([VEJPG])-?(\d{1,9})-?(\d)?$")]
    private static partial Regex Patron();

    /// <summary>
    /// Normaliza lo que se tecleó ("j123456789", "V 12.345.678") a la forma canónica:
    /// "J-12345678-9" para un RIF, "V-12345678" para una cédula. Devuelve null si no se
    /// reconoce la forma; NO comprueba el dígito (eso es <see cref="EsValido"/>).
    /// </summary>
    public static string? Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return null;

        var limpio = new string(texto.ToUpperInvariant().Where(c => char.IsLetterOrDigit(c)).ToArray());
        if (limpio.Length < 2 || !"VEJPG".Contains(limpio[0]) || !limpio[1..].All(char.IsDigit))
            return null;

        var letra = limpio[0];
        var digitos = limpio[1..];

        // 9 dígitos = 8 de número + DV. Menos de 9 = cédula (solo V/E) sin DV.
        if (digitos.Length == 9)
            return $"{letra}-{digitos[..8]}-{digitos[8]}";

        if (digitos.Length <= 8 && (letra == 'V' || letra == 'E'))
            return $"{letra}-{digitos.TrimStart('0').PadLeft(1, '0')}";

        return null;
    }

    /// <summary>True si es un RIF completo (con DV) cuyo dígito verificador cuadra.</summary>
    public static bool EsRifValido(string? texto)
    {
        var normal = Normalizar(texto);
        if (normal is null)
            return false;

        var m = Patron().Match(normal);
        if (!m.Success || !m.Groups[3].Success || m.Groups[2].Value.Length != 8)
            return false;

        return DigitoVerificador(normal[0], m.Groups[2].Value) == m.Groups[3].Value[0] - '0';
    }

    /// <summary>True si es una cédula (V/E sin DV).</summary>
    public static bool EsCedula(string? texto) =>
        Normalizar(texto) is { } normal && normal.Count(c => c == '-') == 1;

    /// <summary>RIF válido o cédula: lo que se acepta como identificación de un cliente.</summary>
    public static bool EsValido(string? texto) => EsRifValido(texto) || EsCedula(texto);

    /// <summary>Mensaje de error para mostrar, o null si la identificación es válida.</summary>
    public static string? Error(string? texto, bool exigirRif = false)
    {
        if (Normalizar(texto) is null)
            return "Escriba el RIF como J-12345678-9 (letras V, E, J, P o G), o la cédula como V-12345678.";

        if (EsCedula(texto))
            return exigirRif ? "Se necesita un RIF completo (con dígito verificador), no una cédula." : null;

        return EsRifValido(texto) ? null : $"El RIF {Normalizar(texto)} no es válido: el dígito verificador no cuadra.";
    }

    public static int DigitoVerificador(char letra, string ochoDigitos)
    {
        var valorLetra = letra switch
        {
            'V' => 1,
            'E' => 2,
            'J' => 3,
            'P' => 4,
            'G' => 5,
            _ => throw new ArgumentException($"Letra de RIF no reconocida: {letra}")
        };

        var suma = valorLetra * 4 + ochoDigitos.Select((c, i) => (c - '0') * Pesos[i]).Sum();
        var dv = 11 - suma % 11;
        return dv >= 10 ? 0 : dv;
    }
}
