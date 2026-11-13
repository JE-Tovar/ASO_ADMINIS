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
        _leerEtiquetas = Ajustes.Actual.LeerEtiquetasBalanza;
        _etiquetaPrefijo = Ajustes.Actual.EtiquetaPrefijo;
        _digitosProducto = Ajustes.Actual.EtiquetaDigitosProducto.ToString();
        _decimalesPeso = Ajustes.Actual.EtiquetaDecimalesPeso.ToString();
    }

    // ---------------------------------------------------------------- Etiquetas de balanza

    private bool _leerEtiquetas;
    /// <summary>Si el buscador de artículos reconoce las etiquetas EAN-13 que imprime la balanza.</summary>
    public bool LeerEtiquetasBalanza
    {
        get => _leerEtiquetas;
        set
        {
            if (!SetProperty(ref _leerEtiquetas, value))
                return;
            Ajustes.Actual.LeerEtiquetasBalanza = value;
            GuardarEtiqueta();
        }
    }

    private string _etiquetaPrefijo;
    public string EtiquetaPrefijo
    {
        get => _etiquetaPrefijo;
        set
        {
            var limpio = (value ?? string.Empty).Trim();
            if (!SetProperty(ref _etiquetaPrefijo, limpio))
                return;
            Ajustes.Actual.EtiquetaPrefijo = limpio;
            GuardarEtiqueta();
        }
    }

    private string _digitosProducto;
    public string EtiquetaDigitosProducto
    {
        get => _digitosProducto;
        set
        {
            if (!SetProperty(ref _digitosProducto, value ?? string.Empty))
                return;
            if (int.TryParse(_digitosProducto, out var n))
                Ajustes.Actual.EtiquetaDigitosProducto = n;
            GuardarEtiqueta();
        }
    }

    private string _decimalesPeso;
    public string EtiquetaDecimalesPeso
    {
        get => _decimalesPeso;
        set
        {
            if (!SetProperty(ref _decimalesPeso, value ?? string.Empty))
                return;
            if (int.TryParse(_decimalesPeso, out var n))
                Ajustes.Actual.EtiquetaDecimalesPeso = n;
            GuardarEtiqueta();
        }
    }

    /// <summary>"2 · 5 dígitos de producto · 5 de peso (3 decimales) · verificador", o por qué no sirve.</summary>
    public string FormatoEtiquetaTexto
    {
        get
        {
            var f = FormatoEtiqueta.De(Ajustes.Actual);
            return f.EsValido
                ? $"Lee: {f.Prefijo} + {f.DigitosProducto} dígitos de producto + {f.DigitosPeso} de peso ({f.DecimalesPeso} decimales) + dígito verificador = 13."
                : "Ese formato no cabe en 13 dígitos: revise el prefijo, los dígitos de producto y los decimales.";
        }
    }

    private void GuardarEtiqueta()
    {
        OnPropertyChanged(nameof(FormatoEtiquetaTexto));
        if (Ajustes.Guardar())
            Aviso.Mostrar();
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
