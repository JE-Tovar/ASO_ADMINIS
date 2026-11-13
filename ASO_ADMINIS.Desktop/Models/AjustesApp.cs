namespace ASO_ADMINIS.Desktop.Models;

/// <summary>Tema visual de la aplicacion. La paleta de cada uno vive en <c>Services/Tema.cs</c>.</summary>
public enum TemaApp
{
    Claro,
    Oscuro
}

/// <summary>
/// Preferencias de quien usa la aplicacion en ESTA maquina. No son datos del negocio y no
/// pertenecen a ninguna organizacion: viven en %AppData%\ASO Adminis\ajustes.json (ver
/// <c>Configuration/AjustesStoreJson.cs</c>), fuera de la base y fuera del repositorio.
///
/// Es un POCO plano a proposito: lo serializa System.Text.Json sin configuracion, y un campo
/// nuevo se agrega aqui y ya. Los archivos viejos siguen leyendose porque lo que falta toma el
/// valor por defecto de su propiedad.
/// </summary>
public class AjustesApp
{
    public TemaApp Tema { get; set; } = TemaApp.Claro;

    /// <summary>Factor de escala de toda la ventana (1,0 = 100 %). Ver <c>MainWindow.xaml</c>.</summary>
    public double EscalaInterfaz { get; set; } = 1.0;

    public bool RecordarUltimoUsuario { get; set; } = true;

    /// <summary>Solo el nombre de usuario, nunca la contrasenna.</summary>
    public string UltimoUsuario { get; set; } = string.Empty;

    /// <summary>Si no, la aplicacion abre siempre en Inicio.</summary>
    public bool AbrirEnUltimaSeccion { get; set; }

    /// <summary>Clave del modulo que se estaba viendo al cerrar.</summary>
    public string UltimaSeccion { get; set; } = string.Empty;

    /// <summary>
    /// Caja o terminal de venta de esta máquina ("CAJA-01"). Figura en cada documento fiscal
    /// que se emite desde aquí; sin ella no se puede facturar.
    /// </summary>
    public string CajaCodigo { get; set; } = string.Empty;

    // ---- Etiquetas de balanza (EAN-13 con peso), ver Services/EtiquetaBalanza.cs ----

    /// <summary>Si el buscador de artículos reconoce las etiquetas que imprime la balanza.</summary>
    public bool LeerEtiquetasBalanza { get; set; } = true;

    /// <summary>
    /// Con qué empieza la etiqueta: el EAN-13 de peso variable usa 20 a 29, según cómo esté
    /// programada la balanza. 20 + 5 de producto + 5 de peso + verificador = 13.
    /// </summary>
    public string EtiquetaPrefijo { get; set; } = "20";

    /// <summary>Cuántos dígitos son el código del producto en la balanza (PLU).</summary>
    public int EtiquetaDigitosProducto { get; set; } = 5;

    /// <summary>Decimales del peso: 3 = el peso viene en gramos (01250 = 1,250 kg).</summary>
    public int EtiquetaDecimalesPeso { get; set; } = 3;

    public AjustesApp Clonar() => (AjustesApp)MemberwiseClone();
}
