using System.IO;
using System.Text.Json;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Diario local de lo que ya ocurrió en el emisor fiscal pero no se pudo guardar en la base
/// (%AppData%\ASO Adminis\diario-fiscal.jsonl): un documento emitido o un reporte Z hecho. Para
/// el fisco ya existen, así que hay que conciliarlos a mano; perderlos no es una opción.
/// </summary>
public static class DiarioFiscal
{
    public const string RutaTexto = "%AppData%\\ASO Adminis\\diario-fiscal.jsonl";

    public static void Anotar(string tipo, object datos, Exception ex)
    {
        try
        {
            var carpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ASO Adminis");
            Directory.CreateDirectory(carpeta);
            var linea = JsonSerializer.Serialize(new { Momento = DateTime.Now, Tipo = tipo, Error = ex.Message, Datos = datos });
            File.AppendAllText(Path.Combine(carpeta, "diario-fiscal.jsonl"), linea + Environment.NewLine);
        }
        catch (Exception)
        {
            // El aviso al usuario sale igual; perder el diario no debe tapar el error original.
        }
    }
}
