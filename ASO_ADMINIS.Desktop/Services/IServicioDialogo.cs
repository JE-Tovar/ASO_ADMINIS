using ASO_ADMINIS.Desktop.ViewModels;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Presentación de diálogos modales (editor CRUD, confirmaciones). Mantiene las
/// ViewModels libres de referencias directas a <c>Window</c>/<c>MessageBox</c>.
/// </summary>
public interface IServicioDialogo
{
    /// <returns><c>true</c> si el usuario guardó los cambios.</returns>
    bool MostrarEditor(CrudEditorViewModelBase editor);

    bool Confirmar(string titulo, string mensaje);

    /// <summary>Aviso sin decisión: reglas de negocio rechazadas, resultados de una acción.</summary>
    void Informar(string titulo, string mensaje);
}
