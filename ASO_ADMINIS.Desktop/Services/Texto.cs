using System.Globalization;
using System.Linq;
using System.Text;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>Comparación de texto tal como la escribe una persona: sin mayúsculas ni acentos.</summary>
public static class Texto
{
    /// <summary>"Pintura Caucho Satinado" y "pintura caucho satinádo" dan lo mismo: minúsculas, sin tildes, recortado.</summary>
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        return new string(texto.Trim().ToLowerInvariant()
            .Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());
    }
}
