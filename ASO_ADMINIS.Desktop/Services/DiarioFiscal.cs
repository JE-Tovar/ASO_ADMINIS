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

    /// <summary>
    /// Dónde se escribe. Las pruebas lo apuntan a una carpeta temporal: un diario fiscal con
    /// documentos de prueba en la máquina real haría conciliar a mano ventas que nunca existieron.
    /// </summary>
    internal static string Carpeta { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ASO Adminis");

    public static void Anotar(string tipo, object datos, Exception ex)
    {
        // También al registro técnico: es lo primero que se mira en soporte, y el diario puede
        // no estar a mano (otra carpeta, otro usuario de Windows).
        Registro.Error(ex, "Emitido en el fisco pero no guardado ({Tipo}); anotado en el diario fiscal", tipo);

        try
        {
            var carpeta = Carpeta;
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
