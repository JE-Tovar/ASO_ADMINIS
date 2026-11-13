using System;
using System.Linq;
using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Cómo arma la balanza su etiqueta. Por defecto: 20 + PPPPP (producto) + WWWWW (gramos) + DV. El
/// prefijo va de 20 a 29 según la balanza.
/// </summary>
public sealed record FormatoEtiqueta(string Prefijo = "20", int DigitosProducto = 5, int DecimalesPeso = 3)
{
    public static FormatoEtiqueta De(AjustesApp ajustes) =>
        new(ajustes.EtiquetaPrefijo, ajustes.EtiquetaDigitosProducto, ajustes.EtiquetaDecimalesPeso);

    /// <summary>El de los ajustes de esta máquina, o nulo si la lectura de etiquetas está apagada.</summary>
    public static FormatoEtiqueta? Vigente() =>
        Ajustes.Actual.LeerEtiquetasBalanza ? De(Ajustes.Actual) : null;

    /// <summary>Dígitos del peso: lo que queda de los 13 tras el prefijo, el producto y el verificador.</summary>
    public int DigitosPeso => 13 - Prefijo.Length - DigitosProducto - 1;

    public bool EsValido =>
        Prefijo.Length > 0 && Prefijo.All(char.IsAsciiDigit)
        && DigitosProducto > 0 && DigitosPeso > 0
        && DecimalesPeso >= 0 && DecimalesPeso <= DigitosPeso;
}

/// <summary>
/// Lee la etiqueta EAN-13 que imprime una balanza de mostrador para lo vendido a granel: lleva el
/// código del producto en la balanza (PLU, ver <see cref="Articulo.CodigoBalanza"/>) y el peso.
///
/// Solo es etiqueta si son 13 dígitos, con el prefijo configurado y el dígito verificador EAN-13
/// correcto; cualquier otra cosa se busca como texto normal. Las etiquetas que traen el PRECIO en
/// vez del peso no se leen todavía.
/// </summary>
public static class EtiquetaBalanza
{
    public static bool Leer(string? codigo, FormatoEtiqueta formato, out string producto, out decimal peso)
    {
        producto = string.Empty;
        peso = 0m;

        var texto = (codigo ?? string.Empty).Trim();
        if (!formato.EsValido || texto.Length != 13 || !texto.All(char.IsAsciiDigit))
            return false;

        if (!texto.StartsWith(formato.Prefijo, StringComparison.Ordinal) || !DigitoVerificadorValido(texto))
            return false;

        producto = texto.Substring(formato.Prefijo.Length, formato.DigitosProducto);
        var crudo = long.Parse(texto.Substring(formato.Prefijo.Length + formato.DigitosProducto, formato.DigitosPeso));
        peso = crudo / (decimal)Math.Pow(10, formato.DecimalesPeso);
        return peso > 0;
    }

    /// <summary>Dígito verificador EAN-13: pesos 1 y 3 alternados desde la izquierda, módulo 10.</summary>
    public static bool DigitoVerificadorValido(string ean13) =>
        ean13.Length == 13 && CalcularDigito(ean13[..12]) == ean13[12] - '0';

    /// <summary>El dígito que completa 12 dígitos a un EAN-13 válido.</summary>
    public static int CalcularDigito(string doce)
    {
        var suma = 0;
        for (var i = 0; i < 12; i++)
            suma += (doce[i] - '0') * (i % 2 == 0 ? 1 : 3);
        return (10 - suma % 10) % 10;
    }

    /// <summary>"00123" y "123" son el mismo PLU: se comparan sin ceros a la izquierda.</summary>
    public static string NormalizarPlu(string? plu)
    {
        var limpio = (plu ?? string.Empty).Trim().TrimStart('0');
        return limpio.Length == 0 && !string.IsNullOrWhiteSpace(plu) ? "0" : limpio;
    }
}
