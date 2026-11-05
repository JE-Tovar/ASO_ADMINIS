using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Preferencias de cómo se comporta la aplicación en esta máquina, para todos los que la usen
/// aquí.
///
/// <b>Todo se guarda al cambiarlo</b>, sin botón de guardar aparte.
///
/// PROVISIONAL: hoy es una sola preferencia, sin permiso propio detrás. Un ajuste que decida
/// algo del negocio (como el umbral de alerta de consumo que tenía ASO) debería ir detrás de
/// un permiso — ver "Configuración y preferencias" en CLAUDE.md.
/// </summary>
public sealed class PreferenciasAppViewModel : ViewModelBase
{
    public PreferenciasAppViewModel()
    {
        _abrirEnUltimaSeccion = Ajustes.Actual.AbrirEnUltimaSeccion;
        _cajaCodigo = Ajustes.Actual.CajaCodigo;
    }

    private string _cajaCodigo;

    /// <summary>
    /// Caja o terminal de venta de esta máquina. Figura en cada documento fiscal emitido desde
    /// aquí; sin ella Ventas no deja facturar. Se guarda al salir del campo.
    /// </summary>
    public string CajaCodigo
    {
        get => _cajaCodigo;
        set
        {
            var limpio = (value ?? string.Empty).Trim().ToUpperInvariant();
            if (!SetProperty(ref _cajaCodigo, limpio))
                return;

            Ajustes.Actual.CajaCodigo = limpio;
            if (Ajustes.Guardar())
                Aviso.Mostrar();
        }
    }

    public AvisoGuardado Aviso { get; } = new();

    private bool _abrirEnUltimaSeccion;
    public bool AbrirEnUltimaSeccion
    {
        get => _abrirEnUltimaSeccion;
        set
        {
            if (!SetProperty(ref _abrirEnUltimaSeccion, value))
                return;

            Ajustes.Actual.AbrirEnUltimaSeccion = value;
            if (Ajustes.Guardar())
                Aviso.Mostrar();
        }
    }
}
