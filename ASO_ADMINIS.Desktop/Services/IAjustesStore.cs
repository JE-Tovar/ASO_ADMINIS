using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Donde se guardan las preferencias de la maquina. Detras de una interfaz como las fuentes de
/// datos, para que los ViewModels no sepan si es un archivo, el registro o una tabla.
/// </summary>
public interface IAjustesStore
{
    /// <summary>
    /// Nunca lanza: un archivo ausente, ilegible o corrupto devuelve los valores por defecto.
    /// Una preferencia rota no puede ser motivo para no poder entrar a trabajar.
    /// </summary>
    AjustesApp Leer();

    /// <returns><c>true</c> si se pudo escribir; <c>false</c> si el disco no dejo.</returns>
    bool Guardar(AjustesApp ajustes);

    /// <summary>Ruta del archivo, para poder decirsela a quien tenga que mirarlo.</summary>
    string Ruta { get; }
}
