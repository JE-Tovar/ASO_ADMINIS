using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.Configuration;

/// <summary>
/// Las preferencias en un JSON dentro de %AppData%\ASO Adminis. Por maquina y por usuario de
/// Windows, que es el alcance que les corresponde: el tema y la escala son de quien esta
/// sentado delante, no de la organizacion, y no tendria sentido sincronizarlos por la base.
///
/// Usa System.Text.Json, que viene en el framework: el proyecto solo referencia el proveedor
/// JSON de lectura de Microsoft.Extensions.Configuration, que no sabe escribir.
/// </summary>
public sealed class AjustesStoreJson : IAjustesStore
{
    private static readonly JsonSerializerOptions _formato = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string Ruta { get; }

    public AjustesStoreJson()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ASO Adminis",
            "ajustes.json"))
    {
    }

    public AjustesStoreJson(string ruta) => Ruta = ruta;

    public AjustesApp Leer()
    {
        // Cualquier fallo cae en los valores por defecto. Se traga la excepcion a proposito:
        // esto corre en el arranque, antes del login, y ahi no hay ventana donde informar.
        try
        {
            if (!File.Exists(Ruta))
                return new AjustesApp();

            return JsonSerializer.Deserialize<AjustesApp>(File.ReadAllText(Ruta), _formato)
                   ?? new AjustesApp();
        }
        catch (Exception e) when (e is IOException
                                    or UnauthorizedAccessException
                                    or JsonException
                                    or NotSupportedException)
        {
            return new AjustesApp();
        }
    }

    public bool Guardar(AjustesApp ajustes)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Ruta)!);
            File.WriteAllText(Ruta, JsonSerializer.Serialize(ajustes, _formato));
            return true;
        }
        catch (Exception e) when (e is IOException
                                    or UnauthorizedAccessException
                                    or NotSupportedException)
        {
            return false;
        }
    }
}
